using GnosisAuthServer.Infrastructure;
using GnosisAuthServer.Models;
using GnosisAuthServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GnosisAuthServer.Controllers;

[ApiController]
[Route("api/internal/official-realms")]
public sealed class InternalOfficialRealmsController : ControllerBase
{
    private readonly IServiceRequestAuthenticator _serviceRequestAuthenticator;
    private readonly IRealmRegistryService _realmRegistryService;
    private readonly ILogger<InternalOfficialRealmsController> _logger;

    public InternalOfficialRealmsController(
        IServiceRequestAuthenticator serviceRequestAuthenticator,
        IRealmRegistryService realmRegistryService,
        ILogger<InternalOfficialRealmsController> logger)
    {
        _serviceRequestAuthenticator = serviceRequestAuthenticator;
        _realmRegistryService = realmRegistryService;
        _logger = logger;
    }

    [HttpPost("heartbeat")]
    [EnableRateLimiting("official-heartbeat")]
    public async Task<IActionResult> Heartbeat([FromBody] OfficialRealmHeartbeatRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!_serviceRequestAuthenticator.TryAuthenticate(Request, out var serviceContext, out var error))
        {
            return Unauthorized(new { error });
        }

        if (serviceContext is null || !serviceContext.Roles.Contains(ServiceRoles.OfficialRealmHeartbeatWrite, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        if (serviceContext.AllowedRealmIds.Count > 0 && !serviceContext.AllowedRealmIds.Contains(request.RealmId, StringComparer.Ordinal))
        {
            return Forbid();
        }

        var realm = await _realmRegistryService.UpsertOfficialHeartbeatAsync(request, cancellationToken);
        if (realm is null)
        {
            return NotFound(new { error = $"Realm '{request.RealmId}' is not registered. Create it through the admin API first." });
        }

        _logger.LogInformation(
            "Official realm heartbeat accepted for {RealmId} with status {Status} and {Zones} healthy zones.",
            realm.RealmId,
            realm.Status,
            realm.HealthyZoneCount);

        return Ok(new OfficialRealmHeartbeatResponse
        {
            RealmId = realm.RealmId,
            Status = realm.Status,
            UpdatedAtUtc = realm.UpdatedAtUtc
        });
    }
}
