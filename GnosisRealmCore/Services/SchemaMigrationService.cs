using GnosisRealmCore.Options;
using Microsoft.Extensions.Options;
using MySqlConnector;
using System.Security.Cryptography;
using System.Text;

namespace GnosisRealmCore.Services;

public sealed class SchemaMigrationService : ISchemaMigrationService
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly SchemaMigrationOptions _options;
    private readonly ILogger<SchemaMigrationService> _logger;

    public SchemaMigrationService(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        IOptions<SchemaMigrationOptions> options,
        ILogger<SchemaMigrationService> logger)
    {
        _configuration = configuration;
        _environment = environment;
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

        var migrationsDirectory = Path.IsPathRooted(_options.DirectoryPath)
            ? _options.DirectoryPath
            : Path.Combine(_environment.ContentRootPath, _options.DirectoryPath);

        if (!Directory.Exists(migrationsDirectory))
        {
            _logger.LogWarning("Schema migration directory does not exist: {Directory}", migrationsDirectory);
            return;
        }

        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureMigrationTableAsync(connection, cancellationToken);

        var applied = new Dictionary<string, string>(StringComparer.Ordinal);
        await using (var cmd = new MySqlCommand("SELECT migration_id, checksum_sha256 FROM schema_migrations;", connection))
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                applied[reader.GetString(0)] = reader.GetString(1);
            }
        }

        foreach (var file in Directory.GetFiles(migrationsDirectory, "*.sql").OrderBy(x => x, StringComparer.Ordinal))
        {
            var fileName = Path.GetFileName(file);
            var migrationId = Path.GetFileNameWithoutExtension(fileName);
            var sql = await File.ReadAllTextAsync(file, cancellationToken);
            var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql))).ToLowerInvariant();

            if (applied.TryGetValue(migrationId, out var existingChecksum))
            {
                if (!string.Equals(existingChecksum, checksum, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Migration checksum mismatch for {migrationId}. Existing migration files must be immutable.");
                }

                continue;
            }

            var isDestructive = migrationId.Contains(".destructive.", StringComparison.OrdinalIgnoreCase)
                || migrationId.Contains("_destructive_", StringComparison.OrdinalIgnoreCase);

            if (isDestructive && !_options.AllowDestructiveMigrations)
            {
                throw new InvalidOperationException($"Destructive migration blocked by configuration: {migrationId}");
            }

            _logger.LogInformation("Applying schema migration {MigrationId}...", migrationId);
            await ExecuteSqlBatchAsync(connection, sql, cancellationToken);

            await using var insert = new MySqlCommand(
                @"INSERT INTO schema_migrations (migration_id, checksum_sha256, applied_at_utc, is_destructive)
                  VALUES (@id, @checksum, @appliedAt, @isDestructive);", connection);

            insert.Parameters.AddWithValue("@id", migrationId);
            insert.Parameters.AddWithValue("@checksum", checksum);
            insert.Parameters.AddWithValue("@appliedAt", DateTime.UtcNow);
            insert.Parameters.AddWithValue("@isDestructive", isDestructive);
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

    private static async Task ExecuteSqlBatchAsync(MySqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        var batches = sql.Split(";
", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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
