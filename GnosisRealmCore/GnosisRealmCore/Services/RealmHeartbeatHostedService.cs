using GnosisRealmCore.Data;
using GnosisRealmCore.Models;
using GnosisRealmCore.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GnosisRealmCore.Services;

public sealed class RealmHeartbeatHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAuthApiClient _authApiClient;
    private readonly RealmOptions _realmOptions;
    private readonly AuthApiOptions _authApiOptions;
    private readonly ILogger<RealmHeartbeatHostedService> _logger;

    private DateTime? _lastHealthyZoneSeenUtc;

    public RealmHeartbeatHostedService(
        IServiceScopeFactory scopeFactory,
        IAuthApiClient authApiClient,
        IOptions<RealmOptions> realmOptions,
        IOptions<AuthApiOptions> authApiOptions,
        ILogger<RealmHeartbeatHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _authApiClient = authApiClient;
        _realmOptions = realmOptions.Value;
        _authApiOptions = authApiOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_authApiOptions.BaseUrl) ||
            string.IsNullOrWhiteSpace(_authApiOptions.ServiceId) ||
            string.IsNullOrWhiteSpace(_authApiOptions.ServiceSecret))
        {
            _logger.LogWarning("AuthApi heartbeat is disabled because AuthApi configuration is incomplete.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendHeartbeatOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Failed to send realm heartbeat to AuthApi.");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(Math.Max(10, _authApiOptions.HeartbeatIntervalSeconds)),
                stoppingToken);
        }
    }

    private async Task SendHeartbeatOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RealmDbContext>();

        var nowUtc = DateTime.UtcNow;
        var healthyCutoff = nowUtc.AddSeconds(-Math.Max(30, _authApiOptions.HeartbeatIntervalSeconds * 2));

        var healthyZones = await dbContext.RealmZoneInstances
            .AsNoTracking()
            .Where(x =>
                x.LastHeartbeatUtc != null &&
                x.LastHeartbeatUtc >= healthyCutoff &&
                x.Status == "online")
            .ToListAsync(cancellationToken);

        var currentPlayers = healthyZones.Sum(x => x.CurrentPlayers);
        var healthyZoneCount = healthyZones.Count;

        if (healthyZoneCount > 0)
        {
            _lastHealthyZoneSeenUtc = nowUtc;
        }

        var status = ComputeRealmStatus(nowUtc, healthyZoneCount);

        var request = new RealmHeartbeatRequest
        {
            RealmId = _realmOptions.RealmId,
            Status = status,
            CurrentPlayers = currentPlayers,
            MaxPlayers = _realmOptions.MaxPlayers,
            HealthyZoneCount = healthyZoneCount
        };

        await _authApiClient.SendRealmHeartbeatAsync(request, cancellationToken);

        _logger.LogDebug(
            "Realm heartbeat sent. RealmId={RealmId}, Status={Status}, Players={CurrentPlayers}/{MaxPlayers}, HealthyZones={HealthyZoneCount}",
            request.RealmId,
            request.Status,
            request.CurrentPlayers,
            request.MaxPlayers,
            request.HealthyZoneCount);
    }

    private string ComputeRealmStatus(DateTime nowUtc, int healthyZoneCount)
    {
        if (healthyZoneCount > 0)
        {
            return "online";
        }

        if (_lastHealthyZoneSeenUtc is null)
        {
            return "degraded";
        }

        var secondsSinceHealthy = (nowUtc - _lastHealthyZoneSeenUtc.Value).TotalSeconds;

        if (secondsSinceHealthy >= Math.Max(30, _realmOptions.OfflineWhenNoHealthyZonesAfterSeconds))
        {
            return "offline";
        }

        return "degraded";
    }
}