using GnosisAuthServer.Infrastructure;
using GnosisAuthServer.Models;
using GnosisAuthServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace GnosisAuthServer.Controllers;

[ApiController]
[Route("api/internal/realms")]
public sealed class InternalRealmsController(
    IServiceRequestAuthenticator serviceAuthenticator,
    IRealmRegistryService realmRegistryService) : ControllerBase
{
    private readonly IServiceRequestAuthenticator _serviceAuthenticator = serviceAuthenticator;
    private readonly IRealmRegistryService _realmRegistryService = realmRegistryService;

    [HttpPost("heartbeat")]
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

        var isOfficialCaller = context.Roles.Contains(ServiceRoles.OfficialRealmHeartbeatWrite, StringComparer.Ordinal);
        var isCommunityCaller = context.Roles.Contains(ServiceRoles.CommunityRealmHeartbeatWrite, StringComparer.Ordinal);

        if (!isOfficialCaller && !isCommunityCaller)
        {
            return Forbid();
        }

        try
        {
            await _realmRegistryService.UpsertHeartbeatAsync(
                request,
                isOfficialCaller,
                isCommunityCaller,
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