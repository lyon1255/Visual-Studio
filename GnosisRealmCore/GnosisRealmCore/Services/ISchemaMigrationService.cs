namespace GnosisRealmCore.Services;

public interface ISchemaMigrationService
{
    Task ApplyPendingMigrationsAsync(CancellationToken cancellationToken = default);
}
