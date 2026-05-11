using GnosisAuthServer.Options;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace GnosisAuthServer.Infrastructure;

public sealed class HmacServiceRequestAuthenticator : IServiceRequestAuthenticator
{
    private readonly ServiceAuthOptions _options;
    private readonly INonceStore _nonceStore;
    private readonly ILogger<HmacServiceRequestAuthenticator> _logger;

    public HmacServiceRequestAuthenticator(
        IOptions<ServiceAuthOptions> options,
        INonceStore nonceStore,
        ILogger<HmacServiceRequestAuthenticator> logger)
    {
        _options = options.Value;
        _nonceStore = nonceStore;
        _logger = logger;
    }

    public async Task<ServiceAuthenticationResult> AuthenticateAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return ServiceAuthenticationResult.Success(new ServiceAuthContext("disabled", Array.Empty<string>()));
        }

        var serviceId = request.Headers[ServiceAuthHeaderNames.ServiceId].ToString();
        var timestampRaw = request.Headers[ServiceAuthHeaderNames.Timestamp].ToString();
        var nonce = request.Headers[ServiceAuthHeaderNames.Nonce].ToString();
        var signatureHex = request.Headers[ServiceAuthHeaderNames.Signature].ToString();
        var bodyHash = request.Headers[ServiceAuthHeaderNames.BodySha256].ToString();

        if (string.IsNullOrWhiteSpace(serviceId) ||
            string.IsNullOrWhiteSpace(timestampRaw) ||
            string.IsNullOrWhiteSpace(nonce) ||
            string.IsNullOrWhiteSpace(signatureHex) ||
            string.IsNullOrWhiteSpace(bodyHash))
        {
            return ServiceAuthenticationResult.Failure("Missing service authentication headers.");
        }

        var client = _options.Clients.FirstOrDefault(x => string.Equals(x.ServiceId, serviceId, StringComparison.Ordinal));
        if (client is null || string.IsNullOrWhiteSpace(client.Secret))
        {
            return ServiceAuthenticationResult.Failure("Unknown service identity.");
        }

        if (!long.TryParse(timestampRaw, out var unixTime))
        {
            return ServiceAuthenticationResult.Failure("Invalid timestamp.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - unixTime) > _options.AllowedClockSkewSeconds)
        {
            return ServiceAuthenticationResult.Failure("Timestamp out of range.");
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
            _logger.LogWarning(ex, "Service authentication failed for {ServiceId}: body hash computation error.", serviceId);
            return ServiceAuthenticationResult.Failure("Unable to verify request body.");
        }

        if (!string.Equals(bodyHash, computedBodyHash, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Service authentication failed for {ServiceId}: body hash mismatch.", serviceId);
            return ServiceAuthenticationResult.Failure("Invalid body hash.");
        }

        var canonical = string.Join("\n",
            request.Method.ToUpperInvariant(),
            request.Path.Value ?? "/",
            request.QueryString.HasValue ? request.QueryString.Value! : string.Empty,
            timestampRaw,
            nonce,
            computedBodyHash.ToLowerInvariant());

        byte[] receivedSignature;
        try
        {
            receivedSignature = Convert.FromHexString(signatureHex);
        }
        catch (FormatException)
        {
            return ServiceAuthenticationResult.Failure("Invalid signature format.");
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(client.Secret));
        var computedSignature = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));

        if (!CryptographicOperations.FixedTimeEquals(computedSignature, receivedSignature))
        {
            _logger.LogWarning("Service authentication failed for {ServiceId}: invalid signature.", serviceId);
            return ServiceAuthenticationResult.Failure("Invalid signature.");
        }

        if (!_nonceStore.TryUseNonce(serviceId, nonce, TimeSpan.FromSeconds(_options.NonceTtlSeconds)))
        {
            return ServiceAuthenticationResult.Failure("Replay detected.");
        }

        return ServiceAuthenticationResult.Success(new ServiceAuthContext(client.ServiceId, client.AllowedRealmIds));
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
