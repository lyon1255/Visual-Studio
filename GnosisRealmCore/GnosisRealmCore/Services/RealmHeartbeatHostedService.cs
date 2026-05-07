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
                await using var scope = _scopeFactory.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<RealmDbContext>();

                var now = DateTime.UtcNow;
                var healthyCutoff = now.AddSeconds(-Math.Max(30, _authApiOptions.HeartbeatIntervalSeconds * 2));

                var healthyZones = await dbContext.RealmZoneInstances
                    .AsNoTracking()
                    .Where(x => x.LastHeartbeatUtc != null &&
                                x.LastHeartbeatUtc >= healthyCutoff &&
                                x.Status == "online")
                    .ToListAsync(stoppingToken);

                var currentPlayers = healthyZones.Sum(x => x.CurrentPlayers);
                var healthyZoneCount = healthyZones.Count;

                var realmType = string.Equals(_realmOptions.Kind, "official", StringComparison.OrdinalIgnoreCase)
                    ? "official"
                    : "community";

                var status = healthyZoneCount > 0 ? "online" : "idle";

                await _authApiClient.SendRealmHeartbeatAsync(new RealmHeartbeatRequest
                {
                    RealmId = _realmOptions.RealmId,
                    RealmName = _realmOptions.DisplayName,
                    RealmType = realmType,
                    Region = _realmOptions.Region,
                    Status = status,
                    CurrentPlayers = currentPlayers,
                    MaxPlayers = _realmOptions.MaxPlayers,
                    HealthyZoneCount = healthyZoneCount,
                    PublicBaseUrl = _realmOptions.PublicBaseUrl,
                    Motd = null,
                    Version = null,
                    Modded = realmType == "community"
                }, stoppingToken);
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
}
