using GnosisRealmCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GnosisRealmCore.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ZoneController : ControllerBase
    {
        private readonly ServerOrchestratorService _orchestrator;
        private readonly IConfiguration _configuration;
        private readonly LoggerService _logger;

        public ZoneController(ServerOrchestratorService orchestrator, IConfiguration configuration, LoggerService logger)
        {
            _orchestrator = orchestrator;
            _configuration = configuration;
            _logger = logger;
        }

        // GET: /api/zone/find/{zoneName}
        [HttpGet("find/{zoneName}")]
        public async Task<IActionResult> GetZoneConnection(string zoneName)
        {
            try
            {
                // JAVÍTVA: Csak a zóna nevét adjuk át, az IP-t a NodeAgent küldi be a Redisbe!
                string? address = await _orchestrator.GetOrStartZoneAsync(zoneName);

                if (string.IsNullOrEmpty(address))
                    return StatusCode(500, new { error = "Failed to start or find the zone. Servers might be full or NodeAgent is not responding." });

                var parts = address.Split(':');
                return Ok(new
                {
                    message = "Zone is ready.",
                    ip = parts[0],
                    port = int.Parse(parts[1])
                });
            }
            catch (Exception ex)
            {
                _logger.LogSync($"Error processing zone request: {ex.Message}", GnosisLogLevel.Error);
                return StatusCode(500, new { error = "Internal server error." });
            }
        }
    }
}