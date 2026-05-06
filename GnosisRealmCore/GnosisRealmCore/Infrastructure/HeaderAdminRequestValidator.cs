using GnosisRealmCore.Options;
using Microsoft.Extensions.Options;
using System.Net;

namespace GnosisRealmCore.Infrastructure;

public sealed class HeaderAdminRequestValidator : IAdminRequestValidator
{
    private readonly AdminOptions _options;
    private readonly ILogger<HeaderAdminRequestValidator> _logger;

    public HeaderAdminRequestValidator(IOptions<AdminOptions> options, ILogger<HeaderAdminRequestValidator> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool TryAuthorize(HttpRequest request, out string error)
    {
        error = string.Empty;

        if (!_options.Enabled)
        {
            error = "Admin API is disabled.";
            return false;
        }

        var remoteIp = request.HttpContext.Connection.RemoteIpAddress?.ToString();
        if (_options.AllowedIpAddresses.Length > 0 && !string.IsNullOrWhiteSpace(remoteIp))
        {
            if (!_options.AllowedIpAddresses.Contains(remoteIp, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Rejected admin request from IP {Ip}.", remoteIp);
                error = "IP address is not allowed.";
                return false;
            }
        }

        var headerValue = request.Headers[_options.HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(headerValue) || !string.Equals(headerValue, _options.ApiKey, StringComparison.Ordinal))
        {
            error = "Invalid admin key.";
            return false;
        }

        return true;
    }
}
