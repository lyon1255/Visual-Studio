using GnosisAuthServer.Infrastructure;
using GnosisAuthServer.Models;
using GnosisAuthServer.Options;
using GnosisAuthServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace GnosisAuthServer.Controllers;

[ApiController]
public sealed class GameDataController : ControllerBase
{
    private readonly IServiceRequestAuthenticator _serviceRequestAuthenticator;
    private readonly IAdminRequestValidator _adminRequestValidator;
    private readonly IGameDataService _gameDataService;
    private readonly GameDataOptions _gameDataOptions;

    public GameDataController(
        IServiceRequestAuthenticator serviceRequestAuthenticator,
        IAdminRequestValidator adminRequestValidator,
        IGameDataService gameDataService,
        IOptions<GameDataOptions> gameDataOptions)
    {
        _serviceRequestAuthenticator = serviceRequestAuthenticator;
        _adminRequestValidator = adminRequestValidator;
        _gameDataService = gameDataService;
        _gameDataOptions = gameDataOptions.Value;
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

        if (ApplyConditionalEtag(version.ContentHash))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        SetResponseEtag(version.ContentHash);
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

        var version = await _gameDataService.GetCurrentVersionAsync(cancellationToken);
        if (ApplyConditionalEtag(version.ContentHash))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        var snapshot = await _gameDataService.GetCurrentSnapshotAsync(cancellationToken);
        SetResponseEtag(snapshot.ContentHash);
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

        if (ApplyConditionalEtag(version.ContentHash))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        SetResponseEtag(version.ContentHash);
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

        var version = await _gameDataService.GetCurrentVersionAsync(cancellationToken);
        if (ApplyConditionalEtag(version.ContentHash))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        var snapshot = await _gameDataService.GetCurrentSnapshotAsync(cancellationToken);
        SetResponseEtag(snapshot.ContentHash);
        return Ok(snapshot);
    }

    [HttpPost("api/admin/gamedata/replace")]
    [EnableRateLimiting("admin-write")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> ReplaceSnapshot([FromBody] ReplaceGlobalGameDataRequest request, CancellationToken cancellationToken)
    {
        var authResult = await _adminRequestValidator.AuthorizeAsync(Request, cancellationToken);
        if (!authResult.IsAuthorized)
        {
            return Unauthorized(new { error = authResult.Error });
        }

        if (Request.ContentLength is long contentLength && contentLength > _gameDataOptions.ReplaceRequestMaxBodyBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new { error = "GameData replace payload is too large." });
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var snapshot = await _gameDataService.ReplaceSnapshotAsync(request, cancellationToken);
            SetResponseEtag(snapshot.ContentHash);
            return Ok(snapshot);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private bool ApplyConditionalEtag(string contentHash)
    {
        var requestEtag = Request.Headers.IfNoneMatch.ToString();
        var currentEtag = BuildEtag(contentHash);
        return !string.IsNullOrWhiteSpace(requestEtag)
            && string.Equals(requestEtag.Trim(), currentEtag, StringComparison.Ordinal);
    }

    private void SetResponseEtag(string contentHash)
    {
        Response.Headers.ETag = BuildEtag(contentHash);
        Response.Headers.CacheControl = "private, max-age=0, must-revalidate";
    }

    private static string BuildEtag(string contentHash)
        => $"\"{contentHash}\"";
}
