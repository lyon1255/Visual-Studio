using GnosisAuthServer.Data;
using Microsoft.AspNetCore.Mvc;

namespace GnosisAuthServer.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    private readonly AuthDbContext _dbContext;

    public HealthController(AuthDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("live")]
    public IActionResult Live() => Ok(new { status = "ok" });

    [HttpGet("ready")]
    public async Task<IActionResult> Ready(CancellationToken cancellationToken)
    {
        var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? Ok(new { status = "ready" })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "degraded" });
    }
}
