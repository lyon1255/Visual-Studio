using GnosisRealmCore.Infrastructure;
using GnosisRealmCore.Models;
using GnosisRealmCore.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GnosisRealmCore.Controllers;

[ApiController]
[Route("api/character")]
public sealed class CharacterController : ControllerBase
{
    private readonly IJwtTokenValidator _jwtTokenValidator;
    private readonly IServiceRequestAuthenticator _serviceRequestAuthenticator;
    private readonly ILegacyNodeApiKeyValidator _legacyNodeApiKeyValidator;
    private readonly ICharacterService _characterService;

    public CharacterController(
        IJwtTokenValidator jwtTokenValidator,
        IServiceRequestAuthenticator serviceRequestAuthenticator,
        ILegacyNodeApiKeyValidator legacyNodeApiKeyValidator,
        ICharacterService characterService)
    {
        _jwtTokenValidator = jwtTokenValidator;
        _serviceRequestAuthenticator = serviceRequestAuthenticator;
        _legacyNodeApiKeyValidator = legacyNodeApiKeyValidator;
        _characterService = characterService;
    }

    [HttpGet("list")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!_jwtTokenValidator.TryValidate(Request, out var principal, out var error))
        {
            return Unauthorized(new { error });
        }

        var steamId = principal!.FindFirst("sub")?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(steamId))
        {
            return Unauthorized(new { error = "Missing steam subject." });
        }

        return Ok(await _characterService.GetCharacterListAsync(steamId, cancellationToken));
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CreateCharacterRequest request, CancellationToken cancellationToken)
    {
        if (!_jwtTokenValidator.TryValidate(Request, out var principal, out var error))
        {
            return Unauthorized(new { error });
        }

        var steamId = principal!.FindFirst("sub")?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(steamId))
        {
            return Unauthorized(new { error = "Missing steam subject." });
        }

        try
        {
            return Ok(await _characterService.CreateCharacterAsync(steamId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpGet("{characterId:int}/details")]
    public async Task<IActionResult> Details(int characterId, CancellationToken cancellationToken)
    {
        if (!_jwtTokenValidator.TryValidate(Request, out var principal, out var error))
        {
            return Unauthorized(new { error });
        }

        var steamId = principal!.FindFirst("sub")?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(steamId))
        {
            return Unauthorized(new { error = "Missing steam subject." });
        }

        var character = await _characterService.GetCharacterDetailsAsync(steamId, characterId, cancellationToken);
        if (character is null)
        {
            return NotFound(new { error = "Character was not found." });
        }

        return Ok(character);
    }

    [HttpDelete("{characterId:int}")]
    public async Task<IActionResult> Delete(int characterId, CancellationToken cancellationToken)
    {
        if (!_jwtTokenValidator.TryValidate(Request, out var principal, out var error))
        {
            return Unauthorized(new { error });
        }

        var steamId = principal!.FindFirst("sub")?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(steamId))
        {
            return Unauthorized(new { error = "Missing steam subject." });
        }

        var deleted = await _characterService.DeleteCharacterAsync(steamId, characterId, cancellationToken);
        return deleted ? Ok(new { deleted = true }) : NotFound(new { error = "Character was not found." });
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] SaveCharacterRequest request, CancellationToken cancellationToken)
    {
        var authorizedByLegacy = _legacyNodeApiKeyValidator.IsAuthorized(Request);
        var authorizedByService = _serviceRequestAuthenticator.TryAuthenticate(Request, out var serviceContext, out var serviceError);

        if (!authorizedByLegacy && !authorizedByService)
        {
            return Unauthorized(new { error = serviceError == string.Empty ? "Missing node authorization." : serviceError });
        }

        if (!authorizedByService && serviceContext is null)
        {
            return Forbid();
        }

        try
        {
            await _characterService.SaveCharacterFromServerAsync(request, cancellationToken);
            return Ok(new { saved = true });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
