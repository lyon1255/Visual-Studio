using GnosisAuthServer.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace GnosisAuthServer.Infrastructure;

public sealed class HeaderAdminRequestValidator : IAdminRequestValidator
{
    private readonly AdminOptions _options;
    private readonly INonceStore _nonceStore;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<HeaderAdminRequestValidator> _logger;

    public HeaderAdminRequestValidator(
        IOptions<AdminOptions> options,
        INonceStore nonceStore,
        IHostEnvironment environment,
        ILogger<HeaderAdminRequestValidator> logger)
    {
        _options = options.Value;
        _nonceStore = nonceStore;
        _environment = environment;
        _logger = logger;
    }

    public async Task<AdminAuthorizationResult> AuthorizeAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return AdminAuthorizationResult.Failure("Admin API is disabled.");
        }

        if (_environment.IsProduction()
            && _options.RequireExplicitIpAllowlistInProduction
            && _options.AllowedIpAddresses.Length == 0
            && _options.AllowedIpNetworks.Length == 0)
        {
            _logger.LogError("Admin authorization rejected because no explicit IP or network allowlist is configured in production.");
            return AdminAuthorizationResult.Failure("Admin IP allowlist is required in production.");
        }

        if (!RequestIpAllowed(request))
        {
            return AdminAuthorizationResult.Failure("Admin IP is not allowed.");
        }

        if (_options.RequireHmac)
        {
            return await AuthorizeHmacAsync(request, cancellationToken);
        }

        return AuthorizeLegacyApiKey(request);
    }

    private async Task<AdminAuthorizationResult> AuthorizeHmacAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var timestampRaw = request.Headers[AdminAuthHeaderNames.Timestamp].ToString();
        var nonce = request.Headers[AdminAuthHeaderNames.Nonce].ToString();
        var signatureHex = request.Headers[AdminAuthHeaderNames.Signature].ToString();
        var bodyHash = request.Headers[AdminAuthHeaderNames.BodySha256].ToString();

        if (string.IsNullOrWhiteSpace(timestampRaw) ||
            string.IsNullOrWhiteSpace(nonce) ||
            string.IsNullOrWhiteSpace(signatureHex) ||
            string.IsNullOrWhiteSpace(bodyHash))
        {
            return AdminAuthorizationResult.Failure("Missing admin authentication headers.");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return AdminAuthorizationResult.Failure("Admin HMAC secret is not configured.");
        }

        if (!long.TryParse(timestampRaw, out var unixTime))
        {
            return AdminAuthorizationResult.Failure("Invalid admin timestamp.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - unixTime) > _options.AllowedClockSkewSeconds)
        {
            return AdminAuthorizationResult.Failure("Admin timestamp out of range.");
        }

        string computedBodyHash;
        try
        {
            computedBodyHash = await ComputeBodyHashAsync(request, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Admin authentication failed during body hash computation.");
            return AdminAuthorizationResult.Failure("Unable to verify admin request body.");
        }

        if (!string.Equals(bodyHash, computedBodyHash, StringComparison.OrdinalIgnoreCase))
        {
            return AdminAuthorizationResult.Failure("Invalid admin body hash.");
        }

        var canonical = string.Join("\n",
            request.Method.ToUpperInvariant(),
            request.Path.Value ?? "/",
            request.QueryString.HasValue ? request.QueryString.Value! : string.Empty,
            timestampRaw,
            nonce,
            computedBodyHash);

        byte[] receivedSignature;
        try
        {
            receivedSignature = Convert.FromHexString(signatureHex);
        }
        catch (FormatException)
        {
            return AdminAuthorizationResult.Failure("Invalid admin signature format.");
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.ApiKey));
        var computedSignature = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));

        if (!CryptographicOperations.FixedTimeEquals(computedSignature, receivedSignature))
        {
            return AdminAuthorizationResult.Failure("Invalid admin signature.");
        }

        if (!_nonceStore.TryUseNonce("admin", nonce, TimeSpan.FromSeconds(_options.NonceTtlSeconds)))
        {
            return AdminAuthorizationResult.Failure("Admin replay detected.");
        }

        return AdminAuthorizationResult.Success();
    }

    private AdminAuthorizationResult AuthorizeLegacyApiKey(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(_options.HeaderName, out var values))
        {
            return AdminAuthorizationResult.Failure("Missing admin header.");
        }

        var incoming = values.ToString();
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(incoming))
        {
            return AdminAuthorizationResult.Failure("Invalid admin key.");
        }

        var expectedBytes = Encoding.UTF8.GetBytes(_options.ApiKey);
        var actualBytes = Encoding.UTF8.GetBytes(incoming);
        if (!CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
        {
            return AdminAuthorizationResult.Failure("Invalid admin key.");
        }

        return AdminAuthorizationResult.Success();
    }

    private bool RequestIpAllowed(HttpRequest request)
    {
        if (_options.AllowedIpAddresses.Length == 0 && _options.AllowedIpNetworks.Length == 0)
        {
            return true;
        }

        var remoteIp = request.HttpContext.Connection.RemoteIpAddress;
        if (remoteIp is null)
        {
            return false;
        }

        var ipMatch = _options.AllowedIpAddresses.Any(x => IPAddress.TryParse(x, out var allowed) && allowed.Equals(remoteIp));
        if (ipMatch)
        {
            return true;
        }

        return _options.AllowedIpNetworks.Any(x => IPNetwork.TryParse(x, out var network) && network.Contains(remoteIp));
    }

    private static async Task<string> ComputeBodyHashAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        request.EnableBuffering();
        request.Body.Position = 0;

        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(request.Body, cancellationToken);

        request.Body.Position = 0;
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
