using GnosisAuthServer.Data;
using GnosisAuthServer.Models;
using GnosisAuthServer.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GnosisAuthServer.Services;

public sealed class GameDataService : IGameDataService
{
    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly AuthDbContext _dbContext;
    private readonly GameDataOptions _options;

    public GameDataService(AuthDbContext dbContext, IOptions<GameDataOptions> options)
    {
        _dbContext = dbContext;
        _options = options.Value;
    }

    public async Task<GlobalGameDataVersionResponse> GetCurrentVersionAsync(CancellationToken cancellationToken)
    {
        var latest = await _dbContext.GameDataVersions.AsNoTracking()
            .OrderByDescending(x => x.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is null)
        {
            return new GlobalGameDataVersionResponse
            {
                VersionNumber = 0,
                VersionTag = "empty",
                ContentHash = string.Empty,
                PublishedAtUtc = DateTime.UnixEpoch
            };
        }

        return new GlobalGameDataVersionResponse
        {
            VersionNumber = latest.VersionNumber,
            VersionTag = latest.VersionTag,
            ContentHash = latest.ContentHash,
            PublishedAtUtc = latest.PublishedAtUtc
        };
    }

    public async Task<GlobalGameDataSnapshotResponse> GetCurrentSnapshotAsync(CancellationToken cancellationToken)
    {
        var latest = await GetCurrentVersionAsync(cancellationToken);

        return new GlobalGameDataSnapshotResponse
        {
            VersionNumber = latest.VersionNumber,
            VersionTag = latest.VersionTag,
            ContentHash = latest.ContentHash,
            PublishedAtUtc = latest.PublishedAtUtc,
            Items = [.. (await ProjectEntriesAsync(_dbContext.GameItems, cancellationToken))],
            Entities = [.. (await ProjectEntriesAsync(_dbContext.GameEntities, cancellationToken))],
            Quests = [.. (await ProjectEntriesAsync(_dbContext.GameQuests, cancellationToken))],
            Spells = [.. (await ProjectEntriesAsync(_dbContext.GameSpells, cancellationToken))],
            Auras = [.. (await ProjectEntriesAsync(_dbContext.GameAuras, cancellationToken))]
        };
    }

    public async Task<GlobalGameDataSnapshotResponse> ReplaceSnapshotAsync(ReplaceGlobalGameDataRequest request, CancellationToken cancellationToken)
    {
        ValidateEntries(request.Items, nameof(request.Items), _options);
        ValidateEntries(request.Entities, nameof(request.Entities), _options);
        ValidateEntries(request.Quests, nameof(request.Quests), _options);
        ValidateEntries(request.Spells, nameof(request.Spells), _options);
        ValidateEntries(request.Auras, nameof(request.Auras), _options);

        var publishedAt = DateTime.UtcNow;
        var nextVersion = (await _dbContext.GameDataVersions.MaxAsync(x => (int?)x.VersionNumber, cancellationToken) ?? 0) + 1;
        var versionTag = string.IsNullOrWhiteSpace(request.VersionTag) ? $"v{nextVersion}" : request.VersionTag.Trim();

        var canonicalPayload = new
        {
            items = Normalize(request.Items),
            entities = Normalize(request.Entities),
            quests = Normalize(request.Quests),
            spells = Normalize(request.Spells),
            auras = Normalize(request.Auras)
        };

        var canonicalJson = JsonSerializer.Serialize(canonicalPayload, CanonicalJsonOptions);
        var contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson))).ToLowerInvariant();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        _dbContext.GameItems.RemoveRange(_dbContext.GameItems);
        _dbContext.GameEntities.RemoveRange(_dbContext.GameEntities);
        _dbContext.GameQuests.RemoveRange(_dbContext.GameQuests);
        _dbContext.GameSpells.RemoveRange(_dbContext.GameSpells);
        _dbContext.GameAuras.RemoveRange(_dbContext.GameAuras);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _dbContext.GameItems.AddRange(MapEntries<DbItem>(request.Items, publishedAt));
        _dbContext.GameEntities.AddRange(MapEntries<DbEntity>(request.Entities, publishedAt));
        _dbContext.GameQuests.AddRange(MapEntries<DbQuest>(request.Quests, publishedAt));
        _dbContext.GameSpells.AddRange(MapEntries<DbSpell>(request.Spells, publishedAt));
        _dbContext.GameAuras.AddRange(MapEntries<DbAura>(request.Auras, publishedAt));

        _dbContext.GameDataVersions.Add(new GameDataVersion
        {
            VersionNumber = nextVersion,
            VersionTag = versionTag,
            ContentHash = contentHash,
            Notes = request.Notes?.Trim(),
            PublishedAtUtc = publishedAt
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetCurrentSnapshotAsync(cancellationToken);
    }

    private static void ValidateEntries(List<GameDataEntryDto> entries, string collectionName, GameDataOptions options)
    {
        if (entries.Count > options.MaxEntriesPerCollection)
        {
            throw new InvalidOperationException($"{collectionName} exceeds the maximum allowed entries ({options.MaxEntriesPerCollection}).");
        }

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.AssetId))
            {
                throw new InvalidOperationException($"{collectionName} contains an empty AssetId.");
            }

            if (string.IsNullOrWhiteSpace(entry.ClassType))
            {
                throw new InvalidOperationException($"{collectionName} contains an empty ClassType for AssetId '{entry.AssetId}'.");
            }

            if (string.IsNullOrWhiteSpace(entry.JsonData))
            {
                throw new InvalidOperationException($"{collectionName} contains empty JsonData for AssetId '{entry.AssetId}'.");
            }

            if (entry.JsonData.Length > options.MaxJsonDataLength)
            {
                throw new InvalidOperationException($"{collectionName} contains oversized JsonData for AssetId '{entry.AssetId}'.");
            }

            try
            {
                using var _ = JsonDocument.Parse(entry.JsonData);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"{collectionName} contains invalid JsonData for AssetId '{entry.AssetId}': {ex.Message}");
            }
        }

        var duplicates = entries
            .GroupBy(x => x.AssetId.Trim(), StringComparer.Ordinal)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException($"Duplicate AssetId values found in {collectionName}: {string.Join(", ", duplicates)}");
        }
    }

    private static List<GameDataEntryDto> Normalize(List<GameDataEntryDto> entries)
    {
        return [.. entries
            .Select(x => new GameDataEntryDto
            {
                AssetId = x.AssetId.Trim(),
                ClassType = x.ClassType.Trim(),
                JsonData = CanonicalizeJson(x.JsonData),
                IsEnabled = x.IsEnabled
            })
            .OrderBy(x => x.AssetId, StringComparer.Ordinal)
            .ThenBy(x => x.ClassType, StringComparer.Ordinal)];
    }

    private static string CanonicalizeJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(document.RootElement, CanonicalJsonOptions);
    }

    private static IEnumerable<T> MapEntries<T>(IEnumerable<GameDataEntryDto> entries, DateTime utcNow) where T : GameDataBaseEntity, new()
    {
        return Normalize([.. entries]).Select(x => new T
        {
            AssetId = x.AssetId,
            ClassType = x.ClassType,
            JsonData = x.JsonData,
            IsEnabled = x.IsEnabled,
            LastUpdatedUtc = utcNow
        });
    }

    private static async Task<IReadOnlyList<GameDataEntryDto>> ProjectEntriesAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) where T : GameDataBaseEntity
    {
        return await query.AsNoTracking()
            .OrderBy(x => x.AssetId)
            .ThenBy(x => x.ClassType)
            .Select(x => new GameDataEntryDto
            {
                AssetId = x.AssetId,
                ClassType = x.ClassType,
                JsonData = x.JsonData,
                IsEnabled = x.IsEnabled
            })
            .ToListAsync(cancellationToken);
    }
}
