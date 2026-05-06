using GnosisRealmCore.Models;

namespace GnosisRealmCore.Services;

public interface IZoneOrchestrationService
{
    Task<ZoneLookupResponse?> ResolveOrStartAsync(string zoneName, CancellationToken cancellationToken);
    Task UpsertZoneHeartbeatAsync(ZoneHeartbeatRequest request, CancellationToken cancellationToken);
}
