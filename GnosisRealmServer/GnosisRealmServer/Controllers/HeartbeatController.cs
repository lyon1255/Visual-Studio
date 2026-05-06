using GnosisRealmCore.Data;
using GnosisRealmCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using System.Text.Json;

namespace GnosisRealmCore.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HeartbeatController : ControllerBase
    {
        private readonly RealmDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IConnectionMultiplexer _redis;
        private readonly LoggerService _logger;

        public HeartbeatController(RealmDbContext context, IConfiguration configuration, IConnectionMultiplexer redis, LoggerService logger)
        {
            _context = context;
            _configuration = configuration;
            _redis = redis;
            _logger = logger;
        }

        private bool IsServerKeyValid()
        {
            if (!Request.Headers.TryGetValue("X-Server-Admin-Key", out var apiKey)) return false;
            var serverKey = _configuration.GetValue<string>("ServerSettings:ApiKey");
            return !string.IsNullOrEmpty(serverKey) && serverKey == apiKey.ToString();
        }

        public class NodeHeartbeatPayload
        {
            public string ZoneName { get; set; } = string.Empty;
            public string IpAddress { get; set; } = string.Empty;
            public int Port { get; set; }
            public int CurrentPlayers { get; set; }
            public int MaxPlayers { get; set; }
            public int Status { get; set; } // 0 = Indul, 1 = Online, 2 = Leáll
        }

        // POST: /api/heartbeat/node
        // Ezt hívják a Unity GameServerek (FishNet) 60 másodpercenként
        [HttpPost("node")]
        [AllowAnonymous]
        public async Task<IActionResult> NodeHeartbeat([FromBody] NodeHeartbeatPayload payload)
        {
            if (!IsServerKeyValid())
            {
                _logger.LogSync($"Node Heartbeat rejected: Invalid API key! (Zone: {payload.ZoneName})", GnosisLogLevel.Warning);
                return Unauthorized(new { error = "Invalid API Key." });
            }

            try
            {
                var db = _redis.GetDatabase();

                // Egyedi azonosító a Redis-ben, pl: "Gnosis:ActiveZones:Forest"
                string redisKey = $"Gnosis:ActiveZones:{payload.ZoneName}";

                var zoneData = new
                {
                    ZoneName = payload.ZoneName,
                    IpAddress = payload.IpAddress,
                    Port = payload.Port,
                    CurrentPlayers = payload.CurrentPlayers,
                    MaxPlayers = payload.MaxPlayers,
                    Status = payload.Status,
                    LastSeen = DateTime.UtcNow
                };

                // JSON-ként elmentjük a Redisbe 90 másodperces lejárattal.
                // Ha 90 mp-ig nem jön új Heartbeat a Unity szervertől, a Redis automatikusan törli!
                await db.StringSetAsync(
                    redisKey,
                    JsonSerializer.Serialize(zoneData),
                    TimeSpan.FromSeconds(90)
                );

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogSync($"Error processing Node Heartbeat (Zone: {payload.ZoneName}): {ex.Message}", GnosisLogLevel.Error);
                return StatusCode(500);
            }
        }

        // GET: /api/heartbeat/health
        // Ezt hívhatja külső monitorozó (pl. Uptime Kuma vagy a sysadmin)
        [AllowAnonymous]
        [HttpGet("health")]
        public async Task<IActionResult> CheckHealth()
        {
            try
            {
                bool isDbConnected = await _context.Database.CanConnectAsync();
                bool isRedisConnected = _redis.IsConnected;

                if (isDbConnected && isRedisConnected)
                    return Ok(new { status = "Healthy", db = "Online", redis = "Online" });

                _logger.LogSync($"Health Check PARTIAL ERROR: DB: {isDbConnected}, Redis: {isRedisConnected}", GnosisLogLevel.Warning);
                return StatusCode(503, new { status = "Degraded", db = isDbConnected ? "Online" : "Offline", redis = isRedisConnected ? "Online" : "Offline" });
            }
            catch (Exception ex)
            {
                _logger.LogSync($"Health Check CRITICAL ERROR: {ex.Message}", GnosisLogLevel.Error);
                return StatusCode(503, "Service Offline.");
            }
        }
    }
}