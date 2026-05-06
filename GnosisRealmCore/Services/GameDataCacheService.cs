using GnosisRealmCore.Data;
using GnosisRealmCore.Models;
using GnosisRealmCore.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace GnosisRealmCore.Services;

public sealed class GameDataCacheService : IGameDataCacheService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAuthApiClient _authApiClient;
    private readonly GameDataCacheOptions _options;
    private readonly ILogger<GameDataCacheService> _logger;
    private readonly object _sync = new();

    private RealmGameDataSnapshotResponse _snapshot = new()
    {
        VersionNumber = 0,
        VersionTag = "empty",
        ContentHash = string.Empty,
        PublishedAtUtc = DateTime.MinValue,
        CachedAtUtc = DateTime.UtcNow
    };

    public GameDataCacheService(
        IServiceScopeFactory scopeFactory,
        IAuthApiClient authApiClient,
        IOptions<GameDataCacheOptions> options,
        ILogger<GameDataCacheService> logger)
    {
        _scopeFactory = scopeFactory;
        _authApiClient = authApiClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task WarmAsync(CancellationToken cancellationToken)
    {
        if (_options.WarmOnStartup)
        {
            await RefreshAsync(cancellationToken);
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            var global = await _authApiClient.GetGlobalGameDataSnapshotAsync(cancellationToken);
            if (global is null)
            {
                throw new InvalidOperationException("AuthApi returned no GameData snapshot.");
            }

            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<RealmDbContext>();
            var overrides = await dbContext.RealmGameDataOverrides
                .AsNoTracking()
                .Where(x => x.IsEnabled)
                .ToListAsync(cancellationToken);

            var merged = new RealmGameDataSnapshotResponse
            {
                VersionNumber = global.VersionNumber,
                VersionTag = global.VersionTag,
                ContentHash = global.ContentHash,
                PublishedAtUtc = global.PublishedAtUtc,
                CachedAtUtc = DateTime.UtcNow,
                Items = Merge(global.Items, overrides, "items"),
                Entities = Merge(global.Entities, overrides, "entities"),
                Quests = Merge(global.Quests, overrides, "quests"),
                Spells = Merge(global.Spells, overrides, "spells"),
                Auras = Merge(global.Auras, overrides, "auras"),
                OverrideCount = overrides.Count
            };

            lock (_sync)
            {
                _snapshot = merged;
            }

            _logger.LogInformation("Realm GameData cache refreshed. Version {VersionNumber} ({VersionTag}).", merged.VersionNumber, merged.VersionTag);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh Realm GameData cache from AuthApi.");
            if (!_options.AllowStaleCacheWhenAuthApiIsUnavailable)
            {
                throw;
            }
        }
    }

    public RealmGameDataVersionResponse GetVersion()
    {
        lock (_sync)
        {
            return new RealmGameDataVersionResponse
            {
                VersionNumber = _snapshot.VersionNumber,
                VersionTag = _snapshot.VersionTag,
                ContentHash = _snapshot.ContentHash,
                PublishedAtUtc = _snapshot.PublishedAtUtc,
                CachedAtUtc = _snapshot.CachedAtUtc
            };
        }
    }

    public RealmGameDataSnapshotResponse GetSnapshot()
    {
        lock (_sync)
        {
            return new RealmGameDataSnapshotResponse
            {
                VersionNumber = _snapshot.VersionNumber,
                VersionTag = _snapshot.VersionTag,
                ContentHash = _snapshot.ContentHash,
                PublishedAtUtc = _snapshot.PublishedAtUtc,
                CachedAtUtc = _snapshot.CachedAtUtc,
                Items = _snapshot.Items.ToList(),
                Entities = _snapshot.Entities.ToList(),
                Quests = _snapshot.Quests.ToList(),
                Spells = _snapshot.Spells.ToList(),
                Auras = _snapshot.Auras.ToList(),
                OverrideCount = _snapshot.OverrideCount
            };
        }
    }

    private static List<GameDataEntryDto> Merge(List<GameDataEntryDto> globalEntries, List<RealmGameDataOverride> overrides, string category)
    {
        var map = globalEntries.ToDictionary(x => x.AssetId, x => new GameDataEntryDto
        {
            AssetId = x.AssetId,
            ClassType = x.ClassType,
            JsonData = x.JsonData,
            IsEnabled = x.IsEnabled
        }, StringComparer.Ordinal);

        foreach (var item in overrides.Where(x => string.Equals(x.Category, category, StringComparison.OrdinalIgnoreCase)))
        {
            if (string.Equals(item.OverrideAction, "delete", StringComparison.OrdinalIgnoreCase))
            {
                map.Remove(item.AssetId);
                continue;
            }

            map[item.AssetId] = new GameDataEntryDto
            {
                AssetId = item.AssetId,
                ClassType = item.ClassType,
                JsonData = item.JsonData,
                IsEnabled = item.IsEnabled
            };
        }

        return map.Values.OrderBy(x => x.AssetId, StringComparer.Ordinal).ToList();
    }
}
