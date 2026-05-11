namespace GnosisAuthServer.Infrastructure;

public interface IServiceRequestAuthenticator
{
    Task<bool> TryAuthenticateAsync(HttpRequest request, CancellationToken cancellationToken, out ServiceAuthContext? context, out string error);
}
