using GnosisAuthServer.Options;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace GnosisAuthServer.Infrastructure;

public sealed class HeaderAdminRequestValidator : IAdminRequestValidator
{
    private readonly AdminOptions _options;

    public HeaderAdminRequestValidator(IOptions<AdminOptions> options)
    {
        _options = options.Value;
    }

    public bool TryAuthorize(HttpRequest request, out string error)
    {
        error = string.Empty;

        if (!_options.Enabled)
        {
            error = "Admin API is disabled.";
            return false;
        }

        if (!RequestIpAllowed(request))
        {
            error = "Admin IP is not allowed.";
            return false;
        }

        if (!request.Headers.TryGetValue(_options.HeaderName, out var values))
        {
            error = "Missing admin header.";
            return false;
        }

        var incoming = values.ToString();
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(incoming))
        {
            error = "Invalid admin key.";
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(_options.ApiKey);
        var actualBytes = Encoding.UTF8.GetBytes(incoming);
        if (!CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
        {
            error = "Invalid admin key.";
            return false;
        }

        return true;
    }

    private bool RequestIpAllowed(HttpRequest request)
    {
        if (_options.AllowedIpAddresses.Length == 0)
        {
            return true;
        }

        var remoteIp = request.HttpContext.Connection.RemoteIpAddress;
        if (remoteIp is null)
        {
            return false;
        }

        return _options.AllowedIpAddresses.Any(x => IPAddress.TryParse(x, out var allowed) && allowed.Equals(remoteIp));
    }
}
