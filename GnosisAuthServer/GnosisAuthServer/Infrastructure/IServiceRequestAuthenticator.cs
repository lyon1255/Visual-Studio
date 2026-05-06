namespace GnosisAuthServer.Infrastructure;

public interface IServiceRequestAuthenticator
{
    bool TryAuthenticate(HttpRequest request, out ServiceAuthContext? context, out string error);
}
