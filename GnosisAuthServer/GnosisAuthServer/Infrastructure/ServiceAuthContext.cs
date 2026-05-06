namespace GnosisAuthServer.Infrastructure;

public sealed class ServiceAuthContext
{
    public ServiceAuthContext(string serviceId, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> allowedRealmIds)
    {
        ServiceId = serviceId;
        Roles = roles;
        AllowedRealmIds = allowedRealmIds;
    }

    public string ServiceId { get; }
    public IReadOnlyCollection<string> Roles { get; }
    public IReadOnlyCollection<string> AllowedRealmIds { get; }
}
