namespace GnosisAuthServer.Infrastructure;

public sealed class ServiceAuthContext
{
    public ServiceAuthContext(string serviceId, IReadOnlyCollection<string> allowedRealmIds)
    {
        ServiceId = serviceId;
        AllowedRealmIds = allowedRealmIds;
    }

    public string ServiceId { get; }

    public IReadOnlyCollection<string> AllowedRealmIds { get; }
}