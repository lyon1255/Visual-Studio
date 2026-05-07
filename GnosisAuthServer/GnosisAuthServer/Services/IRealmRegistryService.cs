using GnosisAuthServer.Data;
using GnosisAuthServer.Models;

namespace GnosisAuthServer.Services;

public interface IRealmRegistryService
{
    Task<IReadOnlyList<Realm>> GetPublicRealmsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Realm>> GetAllRealmsAsync(CancellationToken cancellationToken = default);
    Task<Realm> CreateRealmAsync(AdminRealmUpsertRequest request, CancellationToken cancellationToken = default);
    Task<Realm?> UpdateRealmAsync(string realmId, AdminRealmUpsertRequest request, CancellationToken cancellationToken = default);

    Task UpsertHeartbeatAsync(
        RealmHeartbeatRequest request,
        bool isOfficialCaller,
        bool isCommunityCaller,
        string callerServiceId,
        IReadOnlyCollection<string> allowedRealmIds,
        CancellationToken cancellationToken = default);
}
