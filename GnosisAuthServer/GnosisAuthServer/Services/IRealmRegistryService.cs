using GnosisAuthServer.Models;

namespace GnosisAuthServer.Services;

public interface IRealmRegistryService
{
    Task UpsertHeartbeatAsync(
        RealmHeartbeatRequest request,
        bool isOfficialCaller,
        bool isCommunityCaller,
        string callerServiceId,
        IReadOnlyCollection<string> allowedRealmIds,
        CancellationToken cancellationToken = default);
}