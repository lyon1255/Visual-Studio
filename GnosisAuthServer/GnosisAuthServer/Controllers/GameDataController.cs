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
        var authResult = await _serviceRequestAuthenticator.AuthenticateAsync(Request, cancellationToken);
        if (!authResult.IsAuthenticated)
        {
            return Unauthorized(new { error = authResult.Error });
        }

        var version = await _gameDataService.GetCurrentVersionAsync(cancellationToken);
        return Ok(version);
    }

    [HttpGet("api/gamedata/snapshot")]
    [HttpGet("api/internal/gamedata/snapshot")]
    [EnableRateLimiting("realm-gamedata-read")]
    public async Task<IActionResult> GetSnapshot(CancellationToken cancellationToken)
    {
        var authResult = await _serviceRequestAuthenticator.AuthenticateAsync(Request, cancellationToken);
        if (!authResult.IsAuthenticated)
        {
            return Unauthorized(new { error = authResult.Error });
        }

        var snapshot = await _gameDataService.GetCurrentSnapshotAsync(cancellationToken);
        return Ok(snapshot);
    }

    [HttpGet("api/gamedata/prefabs")]
    [HttpGet("api/internal/gamedata/prefabs")]
    [EnableRateLimiting("realm-gamedata-read")]
    public async Task<IActionResult> GetPrefabRegistry(CancellationToken cancellationToken)
    {
        var authResult = await _serviceRequestAuthenticator.AuthenticateAsync(Request, cancellationToken);
        if (!authResult.IsAuthenticated)
        {
            return Unauthorized(new { error = authResult.Error });
        }

        var version = await _gameDataService.GetCurrentVersionAsync(cancellationToken);

        return Ok(new GlobalPrefabRegistryResponse
        {
            VersionNumber = version.VersionNumber,
            VersionTag = version.VersionTag,
            ContentHash = version.ContentHash,
            PublishedAtUtc = version.PublishedAtUtc
        });
    }

    [HttpGet("api/admin/gamedata/snapshot")]
    public async Task<IActionResult> GetSnapshotForAdmin(CancellationToken cancellationToken)
    {
        var authResult = await _adminRequestValidator.AuthorizeAsync(Request, cancellationToken);
        if (!authResult.IsAuthorized)
        {
            return Unauthorized(new { error = authResult.Error });
        }

        return Ok(await _gameDataService.GetCurrentSnapshotAsync(cancellationToken));
    }

    [HttpPost("api/admin/gamedata/replace")]
    [EnableRateLimiting("admin-write")]
    public async Task<IActionResult> ReplaceSnapshot([FromBody] ReplaceGlobalGameDataRequest request, CancellationToken cancellationToken)
    {
        var authResult = await _adminRequestValidator.AuthorizeAsync(Request, cancellationToken);
        if (!authResult.IsAuthorized)
        {
            return Unauthorized(new { error = authResult.Error });
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
