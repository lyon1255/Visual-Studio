using GnosisAuthServer.Data;
using GnosisAuthServer.Models;
using GnosisAuthServer.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GnosisAuthServer.Services;

public sealed class RealmRegistryService : IRealmRegistryService
{
    private readonly AuthDbContext _dbContext;
    private readonly RealmRegistryOptions _options;
    private readonly ILogger<RealmRegistryService> _logger;

    public RealmRegistryService(
        AuthDbContext dbContext,
        IOptions<RealmRegistryOptions> options,
        ILogger<RealmRegistryService> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Realm>> GetPublicRealmsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddSeconds(-Math.Max(30, _options.HeartbeatTimeoutSeconds));

        var query = _dbContext.Realms
            .AsNoTracking()
            .Where(x => x.Enabled && x.IsListed);

        if (_options.HideUnhealthyRealms)
        {
            query = query.Where(x =>
                x.LastHeartbeatAt != null &&
                x.LastHeartbeatAt >= cutoff &&
                x.Status != "offline");
        }

        return await query
            .OrderByDescending(x => x.RealmType == "official")
            .ThenBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Realm>> GetAllRealmsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Realms
            .AsNoTracking()
            .OrderByDescending(x => x.RealmType == "official")
            .ThenBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task<Realm> CreateRealmAsync(AdminRealmUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var realmId = request.RealmId.Trim();

        if (await _dbContext.Realms.AnyAsync(x => x.RealmId == realmId, cancellationToken))
        {
            throw new InvalidOperationException($"Realm '{realmId}' already exists.");
        }

        var entity = new Realm
        {
            RealmId = realmId,
            DisplayName = request.DisplayName.Trim(),
            Region = request.Region.Trim(),
            Kind = string.IsNullOrWhiteSpace(request.Kind) ? "realmcore" : request.Kind.Trim(),
            PublicBaseUrl = request.PublicBaseUrl.Trim(),
            Status = "offline",
            CurrentPlayers = 0,
            MaxPlayers = request.MaxPlayers,
            HealthyZoneCount = 0,
            IsListed = request.IsListed,
            IsOfficial = request.IsOfficial,
            RealmType = request.IsOfficial ? "official" : "community",
            Enabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Realms.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<Realm?> UpdateRealmAsync(string realmId, AdminRealmUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Realms.FirstOrDefaultAsync(x => x.RealmId == realmId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.DisplayName = request.DisplayName.Trim();
        entity.Region = request.Region.Trim();
        entity.Kind = string.IsNullOrWhiteSpace(request.Kind) ? entity.Kind : request.Kind.Trim();
        entity.PublicBaseUrl = request.PublicBaseUrl.Trim();
        entity.MaxPlayers = request.MaxPlayers;
        entity.IsListed = request.IsListed;
        entity.IsOfficial = request.IsOfficial;
        entity.RealmType = request.IsOfficial ? "official" : "community";
        entity.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpsertHeartbeatAsync(
        RealmHeartbeatRequest request,
        bool isOfficialCaller,
        bool isCommunityCaller,
        string callerServiceId,
        IReadOnlyCollection<string> allowedRealmIds,
        CancellationToken cancellationToken = default)
    {
        var normalizedRealmType = request.RealmType.Trim().ToLowerInvariant();
        if (normalizedRealmType is not ("official" or "community"))
        {
            throw new InvalidOperationException("RealmType must be either 'official' or 'community'.");
        }

        if (normalizedRealmType == "official" && !isOfficialCaller)
        {
            throw new UnauthorizedAccessException("Caller is not allowed to send official realm heartbeat.");
        }

        if (normalizedRealmType == "community" && !isCommunityCaller)
        {
            throw new UnauthorizedAccessException("Caller is not allowed to send community realm heartbeat.");
        }

        if (allowedRealmIds.Count == 0)
        {
            throw new UnauthorizedAccessException("Heartbeat client must have AllowedRealmIds configured.");
        }

        if (!allowedRealmIds.Contains(request.RealmId, StringComparer.Ordinal))
        {
            throw new UnauthorizedAccessException("Caller is not allowed to update this realm.");
        }

        var realm = await _dbContext.Realms
            .FirstOrDefaultAsync(x => x.RealmId == request.RealmId, cancellationToken);

        if (realm is null)
        {
            realm = new Realm
            {
                RealmId = request.RealmId,
                DisplayName = request.RealmName.Trim(),
                RealmType = normalizedRealmType,
                Region = request.Region.Trim(),
                Kind = "realmcore",
                Status = request.Status,
                CurrentPlayers = request.CurrentPlayers,
                MaxPlayers = request.MaxPlayers,
                HealthyZoneCount = request.HealthyZoneCount,
                PublicBaseUrl = request.PublicBaseUrl?.Trim() ?? string.Empty,
                Motd = string.IsNullOrWhiteSpace(request.Motd) ? null : request.Motd.Trim(),
                Version = string.IsNullOrWhiteSpace(request.Version) ? null : request.Version.Trim(),
                Modded = request.Modded,
                Enabled = true,
                IsListed = true,
                IsOfficial = normalizedRealmType == "official",
                LastHeartbeatAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.Realms.Add(realm);

            _logger.LogInformation(
                "Realm {RealmId} auto-created by heartbeat from service {ServiceId}. Type: {RealmType}",
                request.RealmId,
                callerServiceId,
                normalizedRealmType);
        }
        else
        {
            realm.Status = request.Status;
            realm.CurrentPlayers = request.CurrentPlayers;
            realm.MaxPlayers = request.MaxPlayers;
            realm.HealthyZoneCount = request.HealthyZoneCount;
            realm.LastHeartbeatAt = DateTime.UtcNow;
            realm.UpdatedAt = DateTime.UtcNow;

            // Csak opcionálisan frissítjük azokat a mezőket, amelyeknél ez üzletileg elfogadható.
            if (!string.IsNullOrWhiteSpace(request.PublicBaseUrl))
            {
                realm.PublicBaseUrl = request.PublicBaseUrl.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Motd))
            {
                realm.Motd = request.Motd.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Version))
            {
                realm.Version = request.Version.Trim();
            }

            realm.Modded = request.Modded;
            realm.IsOfficial = normalizedRealmType == "official";
            realm.RealmType = normalizedRealmType;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
