using StackExchange.Redis;
using System.Text.Json;

namespace GnosisRealmCore.Services
{
    public class MasterHubService : BackgroundService
    {
        private readonly LoggerService _logger;
        private readonly IConfiguration _config;
        private readonly IConnectionMultiplexer _redis;
        private readonly ServerOrchestratorService _orchestrator;
        private readonly string _globalAuthUrl;
        private readonly string _realmApiKey;
        private readonly string _realmName;
        private readonly string _realmRegion;
        private readonly string _publicApiUrl;

        public MasterHubService(LoggerService logger, IConfiguration config, IConnectionMultiplexer redis, ServerOrchestratorService orchestrator)
        {
            _logger = logger;
            _config = config;
            _redis = redis;
            _orchestrator = orchestrator;

            _globalAuthUrl = _config["ServerSettings:GlobalAuthUrl"] ?? string.Empty;
            _realmApiKey = _config["ServerSettings:ApiKey"] ?? string.Empty;
            _realmName = _config["ServerSettings:RealmName"] ?? "Gnosis Official EU";
            _realmRegion = _config["ServerSettings:RealmRegion"] ?? "EU-West";
            _publicApiUrl = _config["ServerSettings:PublicApiUrl"] ?? string.Empty;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogSync("MasterHub Service has started in the background. Waiting for the system to boot up...");
            await Task.Delay(5000, stoppingToken);

            if (_redis.IsConnected)
            {
                var db = _redis.GetDatabase();
                await db.HashSetAsync("Gnosis:GlobalConfig", new HashEntry[] {
                    new HashEntry("SharedApiKey", _realmApiKey),
                    new HashEntry("RealmApiUrl", _publicApiUrl)
                });
                _logger.LogSync("GlobalConfig (API Key, URL) uploaded to Redis.");
            }

            var autoZones = _config.GetSection("ServerSettings:AutoStartZones").Get<string[]>();
            if (autoZones != null)
            {
                foreach (var zone in autoZones)
                {
                    _logger.LogSync($"Auto-starting mandatory zone via NodeAgent: {zone}");

                    // JAVÍTVA: Nincs 127.0.0.1 átadva, csak a zoneName
                    _ = _orchestrator.GetOrStartZoneAsync(zone);

                    await Task.Delay(5000);
                }
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                await GlobalHeartbeatLoop();
                await Task.Delay(30000, stoppingToken);
            }
        }

        private async Task GlobalHeartbeatLoop()
        {
            if (string.IsNullOrEmpty(_globalAuthUrl))
            {
                _logger.LogSync("GlobalAuthUrl is missing from the configuration. The server will not appear in the server list!", GnosisLogLevel.Error);
                return;
            }

            try
            {
                int totalPlayers = 0;
                int maxPlayers = int.Parse(_config["ServerSettings:MaxPlayers"] ?? "500");
                int status = 1;

                if (_redis.IsConnected)
                {
                    var db = _redis.GetDatabase();
                    var server = _redis.GetServer(_redis.GetEndPoints().First());

                    var keys = server.Keys(pattern: "Gnosis:ActiveZones:*").ToArray();

                    foreach (var key in keys)
                    {
                        string? json = await db.StringGetAsync(key);
                        if (!string.IsNullOrEmpty(json))
                        {
                            using var doc = JsonDocument.Parse(json);
                            totalPlayers += doc.RootElement.GetProperty("CurrentPlayers").GetInt32();
                        }
                    }
                }
                else
                {
                    status = 0;
                    _logger.LogSync("Redis connection lost! The heartbeat will show offline status.", GnosisLogLevel.Error);
                }

                var payload = new
                {
                    Name = _realmName,
                    Region = _realmRegion,
                    RealmApiUrl = _publicApiUrl,
                    CurrentPlayers = totalPlayers,
                    MaxPlayers = maxPlayers,
                    Status = status
                };

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("X-Server-Admin-Key", _realmApiKey);

                string jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{_globalAuthUrl}/api/heartbeat/update", content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogSync($"Failed to send Heartbeat to Global Auth. Status: {response.StatusCode}", GnosisLogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger.LogSync($"[MASTER HUB ERROR] {ex.Message}", GnosisLogLevel.Error);
            }
        }

        // Keresd meg a MasterHubService osztály végét, és add hozzá ezt a metódust:

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogSync("MasterHub is shutting down. Sending emergency stop to all nodes...");

            try
            {
                if (_redis.IsConnected)
                {
                    var sub = _redis.GetSubscriber();
                    // Elküldjük a parancsot a 'gnosis.cluster.emergency' csatornára
                    await sub.PublishAsync(RedisChannel.Literal("gnosis.cluster.emergency"), "Master Server Shutdown");

                    // Töröljük az aktív zónákat a Redisből is, hogy tiszta legyen a következő indítás
                    var db = _redis.GetDatabase();
                    var server = _redis.GetServer(_redis.GetEndPoints().First());
                    var keys = server.Keys(pattern: "Gnosis:ActiveZones:*").ToArray();
                    foreach (var key in keys) await db.KeyDeleteAsync(key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogSync($"Error during shutdown broadcast: {ex.Message}", GnosisLogLevel.Error);
            }

            await base.StopAsync(cancellationToken);
        }
    }
}