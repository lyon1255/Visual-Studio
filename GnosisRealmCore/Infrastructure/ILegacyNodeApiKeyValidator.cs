namespace GnosisRealmCore.Infrastructure;

public interface ILegacyNodeApiKeyValidator
{
    bool IsAuthorized(HttpRequest request);
}
