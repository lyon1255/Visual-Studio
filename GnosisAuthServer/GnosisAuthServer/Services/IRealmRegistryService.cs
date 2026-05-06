using GnosisAuthServer.Data;
using GnosisAuthServer.Models;

namespace GnosisAuthServer.Services;

public interface IRealmRegistryService
{
    Task<IReadOnlyList<Realm>> GetPublicRealmsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Realm>> GetAllRealmsAsync(CancellationToken cancellationToken);
    Task<Realm?> GetRealmAsync(string realmId, CancellationToken cancellationToken);
    Task<Realm> CreateRealmAsync(AdminRealmUpsertRequest request, CancellationToken cancellationToken);
    Task<Realm?> UpdateRealmAsync(string realmId, AdminRealmUpsertRequest request, CancellationToken cancellationToken);
    Task<Realm?> UpsertOfficialHeartbeatAsync(OfficialRealmHeartbeatRequest request, CancellationToken cancellationToken);
}
