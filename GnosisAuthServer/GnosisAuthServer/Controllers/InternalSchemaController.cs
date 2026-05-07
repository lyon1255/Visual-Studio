using GnosisAuthServer.Infrastructure;
using GnosisAuthServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GnosisAuthServer.Controllers;

[ApiController]
[Route("api/internal/schema")]
public sealed class InternalSchemaController : ControllerBase
{
    private readonly IServiceRequestAuthenticator _serviceAuthenticator;
    private readonly ISchemaCatalogService _schemaCatalogService;

    public InternalSchemaController(
        IServiceRequestAuthenticator serviceAuthenticator,
        ISchemaCatalogService schemaCatalogService)
    {
        _serviceAuthenticator = serviceAuthenticator;
        _schemaCatalogService = schemaCatalogService;
    }

    [HttpGet("manifest")]
    [EnableRateLimiting("realm-schema-read")]
    public async Task<IActionResult> GetManifest(CancellationToken cancellationToken)
    {
        if (!_serviceAuthenticator.TryAuthenticate(Request, out _, out var error))
        {
            return Unauthorized(new { error });
        }

        var manifest = await _schemaCatalogService.GetManifestAsync(cancellationToken);
        return Ok(manifest);
    }

    [HttpGet("migrations/{migrationId}")]
    [EnableRateLimiting("realm-schema-read")]
    public async Task<IActionResult> GetMigration(string migrationId, CancellationToken cancellationToken)
    {
        if (!_serviceAuthenticator.TryAuthenticate(Request, out _, out var error))
        {
            return Unauthorized(new { error });
        }

        var migration = await _schemaCatalogService.GetMigrationAsync(migrationId, cancellationToken);
        if (migration is null)
        {
            return NotFound(new { error = "Migration was not found." });
        }

        return Ok(migration);
    }
}