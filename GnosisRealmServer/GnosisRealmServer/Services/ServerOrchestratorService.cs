using GnosisRealmCore.Data;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Text.Json;

namespace GnosisRealmCore.Services
{
    public class ServerOrchestratorService
    {
        private readonly ILogger<ServerOrchestratorService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConnectionMultiplexer _redis;

        // Memória a kiszervezett (Remote) zónáknak: ZoneName -> IP:Port
        private readonly ConcurrentDictionary<string, string> _remoteActiveZones = new();

        public ServerOrchestratorService(ILogger<ServerOrchestratorService> logger, IServiceScopeFactory scopeFactory, IConnectionMultiplexer redis)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _redis = redis;
        }

        // KIVETTÜK A localIp paramétert, mert a NodeAgent (Unity) küldi vissza a valós IP-jét!
        public async Task<string?> GetOrStartZoneAsync(string zoneName)
        {
            // A) Fut-e már valahol? (Villámgyors memória ellenőrzés)
            if (_remoteActiveZones.TryGetValue(zoneName, out string? remoteAddress)) return remoteAddress;

            // B) Ellenőrizzük a Redis-t is, hátha él, csak a MasterHub újraindult és a helyi memóriájából hiányzik
            var redisDb = _redis.GetDatabase();
            string redisKey = $"Gnosis:ActiveZones:{zoneName}";
            string? activeJson = await redisDb.StringGetAsync(redisKey);

            if (!string.IsNullOrEmpty(activeJson))
            {
                using var doc = JsonDocument.Parse(activeJson);
                string ip = doc.RootElement.GetProperty("IpAddress").GetString() ?? "127.0.0.1";
                int port = doc.RootElement.GetProperty("Port").GetInt32();
                string address = $"{ip}:{port}";
                _remoteActiveZones.TryAdd(zoneName, address);
                return address;
            }

            // C) Nincs futó zóna. Adatbázis ellenőrzése, hogy találjunk egy NodeAgentet
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RealmDbContext>();

            var remoteNodes = await db.RealmNodes.ToListAsync();
            string targetNodeName = "ALL"; // Fallback: ha az adatbázis üres, akkor is kiabálunk a Redisbe, hátha van egy bekapcsolt Agent

            if (remoteNodes.Count > 0)
            {
                var bestNode = remoteNodes
                    .Where(n => n.ActiveZones < n.MaxZones)
                    .OrderBy(n => n.ActiveZones) // A legkevésbé leterhelt VPS-t választjuk
                    .FirstOrDefault();

                if (bestNode == null)
                {
                    _logger.LogError("Kritikus: Minden VPS Node tele van (MaxZones elérve)!");
                    return null;
                }

                targetNodeName = bestNode.Name;
                bestNode.ActiveZones += 1; // Lefoglalunk neki egy helyet virtuálisan
                await db.SaveChangesAsync();
            }
            else
            {
                _logger.LogWarning("Nincs regisztrált VPS Node az adatbázisban! 'ALL' paranccsal próbálkozunk a Pub/Sub-on.");
            }

            // D) Küldünk egy Pub/Sub parancsot a NodeAgent(ek)nek
            _logger.LogInformation("Zóna ({ZoneName}) indítása. Célpont: {TargetNode}", zoneName, targetNodeName);
            try
            {
                var command = new { targetNode = targetNodeName, action = "start", zoneName = zoneName };
                var sub = _redis.GetSubscriber();
                await sub.PublishAsync(RedisChannel.Literal("gnosis.cluster.commands"), JsonSerializer.Serialize(command));

                // E) VÁRAKOZÁS AZ INDULÁSRA (A Heartbeat Polling)
                // Maximum 15 másodpercet várunk (30 * 500ms) a Unity szerver indulására
                for (int i = 0; i < 30; i++)
                {
                    await Task.Delay(500);
                    string? json = await redisDb.StringGetAsync(redisKey);

                    if (!string.IsNullOrEmpty(json))
                    {
                        using var doc = JsonDocument.Parse(json);

                        // Itt történik a csoda: a NodeAgent küldi be az IP-t!
                        string remoteIp = doc.RootElement.GetProperty("IpAddress").GetString() ?? "127.0.0.1";
                        int remotePort = doc.RootElement.GetProperty("Port").GetInt32();

                        string fullAddress = $"{remoteIp}:{remotePort}";

                        _remoteActiveZones.TryAdd(zoneName, fullAddress);
                        _logger.LogInformation($"Zóna sikeresen elindult a NodeAgent által: {fullAddress}");

                        return fullAddress;
                    }
                }

                _logger.LogError($"Időtúllépés: A NodeAgent nem indította el a(z) {zoneName} zónát időben.");

                // Visszaállítjuk a leterheltséget, ha mégsem indult el
                if (remoteNodes.Count > 0)
                {
                    var nodeToRevert = await db.RealmNodes.FirstOrDefaultAsync(n => n.Name == targetNodeName);
                    if (nodeToRevert != null && nodeToRevert.ActiveZones > 0)
                    {
                        nodeToRevert.ActiveZones -= 1;
                        await db.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hiba a távoli VPS ({NodeName}) parancsküldésekor.", targetNodeName);
            }

            return null;
        }
    }
}