using GnosisAuthServer.Infrastructure;
using GnosisAuthServer.Models;
using GnosisAuthServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GnosisAuthServer.Controllers;

[ApiController]
[Route("api/admin/realms")]
public sealed class AdminRealmsController : ControllerBase
{
    private readonly IAdminRequestValidator _adminRequestValidator;
    private readonly IRealmRegistryService _realmRegistryService;

    public AdminRealmsController(IAdminRequestValidator adminRequestValidator, IRealmRegistryService realmRegistryService)
    {
        _adminRequestValidator = adminRequestValidator;
        _realmRegistryService = realmRegistryService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        if (!_adminRequestValidator.TryAuthorize(Request, out var error))
        {
            return Unauthorized(new { error });
        }

        var realms = await _realmRegistryService.GetAllRealmsAsync(cancellationToken);
        return Ok(realms);
    }

    [HttpPost]
    [EnableRateLimiting("admin-write")]
    public async Task<IActionResult> Create([FromBody] AdminRealmUpsertRequest request, CancellationToken cancellationToken)
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
            var created = await _realmRegistryService.CreateRealmAsync(request, cancellationToken);
            return Ok(created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPut("{realmId}")]
    [EnableRateLimiting("admin-write")]
    public async Task<IActionResult> Update(string realmId, [FromBody] AdminRealmUpsertRequest request, CancellationToken cancellationToken)
    {
        if (!_adminRequestValidator.TryAuthorize(Request, out var error))
        {
            return Unauthorized(new { error });
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var updated = await _realmRegistryService.UpdateRealmAsync(realmId, request, cancellationToken);
        return updated is null ? NotFound(new { error = $"Realm '{realmId}' was not found." }) : Ok(updated);
    }
}
