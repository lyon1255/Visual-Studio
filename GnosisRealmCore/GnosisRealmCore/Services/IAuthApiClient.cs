using GnosisRealmCore.Models;

namespace GnosisRealmCore.Services;

public interface IAuthApiClient
{
    Task<GlobalGameDataVersionResponse?> GetGlobalGameDataVersionAsync(CancellationToken cancellationToken);
    Task<GlobalGameDataSnapshotResponse?> GetGlobalGameDataSnapshotAsync(CancellationToken cancellationToken);
    Task SendOfficialHeartbeatAsync(OfficialRealmHeartbeatRequest request, CancellationToken cancellationToken);

    Task<SchemaManifestResponse?> GetSchemaManifestAsync(CancellationToken cancellationToken);
    Task<SchemaMigrationContentResponse?> GetSchemaMigrationAsync(string migrationId, CancellationToken cancellationToken);
}