using GnosisAuthServer.Models;

namespace GnosisAuthServer.Services;

public interface ISchemaCatalogService
{
    Task<SchemaManifestResponse> GetManifestAsync(CancellationToken cancellationToken = default);
    Task<SchemaMigrationContentResponse?> GetMigrationAsync(string migrationId, CancellationToken cancellationToken = default);
}