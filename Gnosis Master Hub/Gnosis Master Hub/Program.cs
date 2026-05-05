using Microsoft.Extensions.Configuration;
using MySqlConnector;
using StackExchange.Redis;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GnosisMasterHub
{
    class Program
    {
        private static IConfigurationRoot? _config;
        private static string _redisConn = "";
        private static string _mysqlConn = "";
        private static string _logDirectory = "MasterLogs";
        private static ConnectionMultiplexer? _redis;
        private static Process? _realmApiProcess;
        private static bool _isOperational = false;

        // A frissítések követéséhez használt változók
        private static string _currentVersionDate = "none";
        private static string _currentDbSha = "none";

        static async Task Main(string[] args)
        {
            InitializeLogger();
            LogMessage("========================================", LogLevel.Info);
            LogMessage("     GNOSIS MASTER HUB                  ", LogLevel.Info);
            LogMessage("========================================", LogLevel.Info);

            LoadConfiguration();

            // 1. Adatbázis kapcsolat ellenőrzése
            if (!await CheckAndSeedDatabaseAsync())
            {
                LogMessage("CRITICAL: Database connection failed. System halted.", LogLevel.Critical);
                return;
            }

            // Azonnali adatbázis frissítés ellenőrzés induláskor
            await CheckForDatabaseUpdatesAsync();

            // 2. Redis kapcsolat felépítése
            if (!ConnectToRedis())
            {
                LogMessage("CRITICAL: Redis connection failed. System halted.", LogLevel.Critical);
                return;
            }

            _isOperational = true;

            // 3. Háttérfolyamat indítása: 10mp-es ciklus az API és DB figyeléséhez
            //_ = Task.Run(UpdateMonitorLoop);
            _ = Task.Run(GlobalHeartbeatLoop);

            LogMessage("Master Hub is active. Type 'exit' to shutdown.", LogLevel.Info);

            // Fő szál várakozása
            while (_isOperational)
            {
                var input = Console.ReadLine();
                if (input?.ToLower() == "exit")
                {
                    _isOperational = false;
                    ShutdownSystem();
                    break;
                }
            }
        }

        private static async Task UpdateMonitorLoop()
        {
            while (_isOperational)
            {
                try
                {
                    // API Release és SQL script ellenőrzése a GitHubon
                    await CheckForUpdatesAsync();
                    await CheckForDatabaseUpdatesAsync();
                }
                catch (Exception ex)
                {
                    LogMessage($"Update Check Error: {ex.Message}", LogLevel.Error);
                }

                await Task.Delay(60000);
            }
        }

        #region Adatbázis Automatikus Frissítés

        private static async Task<bool> CheckAndSeedDatabaseAsync()
        {
            LogMessage("Checking database connection...");
            try
            {
                using var conn = new MySqlConnection(_mysqlConn);
                await conn.OpenAsync();
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"Database error: {ex.Message}", LogLevel.Error);
                return false;
            }
        }

        private static async Task CheckForDatabaseUpdatesAsync()
        {
            string schemaUrl = _config?["MasterConfig:AutoSetup:DatabaseSchemaUrl"] ?? "";
            string installDir = _config?["MasterConfig:AutoSetup:InstallDirectory"] ?? "/opt/GnosisRealmAPI/";

            if (string.IsNullOrEmpty(schemaUrl)) return;
            if (!Directory.Exists(installDir)) Directory.CreateDirectory(installDir);

            string dbVersionFile = Path.Combine(installDir, "db_version.txt");

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "GnosisMasterHub-Updater");

                // Letöltjük magát az SQL kódot közvetlenül
                string sqlScript = await client.GetStringAsync(schemaUrl);

                // Generálunk egy egyszerű Hash-t a letöltött kód alapján, hogy lássuk, változott-e
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                byte[] hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sqlScript));
                string remoteSha = Convert.ToBase64String(hashBytes);

                if (_currentDbSha == "none")
                {
                    _currentDbSha = File.Exists(dbVersionFile) ? await File.ReadAllTextAsync(dbVersionFile) : "0";
                }

                if (_currentDbSha != remoteSha)
                {
                    LogMessage($"[DB UPDATE] Új adatbázis séma észlelve! Alkalmazás...", LogLevel.Warning);

                    if (await ExecuteSqlScriptAsync(sqlScript))
                    {
                        _currentDbSha = remoteSha;
                        await File.WriteAllTextAsync(dbVersionFile, remoteSha);
                        LogMessage("[DB SUCCESS] Az adatbázis frissítése sikeres volt.", LogLevel.Info);
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"[GITHUB SQL ERROR] Nem sikerült elérni az SQL fájlt: {ex.Message}", LogLevel.Error);
            }
        }

        private static async Task<bool> ExecuteSqlScriptAsync(string script)
        {
            try
            {
                using var conn = new MySqlConnection(_mysqlConn);
                await conn.OpenAsync();

                using var cmd = new MySqlCommand(script, conn);
                await cmd.ExecuteNonQueryAsync();
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"SQL error while running: {ex.Message}", LogLevel.Error);
                return false;
            }
        }

        #endregion

        #region Realm API Frissítés

        private static async Task CheckForUpdatesAsync()
        {
            string versionUrl = _config?["MasterConfig:AutoSetup:RealmApiVersionUrl"] ?? "";
            string zipUrl = _config?["MasterConfig:AutoSetup:RealmApiZipUrl"] ?? "";
            string installDir = _config?["MasterConfig:AutoSetup:InstallDirectory"] ?? "/opt/GnosisRealmAPI/";

            if (string.IsNullOrEmpty(versionUrl) || string.IsNullOrEmpty(zipUrl)) return;

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "GnosisMasterHub-Updater");

                // Egyszerűen letöltjük a nyers text fájlt, amiben a verziószám van
                string remoteVersionDate = await client.GetStringAsync(versionUrl);
                remoteVersionDate = remoteVersionDate.Trim();

                if (_currentVersionDate == "none")
                {
                    string localVersionPath = Path.Combine(installDir, "api_version.txt");
                    _currentVersionDate = File.Exists(localVersionPath) ? await File.ReadAllTextAsync(localVersionPath) : "0";
                }

                if (_currentVersionDate != remoteVersionDate)
                {
                    LogMessage($"[API UPDATE] Új szerver build észlelve: {remoteVersionDate}", LogLevel.Warning);
                    StopRealmApi();
                    await DownloadAndExtractSafeAsync(zipUrl, installDir, remoteVersionDate);
                    StartRealmApi(installDir);
                    _currentVersionDate = remoteVersionDate;
                }
                else if (_realmApiProcess == null || _realmApiProcess.HasExited)
                {
                    StartRealmApi(installDir);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"[GITHUB API ERROR] Nem sikerült lekérni a verziót: {ex.Message}", LogLevel.Error);
            }
        }

        private static async Task DownloadAndExtractSafeAsync(string url, string targetDir, string versionDate)
        {
            try
            {
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                string tempZip = Path.Combine(Path.GetTempPath(), "gnosis_update.zip");
                using var client = new HttpClient();
                var data = await client.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(tempZip, data);

                using (ZipArchive archive = ZipFile.OpenRead(tempZip))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string destinationPath = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));
                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            Directory.CreateDirectory(destinationPath);
                            continue;
                        }

                        if (entry.Name.Equals("appsettings.json", StringComparison.OrdinalIgnoreCase) && File.Exists(destinationPath))
                        {
                            LogMessage($"[SKIP] Configuration preserved: {entry.Name}", LogLevel.Info);
                            continue;
                        }

                        entry.ExtractToFile(destinationPath, true);
                    }
                }

                File.Delete(tempZip);
                await File.WriteAllTextAsync(Path.Combine(targetDir, "api_version.txt"), versionDate);

                string exePath = Path.Combine(targetDir, "GnosisRealmServer");
                if (File.Exists(exePath))
                {
                    Process.Start("chmod", $"+x {exePath}");
                }

                LogMessage($"[SUCCESS] Build {versionDate} installed.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LogMessage($"Extraction error: {ex.Message}", LogLevel.Error);
            }
        }

        private static void StartRealmApi(string installDir)
        {
            string apiPath = Path.Combine(installDir, "GnosisRealmServer");
            if (!File.Exists(apiPath))
            {
                LogMessage($"Error: Executable not found: {apiPath}", LogLevel.Error);
                return;
            }

            // 1. ELŐSZÖR feltöltjük a titkokat a Redisbe
            if (_redis != null && _redis.IsConnected)
            {
                var db = _redis.GetDatabase();
                db.HashSet("Gnosis:GlobalConfig", new HashEntry[]
                {
                    new HashEntry("MySqlConnectionString", _mysqlConn),
                    new HashEntry("SharedJwtKey", _config?["MasterConfig:SharedJwtKey"] ?? ""),
                    new HashEntry("SharedApiKey", _config?["MasterConfig:SharedApiKey"] ?? ""),
                    new HashEntry("RealmApiPort", _config?["MasterConfig:RealmApiPort"] ?? "5159")
                });
                LogMessage("Global configuration uploaded to Redis.", LogLevel.Info);
            }

            LogMessage("Starting Gnosis Realm API...", LogLevel.Info);

            // 2. Csak EGYSZER hozzuk létre a környezetet, és injektáljuk a Redis elérést
            var psi = new ProcessStartInfo
            {
                FileName = apiPath,
                WorkingDirectory = installDir,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            psi.EnvironmentVariables["RedisConnectionString"] = _redisConn;

            // 3. Elindítjuk
            _realmApiProcess = Process.Start(psi);
        }

        private static void StopRealmApi()
        {
            if (_realmApiProcess != null && !_realmApiProcess.HasExited)
            {
                LogMessage("Stopping running API...", LogLevel.Warning);
                _realmApiProcess.Kill();
                _realmApiProcess.WaitForExit();
            }
        }

        #endregion

        #region Segédmetódusok (Logger, Config, Redis, Shutdown)

        private static void InitializeLogger()
        {
            var loc = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _logDirectory = Path.Combine(loc ?? Directory.GetCurrentDirectory(), "MasterLogs");
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
        }

        private static void LogMessage(string message, LogLevel level = LogLevel.Info)
        {
            string logEntry = $"[{DateTime.Now:HH:mm:ss}] [{level.ToString().ToUpper()}] {message}";

            switch (level)
            {
                case LogLevel.Info: Console.ForegroundColor = ConsoleColor.White; break;
                case LogLevel.Warning: Console.ForegroundColor = ConsoleColor.Yellow; break;
                case LogLevel.Error: Console.ForegroundColor = ConsoleColor.Red; break;
                case LogLevel.Critical: Console.ForegroundColor = ConsoleColor.DarkRed; break;
                case LogLevel.NodeEvent: Console.ForegroundColor = ConsoleColor.Cyan; break;
            }

            Console.WriteLine(logEntry);
            Console.ResetColor();

            try
            {
                File.AppendAllText(Path.Combine(_logDirectory, $"Log_{DateTime.Now:yyyy_MM_dd}.txt"), logEntry + Environment.NewLine);
            }
            catch { }
        }

        private static void LoadConfiguration()
        {
            var loc = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _config = new ConfigurationBuilder()
                .SetBasePath(loc ?? Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            _redisConn = _config["MasterConfig:RedisConnectionString"] ?? "";
            _mysqlConn = _config["MasterConfig:MySqlConnectionString"] ?? "";
        }

        private static bool ConnectToRedis()
        {
            try
            {
                LogMessage("Connecting to Redis Cluster...");
                _redis = ConnectionMultiplexer.Connect(_redisConn);
                var sub = _redis.GetSubscriber();
                sub.Subscribe(RedisChannel.Literal("gnosis.cluster.status"), (ch, msg) => LogMessage($"[NODE] {msg}", LogLevel.NodeEvent));
                LogMessage("Redis connection successful.", LogLevel.Info);
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"Redis error: {ex.Message}", LogLevel.Error);
                return false;
            }
        }

        private static void ShutdownSystem()
        {
            LogMessage("Stopping Master Hub...", LogLevel.Warning);
            StopRealmApi();
            if (_redis != null && _redis.IsConnected)
            {
                _redis.GetDatabase().Publish(RedisChannel.Literal("gnosis.cluster.emergency"), "Master Hub shutdown.");
            }
            LogMessage("System offline.", LogLevel.Info);
        }

        enum LogLevel { Info, Warning, Error, Critical, NodeEvent }

        #endregion

        #region Global Auth Synchronization

        private static async Task GlobalHeartbeatLoop()
        {
            LogMessage("Starting Global Heartbeat Loop...", LogLevel.Info);

            string globalAuthUrl = _config?["MasterConfig:GlobalAuthUrl"] ?? "";
            string sharedApiKey = _config?["MasterConfig:SharedApiKey"] ?? "";
            string realmName = _config?["MasterConfig:RealmName"] ?? "Unknown Realm";
            string region = _config?["MasterConfig:RealmRegion"] ?? "Unknown";
            string publicApiUrl = _config?["MasterConfig:PublicApiUrl"] ?? "";

            if (string.IsNullOrEmpty(globalAuthUrl))
            {
                LogMessage("GlobalAuthUrl is missing. Heartbeats will NOT be sent.", LogLevel.Warning);
                return;
            }

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("X-Server-Admin-Key", sharedApiKey);

            while (_isOperational)
            {
                try
                {
                    int totalPlayers = 0;
                    int maxPlayers = 500; // Ezt olvashatnád konfigból is
                    int status = 1; // 1 = Online

                    // 1. Összesítjük a játékosokat a Redisből
                    if (_redis != null && _redis.IsConnected)
                    {
                        var db = _redis.GetDatabase();
                        var server = _redis.GetServer(_redis.GetEndPoints().First());

                        // Megkeressük az összes "Gnosis:ActiveZones:*" kulcsot
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
                        status = 0; // Offline, ha nincs Redis
                    }

                    // 2. Összeállítjuk a payloadot a Global Auth Servernek
                    var payload = new
                    {
                        Name = realmName,
                        Region = region,
                        RealmApiUrl = publicApiUrl,
                        CurrentPlayers = totalPlayers,
                        MaxPlayers = maxPlayers,
                        Status = status
                    };

                    string jsonPayload = JsonSerializer.Serialize(payload);
                    var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

                    // 3. Elküldjük a Heartbeat-et
                    var response = await httpClient.PostAsync($"{globalAuthUrl}/api/heartbeat/update", content);

                    if (!response.IsSuccessStatusCode)
                    {
                        LogMessage($"[GLOBAL AUTH] Failed to send heartbeat. Status: {response.StatusCode}", LogLevel.Warning);
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"[GLOBAL AUTH ERROR] {ex.Message}", LogLevel.Error);
                }

                // 30 másodpercenként küldünk életjelet
                await Task.Delay(30000);
            }
        }

        #endregion
    }
}