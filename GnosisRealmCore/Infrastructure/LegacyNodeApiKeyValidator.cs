using GnosisRealmCore.Options;
using Microsoft.Extensions.Options;

namespace GnosisRealmCore.Infrastructure;

public sealed class LegacyNodeApiKeyValidator : ILegacyNodeApiKeyValidator
{
    private readonly LegacyNodeAuthOptions _options;

    public LegacyNodeApiKeyValidator(IOptions<LegacyNodeAuthOptions> options)
    {
        _options = options.Value;
    }

    public bool IsAuthorized(HttpRequest request)
    {
        if (!_options.Enabled)
        {
            return false;
        }

        var value = request.Headers[_options.HeaderName].ToString();
        return !string.IsNullOrWhiteSpace(_options.ApiKey)
            && string.Equals(value, _options.ApiKey, StringComparison.Ordinal);
    }
}
