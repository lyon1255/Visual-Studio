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
        var cutoff = DateTime.UtcNow.AddSeconds(-Math.Max(30, _options.HeartbeatTimeoutSeconds));

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
            .OrderByDescending(x => x.IsOfficial)
            .ThenBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Realm>> GetAllRealmsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Realms
            .AsNoTracking()
            .OrderByDescending(x => x.IsOfficial)
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

        var nowUtc = DateTime.UtcNow;

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
            Enabled = request.Enabled,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc
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
        entity.Enabled = request.Enabled;
        entity.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpsertHeartbeatAsync(
        RealmHeartbeatRequest request,
        string callerServiceId,
        IReadOnlyCollection<string> allowedRealmIds,
        CancellationToken cancellationToken = default)
    {
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
            throw new InvalidOperationException(
                $"Realm '{request.RealmId}' does not exist. Create it first through the admin API.");
        }

        var normalizedStatus = request.Status.Trim().ToLowerInvariant();
        if (normalizedStatus is not ("online" or "degraded" or "offline"))
        {
            throw new InvalidOperationException("Status must be 'online', 'degraded', or 'offline'.");
        }

        var nowUtc = DateTime.UtcNow;

        realm.Status = normalizedStatus;
        realm.CurrentPlayers = request.CurrentPlayers;
        realm.MaxPlayers = request.MaxPlayers;
        realm.HealthyZoneCount = request.HealthyZoneCount;
        realm.LastHeartbeatAt = nowUtc;
        realm.UpdatedAt = nowUtc;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Heartbeat accepted for realm {RealmId} from service {ServiceId}. Players: {CurrentPlayers}/{MaxPlayers}, HealthyZones: {HealthyZoneCount}",
            request.RealmId,
            callerServiceId,
            request.CurrentPlayers,
            request.MaxPlayers,
            request.HealthyZoneCount);
    }
}
