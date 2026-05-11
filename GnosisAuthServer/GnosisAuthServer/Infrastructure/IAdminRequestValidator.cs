namespace GnosisAuthServer.Infrastructure;

public interface IAdminRequestValidator
{
    Task<AdminAuthorizationResult> AuthorizeAsync(HttpRequest request, CancellationToken cancellationToken);
}
