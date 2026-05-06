using GnosisRealmCore.Models;
using GnosisRealmCore.Options;
using Microsoft.Extensions.Options;
using MySqlConnector;
using System.Security.Cryptography;
using System.Text;

namespace GnosisRealmCore.Services;

public sealed class SchemaMigrationService : ISchemaMigrationService
{
    private readonly IConfiguration _configuration;
    private readonly IAuthApiClient _authApiClient;
    private readonly SchemaMigrationOptions _options;
    private readonly ILogger<SchemaMigrationService> _logger;

    public SchemaMigrationService(
        IConfiguration configuration,
        IAuthApiClient authApiClient,
        IOptions<SchemaMigrationOptions> options,
        ILogger<SchemaMigrationService> logger)
    {
        _configuration = configuration;
        _authApiClient = authApiClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ApplyPendingMigrationsAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Schema migrations are disabled.");
            return;
        }

        var connectionString = _configuration.GetSection(DatabaseOptions.SectionName)["ConnectionString"]
            ?? throw new InvalidOperationException("Missing Database:ConnectionString.");

        var manifest = await _authApiClient.GetSchemaManifestAsync(cancellationToken);
        if (manifest is null || manifest.Migrations.Count == 0)
        {
            _logger.LogInformation("No remote schema migrations were returned by AuthApi.");
            return;
        }

        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureMigrationTableAsync(connection, cancellationToken);

        var applied = new Dictionary<string, string>(StringComparer.Ordinal);

        await using (var cmd = new MySqlCommand(
            "SELECT migration_id, checksum_sha256 FROM schema_migrations;",
            connection))
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                applied[reader.GetString(0)] = reader.GetString(1);
            }
        }

        foreach (var migration in manifest.Migrations.OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            if (applied.TryGetValue(migration.Id, out var existingChecksum))
            {
                if (!string.Equals(existingChecksum, migration.ChecksumSha256, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Migration checksum mismatch for {migration.Id}. Existing migration files must be immutable.");
                }

                continue;
            }

            if (migration.IsDestructive && !_options.AllowDestructiveMigrations)
            {
                throw new InvalidOperationException(
                    $"Destructive migration blocked by configuration: {migration.Id}");
            }

            var content = await _authApiClient.GetSchemaMigrationAsync(migration.Id, cancellationToken);
            if (content is null)
            {
                throw new InvalidOperationException($"Migration content was not returned for {migration.Id}.");
            }

            if (!string.Equals(content.ChecksumSha256, migration.ChecksumSha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Remote migration checksum mismatch for {migration.Id}.");
            }

            _logger.LogInformation("Applying remote schema migration {MigrationId}...", migration.Id);

            await ExecuteSqlBatchAsync(connection, content.Sql, cancellationToken);

            await using var insert = new MySqlCommand("""
                INSERT INTO schema_migrations
                (migration_id, checksum_sha256, applied_at_utc, is_destructive)
                VALUES (@id, @checksum, @appliedAt, @isDestructive);
                """, connection);

            insert.Parameters.AddWithValue("@id", migration.Id);
            insert.Parameters.AddWithValue("@checksum", migration.ChecksumSha256);
            insert.Parameters.AddWithValue("@appliedAt", DateTime.UtcNow);
            insert.Parameters.AddWithValue("@isDestructive", migration.IsDestructive);

            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task EnsureMigrationTableAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = """
        CREATE TABLE IF NOT EXISTS schema_migrations (
            migration_id VARCHAR(128) NOT NULL PRIMARY KEY,
            checksum_sha256 VARCHAR(64) NOT NULL,
            applied_at_utc DATETIME NOT NULL,
            is_destructive TINYINT(1) NOT NULL DEFAULT 0
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """;

        await using var cmd = new MySqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ExecuteSqlBatchAsync(
        MySqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        var batches = sql.Split(";\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var batch in batches)
        {
            if (string.IsNullOrWhiteSpace(batch))
            {
                continue;
            }

            await using var cmd = new MySqlCommand(batch, connection);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}