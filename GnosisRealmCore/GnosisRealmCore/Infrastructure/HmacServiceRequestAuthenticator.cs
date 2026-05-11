using GnosisRealmCore.Options;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace GnosisRealmCore.Infrastructure;

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

    public bool TryAuthenticate(HttpRequest request, out ServiceAuthContext? context, out string error)
    {
        context = null;
        error = string.Empty;

        if (!_options.Enabled)
        {
            context = new ServiceAuthContext("disabled", Array.Empty<string>());
            return true;
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
            error = "Missing service authentication headers.";
            return false;
        }

        var client = _options.Clients.FirstOrDefault(x => string.Equals(x.ServiceId, serviceId, StringComparison.Ordinal));
        if (client is null || string.IsNullOrWhiteSpace(client.Secret))
        {
            error = "Unknown service identity.";
            return false;
        }

        if (!long.TryParse(timestampRaw, out var unixTime))
        {
            error = "Invalid timestamp.";
            return false;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - unixTime) > _options.AllowedClockSkewSeconds)
        {
            error = "Timestamp out of range.";
            return false;
        }

        if (!_nonceStore.TryUseNonce(serviceId, nonce, TimeSpan.FromSeconds(_options.NonceTtlSeconds)))
        {
            error = "Replay detected.";
            return false;
        }

        var canonical = string.Join("\n",
            request.Method.ToUpperInvariant(),
            request.Path.Value ?? "/",
            timestampRaw,
            nonce,
            bodyHash);

        byte[] receivedSignature;
        try
        {
            receivedSignature = Convert.FromHexString(signatureHex);
        }
        catch (FormatException)
        {
            error = "Invalid signature format.";
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(client.Secret));
        var computed = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));

        if (!CryptographicOperations.FixedTimeEquals(receivedSignature, computed))
        {
            _logger.LogWarning("Invalid HMAC signature for service {ServiceId}.", serviceId);
            error = "Invalid signature.";
            return false;
        }

        context = new ServiceAuthContext(client.ServiceId, client.AllowedRealmIds);
        return true;
    }
}