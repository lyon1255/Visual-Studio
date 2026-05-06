using GnosisAuthServer.Infrastructure;
using GnosisAuthServer.Models;
using GnosisAuthServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GnosisAuthServer.Controllers;

[ApiController]
public sealed class GameDataController : ControllerBase
{
    private readonly IServiceRequestAuthenticator _serviceRequestAuthenticator;
    private readonly IAdminRequestValidator _adminRequestValidator;
    private readonly IGameDataService _gameDataService;

    public GameDataController(
        IServiceRequestAuthenticator serviceRequestAuthenticator,
        IAdminRequestValidator adminRequestValidator,
        IGameDataService gameDataService)
    {
        _serviceRequestAuthenticator = serviceRequestAuthenticator;
        _adminRequestValidator = adminRequestValidator;
        _gameDataService = gameDataService;
    }

    [HttpGet("api/gamedata/version")]
    [HttpGet("api/internal/gamedata/version")]
    [EnableRateLimiting("realm-gamedata-read")]
    public async Task<IActionResult> GetVersion(CancellationToken cancellationToken)
    {
        if (!_serviceRequestAuthenticator.TryAuthenticate(Request, out var context, out var error))
        {
            return Unauthorized(new { error });
        }

        if (context is null || !context.Roles.Contains(ServiceRoles.RealmGameDataRead, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var version = await _gameDataService.GetCurrentVersionAsync(cancellationToken);
        return Ok(version);
    }

    [HttpGet("api/gamedata/snapshot")]
    [HttpGet("api/internal/gamedata/snapshot")]
    [EnableRateLimiting("realm-gamedata-read")]
    public async Task<IActionResult> GetSnapshot(CancellationToken cancellationToken)
    {
        if (!_serviceRequestAuthenticator.TryAuthenticate(Request, out var context, out var error))
        {
            return Unauthorized(new { error });
        }

        if (context is null || !context.Roles.Contains(ServiceRoles.RealmGameDataRead, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var snapshot = await _gameDataService.GetCurrentSnapshotAsync(cancellationToken);
        return Ok(snapshot);
    }

    [HttpGet("api/gamedata/prefabs")]
    [HttpGet("api/internal/gamedata/prefabs")]
    [EnableRateLimiting("realm-gamedata-read")]
    public async Task<IActionResult> GetPrefabRegistry(CancellationToken cancellationToken)
    {
        if (!_serviceRequestAuthenticator.TryAuthenticate(Request, out var context, out var error))
        {
            return Unauthorized(new { error });
        }

        if (context is null || !context.Roles.Contains(ServiceRoles.RealmGameDataRead, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var version = await _gameDataService.GetCurrentVersionAsync(cancellationToken);

        return Ok(new GlobalPrefabRegistryResponse
        {
            VersionNumber = version.VersionNumber,
            VersionTag = version.VersionTag,
            ContentHash = version.ContentHash,
            PublishedAtUtc = version.PublishedAtUtc,
        });
    }

    [HttpGet("api/admin/gamedata/snapshot")]
    public async Task<IActionResult> GetSnapshotForAdmin(CancellationToken cancellationToken)
    {
        if (!_adminRequestValidator.TryAuthorize(Request, out var error))
        {
            return Unauthorized(new { error });
        }

        return Ok(await _gameDataService.GetCurrentSnapshotAsync(cancellationToken));
    }

    [HttpPost("api/admin/gamedata/replace")]
    [EnableRateLimiting("admin-write")]
    public async Task<IActionResult> ReplaceSnapshot([FromBody] ReplaceGlobalGameDataRequest request, CancellationToken cancellationToken)
    {
        if (!_adminRequestValidator.TryAuthorize(Request, out var error))
        {
            return Unauthorized(new { error });
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var snapshot = await _gameDataService.ReplaceSnapshotAsync(request, cancellationToken);
            return Ok(snapshot);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
