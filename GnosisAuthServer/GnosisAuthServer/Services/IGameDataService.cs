using GnosisAuthServer.Models;

namespace GnosisAuthServer.Services;

public interface IGameDataService
{
    Task<GlobalGameDataVersionResponse> GetCurrentVersionAsync(CancellationToken cancellationToken);
    Task<GlobalGameDataSnapshotResponse> GetCurrentSnapshotAsync(CancellationToken cancellationToken);
    Task<GlobalGameDataSnapshotResponse> ReplaceSnapshotAsync(ReplaceGlobalGameDataRequest request, CancellationToken cancellationToken);
}
