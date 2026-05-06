using GnosisRealmCore.Infrastructure;
using GnosisRealmCore.Services;
using Microsoft.AspNetCore.Mvc;

namespace GnosisRealmCore.Controllers;

[ApiController]
[Route("api/zone")]
public sealed class ZoneController : ControllerBase
{
    private readonly IZoneOrchestrationService _zoneOrchestrationService;
    private readonly IJwtTokenValidator _jwtTokenValidator;
    private readonly IServiceRequestAuthenticator _serviceRequestAuthenticator;
    private readonly ILegacyNodeApiKeyValidator _legacyNodeApiKeyValidator;

    public ZoneController(
        IZoneOrchestrationService zoneOrchestrationService,
        IJwtTokenValidator jwtTokenValidator,
        IServiceRequestAuthenticator serviceRequestAuthenticator,
        ILegacyNodeApiKeyValidator legacyNodeApiKeyValidator)
    {
        _zoneOrchestrationService = zoneOrchestrationService;
        _jwtTokenValidator = jwtTokenValidator;
        _serviceRequestAuthenticator = serviceRequestAuthenticator;
        _legacyNodeApiKeyValidator = legacyNodeApiKeyValidator;
    }

    [HttpGet("find/{zoneName}")]
    public async Task<IActionResult> Find(string zoneName, CancellationToken cancellationToken)
    {
        var isPlayerAuthorized = _jwtTokenValidator.TryValidate(Request, out _, out _);
        var isLegacyAuthorized = _legacyNodeApiKeyValidator.IsAuthorized(Request);
        var isServiceAuthorized = _serviceRequestAuthenticator.TryAuthenticate(Request, out _, out _);

        if (!isPlayerAuthorized && !isLegacyAuthorized && !isServiceAuthorized)
        {
            return Unauthorized(new { error = "Missing authorization." });
        }

        var result = await _zoneOrchestrationService.ResolveOrStartAsync(zoneName, cancellationToken);
        if (result is null)
        {
            return StatusCode(503, new { error = "Zone is not ready." });
        }

        return Ok(new
        {
            message = "Zone is ready.",
            ip = result.Ip,
            port = result.Port,
            status = result.Status
        });
    }
}
