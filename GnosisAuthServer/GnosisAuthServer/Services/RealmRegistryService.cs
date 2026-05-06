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

    public RealmRegistryService(AuthDbContext dbContext, IOptions<RealmRegistryOptions> options)
    {
        _dbContext = dbContext;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<Realm>> GetPublicRealmsAsync(CancellationToken cancellationToken)
    {
        var staleBefore = DateTime.UtcNow.AddSeconds(-_options.HeartbeatTimeoutSeconds);

        var query = _dbContext.Realms.AsNoTracking().Where(x => x.IsListed);
        if (_options.HideUnhealthyRealms)
        {
            query = query.Where(x => x.LastHeartbeatAtUtc != null && x.LastHeartbeatAtUtc >= staleBefore && x.Status == "online");
        }

        return await query.OrderBy(x => x.Kind).ThenBy(x => x.Region).ThenBy(x => x.DisplayName).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Realm>> GetAllRealmsAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Realms.AsNoTracking().OrderBy(x => x.Kind).ThenBy(x => x.Region).ThenBy(x => x.DisplayName).ToListAsync(cancellationToken);
    }

    public async Task<Realm?> GetRealmAsync(string realmId, CancellationToken cancellationToken)
    {
        return await _dbContext.Realms.AsNoTracking().FirstOrDefaultAsync(x => x.RealmId == realmId, cancellationToken);
    }

    public async Task<Realm> CreateRealmAsync(AdminRealmUpsertRequest request, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.Realms.FirstOrDefaultAsync(x => x.RealmId == request.RealmId, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException($"Realm '{request.RealmId}' already exists.");
        }

        var entity = new Realm
        {
            RealmId = request.RealmId.Trim(),
            DisplayName = request.DisplayName.Trim(),
            Region = request.Region.Trim(),
            Kind = request.Kind.Trim().ToLowerInvariant(),
            PublicBaseUrl = request.PublicBaseUrl.Trim(),
            MaxPlayers = Math.Max(0, request.MaxPlayers),
            IsListed = request.IsListed,
            IsOfficial = request.IsOfficial,
            Status = "offline",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Realms.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<Realm?> UpdateRealmAsync(string realmId, AdminRealmUpsertRequest request, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Realms.FirstOrDefaultAsync(x => x.RealmId == realmId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.DisplayName = request.DisplayName.Trim();
        entity.Region = request.Region.Trim();
        entity.Kind = request.Kind.Trim().ToLowerInvariant();
        entity.PublicBaseUrl = request.PublicBaseUrl.Trim();
        entity.MaxPlayers = Math.Max(0, request.MaxPlayers);
        entity.IsListed = request.IsListed;
        entity.IsOfficial = request.IsOfficial;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<Realm?> UpsertOfficialHeartbeatAsync(OfficialRealmHeartbeatRequest request, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Realms.FirstOrDefaultAsync(x => x.RealmId == request.RealmId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        if (!entity.IsOfficial)
        {
            throw new InvalidOperationException($"Realm '{request.RealmId}' is not marked as official.");
        }

        entity.DisplayName = request.DisplayName.Trim();
        entity.Region = request.Region.Trim();
        entity.PublicBaseUrl = request.PublicBaseUrl.Trim();
        entity.Status = request.Status.Trim().ToLowerInvariant();
        entity.CurrentPlayers = Math.Max(0, request.CurrentPlayers);
        entity.MaxPlayers = Math.Max(0, request.MaxPlayers);
        entity.HealthyZoneCount = Math.Max(0, request.HealthyZoneCount);
        entity.LastHeartbeatAtUtc = DateTime.UtcNow;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }
}
