using GnosisRealmCore.Infrastructure;
using GnosisRealmCore.Models;
using GnosisRealmCore.Services;
using Microsoft.AspNetCore.Mvc;

namespace GnosisRealmCore.Controllers;

[ApiController]
[Route("api/heartbeat")]
public sealed class HeartbeatController : ControllerBase
{
    private readonly IServiceRequestAuthenticator _serviceRequestAuthenticator;
    private readonly ILegacyNodeApiKeyValidator _legacyNodeApiKeyValidator;
    private readonly IZoneOrchestrationService _zoneOrchestrationService;

    public HeartbeatController(
        IServiceRequestAuthenticator serviceRequestAuthenticator,
        ILegacyNodeApiKeyValidator legacyNodeApiKeyValidator,
        IZoneOrchestrationService zoneOrchestrationService)
    {
        _serviceRequestAuthenticator = serviceRequestAuthenticator;
        _legacyNodeApiKeyValidator = legacyNodeApiKeyValidator;
        _zoneOrchestrationService = zoneOrchestrationService;
    }

    [HttpPost("node")]
    public async Task<IActionResult> ZoneHeartbeat([FromBody] ZoneHeartbeatRequest request, CancellationToken cancellationToken)
    {
        var authorizedByLegacy = _legacyNodeApiKeyValidator.IsAuthorized(Request);
        var authorizedByService = _serviceRequestAuthenticator.TryAuthenticate(Request, out var serviceContext, out var error);

        if (!authorizedByLegacy && !authorizedByService)
        {
            return Unauthorized(new { error = string.IsNullOrWhiteSpace(error) ? "Missing heartbeat authorization." : error });
        }

        if (authorizedByService && serviceContext is not null &&
            !serviceContext.Roles.Contains(ServiceRoles.ZoneHeartbeatWrite, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        await _zoneOrchestrationService.UpsertZoneHeartbeatAsync(request, cancellationToken);
        return Ok(new { accepted = true });
    }
}
