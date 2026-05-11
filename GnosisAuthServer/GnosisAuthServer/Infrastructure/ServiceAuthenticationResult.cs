namespace GnosisAuthServer.Infrastructure;

public sealed class ServiceAuthenticationResult
{
    private ServiceAuthenticationResult(bool isAuthenticated, ServiceAuthContext? context, string error)
    {
        IsAuthenticated = isAuthenticated;
        Context = context;
        Error = error;
    }

    public bool IsAuthenticated { get; }

    public ServiceAuthContext? Context { get; }

    public string Error { get; }

    public static ServiceAuthenticationResult Success(ServiceAuthContext context)
        => new(true, context, string.Empty);

    public static ServiceAuthenticationResult Failure(string error)
        => new(false, null, error);
}
