using GnosisAuthServer.Infrastructure;
using GnosisAuthServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace GnosisAuthServer.Controllers;

[ApiController]
[Route("api/internal/schema")]
public sealed class InternalSchemaController(
    IServiceRequestAuthenticator serviceAuthenticator,
    ISchemaCatalogService schemaCatalogService) : ControllerBase
{
    private readonly IServiceRequestAuthenticator _serviceAuthenticator = serviceAuthenticator;
    private readonly ISchemaCatalogService _schemaCatalogService = schemaCatalogService;


    [HttpGet("manifest")]
    public async Task<IActionResult> GetManifest(CancellationToken cancellationToken)
    {
        if (!TryAuthorize("realm-schema.read", out var denied))
        {
            return denied!;
        }

        var manifest = await _schemaCatalogService.GetManifestAsync(cancellationToken);
        return Ok(manifest);
    }

    [HttpGet("migrations/{migrationId}")]
    public async Task<IActionResult> GetMigration(string migrationId, CancellationToken cancellationToken)
    {
        if (!TryAuthorize("realm-schema.read", out var denied))
        {
            return denied!;
        }

        var migration = await _schemaCatalogService.GetMigrationAsync(migrationId, cancellationToken);
        if (migration is null)
        {
            return NotFound(new { error = "Migration was not found." });
        }

        return Ok(migration);
    }

    private bool TryAuthorize(string requiredRole, out IActionResult? denied)
    {
        denied = null;

        if (!_serviceAuthenticator.TryAuthenticate(Request, out var context, out var error))
        {
            denied = Unauthorized(new { error });
            return false;
        }

        if (context is null || context.Roles is null || !context.Roles.Contains(requiredRole, StringComparer.Ordinal))
        {
            denied = Forbid();
            return false;
        }

        return true;
    }
}