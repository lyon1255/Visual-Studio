using GnosisRealmCore.Models;

namespace GnosisRealmCore.Services;

public interface IGameDataCacheService
{
    Task WarmAsync(CancellationToken cancellationToken);
    Task RefreshAsync(CancellationToken cancellationToken);
    RealmGameDataVersionResponse GetVersion();
    RealmGameDataSnapshotResponse GetSnapshot();
}
