namespace GnosisAuthServer.Infrastructure;

public sealed class AdminAuthorizationResult
{
    private AdminAuthorizationResult(bool isAuthorized, string error)
    {
        IsAuthorized = isAuthorized;
        Error = error;
    }

    public bool IsAuthorized { get; }

    public string Error { get; }

    public static AdminAuthorizationResult Success()
        => new(true, string.Empty);

    public static AdminAuthorizationResult Failure(string error)
        => new(false, error);
}
