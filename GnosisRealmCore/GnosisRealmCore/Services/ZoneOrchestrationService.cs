using GnosisRealmCore.Data;
using GnosisRealmCore.Models;
using GnosisRealmCore.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace GnosisRealmCore.Services;

public sealed class ZoneOrchestrationService : IZoneOrchestrationService
{
    private readonly RealmDbContext _dbContext;
    private readonly RealmOptions _realmOptions;
    private readonly ILogger<ZoneOrchestrationService> _logger;

    public ZoneOrchestrationService(RealmDbContext dbContext, IOptions<RealmOptions> realmOptions, ILogger<ZoneOrchestrationService> logger)
    {
        _dbContext = dbContext;
        _realmOptions = realmOptions.Value;
        _logger = logger;
    }

    public async Task<ZoneLookupResponse?> ResolveOrStartAsync(string zoneName, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.RealmZoneInstances
            .AsNoTracking()
            .Where(x => x.ZoneName == zoneName)
            .OrderByDescending(x => x.LastHeartbeatUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null && string.Equals(existing.Status, "online", StringComparison.OrdinalIgnoreCase))
        {
            return new ZoneLookupResponse
            {
                ZoneName = existing.ZoneName,
                Ip = existing.IpAddress,
                Port = existing.Port,
                Status = existing.Status
            };
        }

        var node = await _dbContext.RealmNodes
            .Where(x => x.Status == "online")
            .OrderBy(x => x.ActiveZones)
            .FirstOrDefaultAsync(cancellationToken);

        if (node is null)
        {
            _logger.LogWarning("Zone {ZoneName} cannot be started because there is no online node.", zoneName);
            return null;
        }

        var pending = await _dbContext.RealmNodeCommands
            .AnyAsync(x =>
                x.NodeId == node.NodeId &&
                x.Status == "pending" &&
                x.CommandType == "start-zone" &&
                x.PayloadJson.Contains($"\"zoneName\":\"{zoneName}\""),
                cancellationToken);

        if (!pending)
        {
            var command = new RealmNodeCommand
            {
                NodeId = node.NodeId,
                CommandType = "start-zone",
                PayloadJson = JsonSerializer.Serialize(new
                {
                    zoneName,
                    preferredPort = 0
                }),
                Status = "pending",
                CreatedAtUtc = DateTime.UtcNow
            };

            _dbContext.RealmNodeCommands.Add(command);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var until = DateTime.UtcNow.AddSeconds(_realmOptions.ZoneStartupPollSeconds);
        while (DateTime.UtcNow < until)
        {
            var started = await _dbContext.RealmZoneInstances
                .AsNoTracking()
                .Where(x => x.ZoneName == zoneName)
                .OrderByDescending(x => x.LastHeartbeatUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (started is not null && string.Equals(started.Status, "online", StringComparison.OrdinalIgnoreCase))
            {
                return new ZoneLookupResponse
                {
                    ZoneName = started.ZoneName,
                    Ip = started.IpAddress,
                    Port = started.Port,
                    Status = started.Status
                };
            }

            await Task.Delay(500, cancellationToken);
        }

        return null;
    }

    public async Task UpsertZoneHeartbeatAsync(ZoneHeartbeatRequest request, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.RealmZoneInstances
            .FirstOrDefaultAsync(x => x.ZoneName == request.ZoneName && x.NodeId == request.NodeId, cancellationToken);

        if (entity is null)
        {
            entity = new RealmZoneInstance
            {
                ZoneName = request.ZoneName,
                NodeId = request.NodeId,
                IpAddress = request.IpAddress,
                Port = request.Port,
                Status = request.Status,
                CurrentPlayers = request.CurrentPlayers,
                MaxPlayers = request.MaxPlayers,
                StartedAtUtc = DateTime.UtcNow,
                LastHeartbeatUtc = DateTime.UtcNow
            };

            _dbContext.RealmZoneInstances.Add(entity);
        }
        else
        {
            entity.IpAddress = request.IpAddress;
            entity.Port = request.Port;
            entity.Status = request.Status;
            entity.CurrentPlayers = request.CurrentPlayers;
            entity.MaxPlayers = request.MaxPlayers;
            entity.LastHeartbeatUtc = DateTime.UtcNow;
        }

        var node = await _dbContext.RealmNodes.FirstOrDefaultAsync(x => x.NodeId == request.NodeId, cancellationToken);
        if (node is not null)
        {
            node.ActiveZones = await _dbContext.RealmZoneInstances.CountAsync(x => x.NodeId == node.NodeId && x.Status == "online", cancellationToken);
            node.LastHeartbeatUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}