using GnosisAuthServer.Infrastructure;
using GnosisAuthServer.Models;
using GnosisAuthServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json;

namespace GnosisAuthServer.Controllers;

[ApiController]
[Route("api/internal/realms")]
public sealed class InternalRealmsController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IServiceRequestAuthenticator _serviceAuthenticator;
    private readonly IRealmRegistryService _realmRegistryService;

    public InternalRealmsController(
        IServiceRequestAuthenticator serviceAuthenticator,
        IRealmRegistryService realmRegistryService)
    {
        _serviceAuthenticator = serviceAuthenticator;
        _realmRegistryService = realmRegistryService;
    }

    [HttpPost("heartbeat")]
    [EnableRateLimiting("realm-heartbeat")]
    public async Task<IActionResult> Heartbeat(CancellationToken cancellationToken)
    {
        var authResult = await _serviceAuthenticator.AuthenticateAsync(Request, cancellationToken);
        if (!authResult.IsAuthenticated)
        {
            return Unauthorized(new { error = authResult.Error });
        }

        if (authResult.Context is null)
        {
            return Unauthorized(new { error = "Missing service auth context." });
        }

        RealmHeartbeatRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<RealmHeartbeatRequest>(Request.Body, JsonOptions, cancellationToken);
            Request.Body.Position = 0;
        }
        catch (JsonException ex)
        {
            return BadRequest(new { error = $"Invalid heartbeat payload: {ex.Message}" });
        }

        if (request is null)
        {
            return BadRequest(new { error = "Heartbeat payload is required." });
        }

        if (!TryValidateModel(request))
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            await _realmRegistryService.UpsertHeartbeatAsync(
                request,
                authResult.Context.ServiceId,
                authResult.Context.AllowedRealmIds,
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
