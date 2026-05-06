using GnosisRealmCore.Models;

namespace GnosisRealmCore.Services;

public interface IAuthApiClient
{
    Task<GlobalGameDataVersionResponse?> GetGlobalGameDataVersionAsync(CancellationToken cancellationToken);
    Task<GlobalGameDataSnapshotResponse?> GetGlobalGameDataSnapshotAsync(CancellationToken cancellationToken);
    Task SendOfficialHeartbeatAsync(OfficialRealmHeartbeatRequest request, CancellationToken cancellationToken);
}
