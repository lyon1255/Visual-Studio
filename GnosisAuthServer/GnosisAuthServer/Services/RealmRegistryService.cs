using GnosisAuthServer.Data;
using GnosisAuthServer.Models;
using Microsoft.EntityFrameworkCore;

namespace GnosisAuthServer.Services;

public sealed class RealmRegistryService(
    AuthDbContext dbContext,
    ILogger<RealmRegistryService> logger) : IRealmRegistryService
{
    private readonly AuthDbContext _dbContext = dbContext;
    private readonly ILogger<RealmRegistryService> _logger = logger;

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

        if (allowedRealmIds.Count > 0 && !allowedRealmIds.Contains(request.RealmId, StringComparer.Ordinal))
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
                DisplayName = request.RealmName,
                RealmType = normalizedRealmType,
                Region = request.Region,
                Status = request.Status,
                CurrentPlayers = request.CurrentPlayers,
                MaxPlayers = request.MaxPlayers,
                HealthyZoneCount = request.HealthyZoneCount,
                PublicBaseUrl = request.PublicBaseUrl ?? string.Empty,
                Motd = request.Motd ?? string.Empty,
                Version = request.Version,
                Modded = request.Modded,
                Enabled = true,
                LastHeartbeatAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.Realms.Add(realm);

            _logger.LogInformation(
                "Realm {RealmId} was auto-created by heartbeat from service {ServiceId}. Type: {RealmType}",
                request.RealmId,
                callerServiceId,
                normalizedRealmType);
        }
        else
        {
            realm.DisplayName = request.RealmName;
            realm.RealmType = normalizedRealmType;
            realm.Region = request.Region;
            realm.Status = request.Status;
            realm.CurrentPlayers = request.CurrentPlayers;
            realm.MaxPlayers = request.MaxPlayers;
            realm.HealthyZoneCount = request.HealthyZoneCount;
            realm.PublicBaseUrl = request.PublicBaseUrl ?? string.Empty;
            realm.Motd = request.Motd ?? string.Empty;
            realm.Version = request.Version;
            realm.Modded = request.Modded;
            realm.LastHeartbeatAt = DateTime.UtcNow;
            realm.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}