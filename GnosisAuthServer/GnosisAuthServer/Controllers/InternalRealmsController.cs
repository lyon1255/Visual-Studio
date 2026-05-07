using GnosisAuthServer.Infrastructure;
using GnosisAuthServer.Models;
using GnosisAuthServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GnosisAuthServer.Controllers;

[ApiController]
[Route("api/internal/realms")]
public sealed class InternalRealmsController : ControllerBase
{
    private readonly IServiceRequestAuthenticator _serviceAuthenticator;
    private readonly IRealmRegistryService _realmRegistryService;

    public InternalRealmsController(
        IServiceRequestAuthenticator serviceAuthenticator,
        IRealmRegistryService realmRegistryService)
    {
        _serviceAuthenticator = serviceAuthenticator;
        _realmRegistryService = realmRegistryService;
    }

    [HttpPost("heartbeat")]
    [EnableRateLimiting("realm-heartbeat")]
    public async Task<IActionResult> Heartbeat(
        [FromBody] RealmHeartbeatRequest request,
        CancellationToken cancellationToken)
    {
        if (!_serviceAuthenticator.TryAuthenticate(Request, out var context, out var error))
        {
            return Unauthorized(new { error });
        }

        if (context is null)
        {
            return Unauthorized(new { error = "Missing service auth context." });
        }

        try
        {
            await _realmRegistryService.UpsertHeartbeatAsync(
                request,
                context.ServiceId,
                context.AllowedRealmIds,
                cancellationToken);

            return Ok(new { status = "ok" });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}