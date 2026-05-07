using GnosisAuthServer.Models;
using GnosisAuthServer.Options;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace GnosisAuthServer.Services;

public sealed class SchemaCatalogService(
    IWebHostEnvironment environment,
    IOptions<SchemaDeliveryOptions> options,
    ILogger<SchemaCatalogService> logger) : ISchemaCatalogService
{
    private readonly IWebHostEnvironment _environment = environment;
    private readonly SchemaDeliveryOptions _options = options.Value;
    private readonly ILogger<SchemaCatalogService> _logger = logger;

    public async Task<SchemaManifestResponse> GetManifestAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return new SchemaManifestResponse
            {
                Channel = _options.Channel,
                LatestMigrationId = string.Empty,
                MigrationCount = 0,
                PublishedAtUtc = DateTime.UtcNow
            };
        }

        var directory = ResolveDirectoryPath();
        if (!Directory.Exists(directory))
        {
            _logger.LogWarning("Schema directory does not exist: {Directory}", directory);

            return new SchemaManifestResponse
            {
                Channel = _options.Channel,
                LatestMigrationId = string.Empty,
                MigrationCount = 0,
                PublishedAtUtc = DateTime.UtcNow
            };
        }

        var files = Directory
            .GetFiles(directory, "*.mysql", SearchOption.TopDirectoryOnly)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var migrations = new List<SchemaMigrationDescriptorResponse>();

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var migrationId = Path.GetFileNameWithoutExtension(fileName);
            var sql = await File.ReadAllTextAsync(file, cancellationToken);

            var checksum = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(sql)))
                .ToLowerInvariant();

            var isDestructive =
                migrationId.Contains(".destructive.", StringComparison.OrdinalIgnoreCase) ||
                migrationId.Contains("_destructive_", StringComparison.OrdinalIgnoreCase);

            migrations.Add(new SchemaMigrationDescriptorResponse
            {
                Id = migrationId,
                ChecksumSha256 = checksum,
                IsDestructive = isDestructive
            });
        }

        return new SchemaManifestResponse
        {
            Channel = _options.Channel,
            LatestMigrationId = migrations.LastOrDefault()?.Id ?? string.Empty,
            MigrationCount = migrations.Count,
            PublishedAtUtc = DateTime.UtcNow,
            Migrations = migrations
        };
    }

    public async Task<SchemaMigrationContentResponse?> GetMigrationAsync(string migrationId, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(migrationId))
        {
            return null;
        }

        var directory = ResolveDirectoryPath();
        if (!Directory.Exists(directory))
        {
            return null;
        }

        var file = Directory
            .GetFiles(directory, "*.mysql", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(x =>
                string.Equals(
                    Path.GetFileNameWithoutExtension(Path.GetFileName(x)),
                    migrationId,
                    StringComparison.Ordinal));

        if (file is null)
        {
            return null;
        }

        var sql = await File.ReadAllTextAsync(file, cancellationToken);

        var checksum = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(sql)))
            .ToLowerInvariant();

        var isDestructive =
            migrationId.Contains(".destructive.", StringComparison.OrdinalIgnoreCase) ||
            migrationId.Contains("_destructive_", StringComparison.OrdinalIgnoreCase);

        return new SchemaMigrationContentResponse
        {
            Id = migrationId,
            ChecksumSha256 = checksum,
            IsDestructive = isDestructive,
            Sql = sql
        };
    }

    private string ResolveDirectoryPath()
    {
        if (Path.IsPathRooted(_options.DirectoryPath))
        {
            return _options.DirectoryPath;
        }

        return Path.Combine(_environment.ContentRootPath, _options.DirectoryPath);
    }
}