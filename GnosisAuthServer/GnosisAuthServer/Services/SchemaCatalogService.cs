using GnosisAuthServer.Models;
using GnosisAuthServer.Options;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace GnosisAuthServer.Services;

public sealed class SchemaCatalogService : ISchemaCatalogService
{
    private readonly IWebHostEnvironment _environment;
    private readonly SchemaDeliveryOptions _options;
    private readonly ILogger<SchemaCatalogService> _logger;

    public SchemaCatalogService(
        IWebHostEnvironment environment,
        IOptions<SchemaDeliveryOptions> options,
        ILogger<SchemaCatalogService> logger)
    {
        _environment = environment;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SchemaManifestResponse> GetManifestAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return new SchemaManifestResponse
            {
                Channel = _options.Channel,
                LatestMigrationId = string.Empty,
                MigrationCount = 0,
                PublishedAtUtc = DateTime.UtcNow,
                Migrations = new List<SchemaMigrationDescriptorResponse>()
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
                PublishedAtUtc = DateTime.UtcNow,
                Migrations = new List<SchemaMigrationDescriptorResponse>()
            };
        }

        var files = Directory
            .GetFiles(directory, "*.mysql", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();

        if (files.Count == 0)
        {
            _logger.LogWarning("Schema directory contains no .mysql files: {Directory}", directory);

            return new SchemaManifestResponse
            {
                Channel = _options.Channel,
                LatestMigrationId = string.Empty,
                MigrationCount = 0,
                PublishedAtUtc = DateTime.UtcNow,
                Migrations = new List<SchemaMigrationDescriptorResponse>()
            };
        }

        var migrations = new List<SchemaMigrationDescriptorResponse>(files.Count);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var migrationId = Path.GetFileNameWithoutExtension(file);
            var sql = await File.ReadAllTextAsync(file, cancellationToken);

            migrations.Add(new SchemaMigrationDescriptorResponse
            {
                Id = migrationId,
                ChecksumSha256 = ComputeChecksumSha256(sql),
                IsDestructive = IsDestructiveMigrationName(migrationId)
            });
        }

        return new SchemaManifestResponse
        {
            Channel = _options.Channel,
            LatestMigrationId = migrations[^1].Id,
            MigrationCount = migrations.Count,
            PublishedAtUtc = DateTime.UtcNow,
            Migrations = migrations
        };
    }

    public async Task<SchemaMigrationContentResponse?> GetMigrationAsync(
        string migrationId,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(migrationId))
        {
            return null;
        }

        if (migrationId.Contains('/') || migrationId.Contains('\\'))
        {
            return null;
        }

        var directory = ResolveDirectoryPath();
        if (!Directory.Exists(directory))
        {
            _logger.LogWarning("Schema directory does not exist while fetching migration {MigrationId}: {Directory}", migrationId, directory);
            return null;
        }

        var file = Directory
            .GetFiles(directory, "*.mysql", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(path =>
                string.Equals(
                    Path.GetFileNameWithoutExtension(path),
                    migrationId,
                    StringComparison.Ordinal));

        if (file is null)
        {
            _logger.LogWarning("Schema migration was not found: {MigrationId}", migrationId);
            return null;
        }

        var sql = await File.ReadAllTextAsync(file, cancellationToken);

        return new SchemaMigrationContentResponse
        {
            Id = migrationId,
            ChecksumSha256 = ComputeChecksumSha256(sql),
            IsDestructive = IsDestructiveMigrationName(migrationId),
            Sql = sql
        };
    }

    private string ResolveDirectoryPath()
    {
        if (string.IsNullOrWhiteSpace(_options.DirectoryPath))
        {
            throw new InvalidOperationException("SchemaDelivery:DirectoryPath is missing.");
        }

        if (Path.IsPathRooted(_options.DirectoryPath))
        {
            return _options.DirectoryPath;
        }

        return Path.Combine(_environment.ContentRootPath, _options.DirectoryPath);
    }

    private static string ComputeChecksumSha256(string sql)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql))).ToLowerInvariant();
    }

    private static bool IsDestructiveMigrationName(string migrationId)
    {
        return migrationId.Contains(".destructive.", StringComparison.OrdinalIgnoreCase)
            || migrationId.EndsWith(".destructive", StringComparison.OrdinalIgnoreCase)
            || migrationId.Contains("_destructive_", StringComparison.OrdinalIgnoreCase)
            || migrationId.EndsWith("_destructive", StringComparison.OrdinalIgnoreCase);
    }
}