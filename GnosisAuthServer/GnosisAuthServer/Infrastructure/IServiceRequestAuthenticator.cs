namespace GnosisAuthServer.Infrastructure;

public interface IServiceRequestAuthenticator
{
    Task<ServiceAuthenticationResult> AuthenticateAsync(HttpRequest request, CancellationToken cancellationToken);
}
