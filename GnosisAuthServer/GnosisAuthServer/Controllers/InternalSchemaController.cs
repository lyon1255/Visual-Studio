using GnosisAuthServer.Infrastructure;
using GnosisAuthServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GnosisAuthServer.Controllers;

[ApiController]
[Route("api/internal/schema")]
public sealed class InternalSchemaController : ControllerBase
{
    private readonly IServiceRequestAuthenticator _serviceRequestAuthenticator;
    private readonly ISchemaCatalogService _schemaCatalogService;

    public InternalSchemaController(
        IServiceRequestAuthenticator serviceRequestAuthenticator,
        ISchemaCatalogService schemaCatalogService)
    {
        _serviceRequestAuthenticator = serviceRequestAuthenticator;
        _schemaCatalogService = schemaCatalogService;
    }

    [HttpGet("manifest")]
    [EnableRateLimiting("realm-schema-read")]
    public async Task<IActionResult> GetManifest(CancellationToken cancellationToken)
    {
        var authResult = await _serviceRequestAuthenticator.AuthenticateAsync(Request, cancellationToken);
        if (!authResult.IsAuthenticated)
        {
            return Unauthorized(new { error = authResult.Error });
        }

        return Ok(await _schemaCatalogService.GetManifestAsync(cancellationToken));
    }

    [HttpGet("migrations/{migrationId}")]
    [EnableRateLimiting("realm-schema-read")]
    public async Task<IActionResult> GetMigration(string migrationId, CancellationToken cancellationToken)
    {
        var authResult = await _serviceRequestAuthenticator.AuthenticateAsync(Request, cancellationToken);
        if (!authResult.IsAuthenticated)
        {
            return Unauthorized(new { error = authResult.Error });
        }

        var migration = await _schemaCatalogService.GetMigrationAsync(migrationId, cancellationToken);
        if (migration is null)
        {
            return NotFound(new { error = "Schema migration was not found." });
        }

        return Ok(migration);
    }
}
