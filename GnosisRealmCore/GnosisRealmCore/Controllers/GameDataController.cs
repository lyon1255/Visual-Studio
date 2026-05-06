using GnosisRealmCore.Infrastructure;
using GnosisRealmCore.Services;
using Microsoft.AspNetCore.Mvc;

namespace GnosisRealmCore.Controllers;

[ApiController]
[Route("api/gamedata")]
public sealed class GameDataController : ControllerBase
{
    private readonly IServiceRequestAuthenticator _serviceRequestAuthenticator;
    private readonly ILegacyNodeApiKeyValidator _legacyNodeApiKeyValidator;
    private readonly IGameDataCacheService _gameDataCacheService;
    private readonly IAdminRequestValidator _adminRequestValidator;

    public GameDataController(
        IServiceRequestAuthenticator serviceRequestAuthenticator,
        ILegacyNodeApiKeyValidator legacyNodeApiKeyValidator,
        IGameDataCacheService gameDataCacheService,
        IAdminRequestValidator adminRequestValidator)
    {
        _serviceRequestAuthenticator = serviceRequestAuthenticator;
        _legacyNodeApiKeyValidator = legacyNodeApiKeyValidator;
        _gameDataCacheService = gameDataCacheService;
        _adminRequestValidator = adminRequestValidator;
    }

    [HttpGet]
    public IActionResult Snapshot()
    {
        if (!AuthorizeServiceOrLegacy(out var error))
        {
            return Unauthorized(new { error });
        }

        return Ok(_gameDataCacheService.GetSnapshot());
    }

    [HttpGet("version")]
    public IActionResult Version()
    {
        if (!AuthorizeServiceOrLegacy(out var error))
        {
            return Unauthorized(new { error });
        }

        return Ok(_gameDataCacheService.GetVersion());
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        if (!_adminRequestValidator.TryAuthorize(Request, out var error))
        {
            return Unauthorized(new { error });
        }

        await _gameDataCacheService.RefreshAsync(cancellationToken);
        return Ok(_gameDataCacheService.GetVersion());
    }

    private bool AuthorizeServiceOrLegacy(out string error)
    {
        error = string.Empty;

        if (_legacyNodeApiKeyValidator.IsAuthorized(Request))
        {
            return true;
        }

        if (!_serviceRequestAuthenticator.TryAuthenticate(Request, out var context, out error))
        {
            return false;
        }

        return context is not null && context.Roles.Contains(ServiceRoles.RealmGameDataRead, StringComparer.OrdinalIgnoreCase);
    }
}
