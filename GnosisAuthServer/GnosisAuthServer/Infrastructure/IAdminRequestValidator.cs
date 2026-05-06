namespace GnosisAuthServer.Infrastructure;

public interface IAdminRequestValidator
{
    bool TryAuthorize(HttpRequest request, out string error);
}
