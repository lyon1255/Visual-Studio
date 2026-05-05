using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using StackExchange.Redis;
using Microsoft.Extensions.Configuration;

namespace GnosisNodeAgent
{
    class Program
    {
        private static IConfigurationRoot? _config;
        private static string _nodeName = "Worker-Node-01";
        private static string _redisConn = "";
        private static string _unityPath = "";
        private static string _logDirectory = "Logs";
        private static string _publicIp = "";

        private static string _redisHost = "";
        private static string _redisPassword = "";

        // TUPLE HASZNÁLATA: Eltároljuk a folyamatot és a hozzá tartozó portot is!
        // Kulcs: "ZoneName_Port" (így futhat több ugyanolyan nevű zóna is)
        private static readonly ConcurrentDictionary<string, (Process Proc, int Port)> _runningProcesses = new();
        private static ConnectionMultiplexer? _redis;

        static void Main(string[] args)
        {
            InitializeLogger();
            LoadConfiguration();
            ConnectToRedis();

            Thread.Sleep(Timeout.Infinite);
        }

        #region Logging System

        enum LogLevel { Info, Warning, Error, Critical }

        private static void InitializeLogger()
        {
            if (!Directory.Exists(_logDirectory)) Directory.CreateDirectory(_logDirectory);
        }

        private static void LogMessage(string message, LogLevel level = LogLevel.Info)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string logEntry = $"[{timestamp}] [{level.ToString().ToUpper()}] {message}";

            switch (level)
            {
                case LogLevel.Info: Console.ForegroundColor = ConsoleColor.White; break;
                case LogLevel.Warning: Console.ForegroundColor = ConsoleColor.Yellow; break;
                case LogLevel.Error: Console.ForegroundColor = ConsoleColor.Red; break;
                case LogLevel.Critical: Console.ForegroundColor = ConsoleColor.DarkRed; break;
            }

            Console.WriteLine(logEntry);
            Console.ResetColor();

            try
            {
                string logFileName = Path.Combine(_logDirectory, $"NodeAgent_Log_{DateTime.Now:yyyy_MM_dd}.txt");
                File.AppendAllText(logFileName, logEntry + Environment.NewLine);
            }
            catch { }

            if (_redis != null && _redis.IsConnected)
            {
                try
                {
                    string redisMessage = $"[{_nodeName}] {message}";
                    _redis.GetSubscriber().Publish(RedisChannel.Literal("gnosis.cluster.status"), redisMessage);
                }
                catch { }
            }
        }

        #endregion

        private static void LoadConfiguration()
        {
            try
            {
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

                _config = builder.Build();

                _nodeName = _config["NodeConfig:NodeName"] ?? "Worker-Node-Unknown";
                _unityPath = _config["NodeConfig:UnityServerPath"] ?? "";
                _redisHost = _config["Redis:RedisHost"] ?? "127.0.0.1:6379";
                _redisPassword = _config["Redis:RedisPassword"] ?? "";
                _redisConn = $"{_redisHost},password={_redisPassword},abortConnect=false";

                string configIpOverride = _config["NodeConfig:PublicIp"] ?? "AUTO";
                _publicIp = GetPublicIpAddress(configIpOverride);

                LogMessage($"Configuration loaded. Initializing as: {_nodeName}");
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to load configuration: {ex.Message}", LogLevel.Critical);
                Environment.Exit(1);
            }
        }

        #region Redis Communication & Process Management

        private static void ConnectToRedis()
        {
            if (string.IsNullOrEmpty(_redisConn)) return;

            LogMessage("Connecting to Master Redis server...");
            try
            {
                _redis = ConnectionMultiplexer.Connect(_redisConn);
                var sub = _redis.GetSubscriber();

                sub.Subscribe(RedisChannel.Literal("gnosis.cluster.commands"), (channel, message) => HandleCommand(message.ToString()));
                sub.Subscribe(RedisChannel.Literal("gnosis.cluster.emergency"), (channel, message) => HandleEmergencyStop(message.ToString()));

                LogMessage("Connected to Redis successfully! Waiting for commands...", LogLevel.Info);

                _redis.ConnectionFailed += (sender, e) =>
                {
                    LogMessage("CRITICAL: Lost connection to Master Redis!", LogLevel.Critical);
                    HandleEmergencyStop("Redis Disconnected (Dead Man's Switch)");
                };
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to connect to Redis: {ex.Message}", LogLevel.Critical);
            }
        }

        private static void HandleCommand(string jsonMessage)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonMessage);
                var root = doc.RootElement;
                string targetNode = root.GetProperty("targetNode").GetString() ?? "";

                if (targetNode != _nodeName && targetNode != "ALL") return;

                string action = root.GetProperty("action").GetString() ?? "";
                if (action == "start")
                {
                    string zoneName = root.GetProperty("zoneName").GetString() ?? "Main";
                    StartUnityProcess(zoneName);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Received invalid command format: {ex.Message}", LogLevel.Error);
            }
        }

        private static void StartUnityProcess(string zoneName)
        {
            int newPort = GetAvailablePort();
            string processKey = $"{zoneName}_{newPort}"; // Egyedi kulcs a porttal kombinálva

            // Port takarítás (Linux fuser használatával)
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = $"-c \"fuser -k {newPort}/tcp\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                }).WaitForExit();
            }
            catch { }

            LogMessage($"Starting Zone '{zoneName}' on port {newPort}...");

            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = _unityPath,
                    Arguments = $"-batchmode -nographics -zone {zoneName} -port {newPort} -ip {_publicIp} -redis {_redisHost} -redispass {_redisPassword}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var proc = new Process { StartInfo = processInfo };
                proc.EnableRaisingEvents = true;

                proc.Exited += (sender, e) =>
                {
                    LogMessage($"Process for Zone '{zoneName}' (Port: {newPort}) has STOPPED!", LogLevel.Error);
                    _runningProcesses.TryRemove(processKey, out _);
                };

                if (proc.Start())
                {
                    _runningProcesses.TryAdd(processKey, (proc, newPort));
                    LogMessage($"Zone '{zoneName}' successfully launched (PID: {proc.Id}) on Port {newPort}.");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to start Zone '{zoneName}': {ex.Message}", LogLevel.Error);
            }
        }

        private static void HandleEmergencyStop(string reason)
        {
            LogMessage($"EMERGENCY SHUTDOWN TRIGGERED: {reason}", LogLevel.Critical);

            foreach (var kvp in _runningProcesses)
            {
                try
                {
                    if (!kvp.Value.Proc.HasExited)
                    {
                        kvp.Value.Proc.Kill();
                        LogMessage($"Killed Zone Instance: {kvp.Key}", LogLevel.Warning);
                    }
                }
                catch { }
            }

            _runningProcesses.Clear();
            LogMessage("All processes secured.", LogLevel.Info);
        }

        private static int GetAvailablePort()
        {
            int start = int.Parse(_config?["NodeConfig:StartingPort"] ?? "7777");
            int port = start;
            var ipProps = IPGlobalProperties.GetIPGlobalProperties();

            while (true)
            {
                // 1. Megnézzük az OS szintű foglaltságot
                var tcpConnections = ipProps.GetActiveTcpConnections();
                var tcpListeners = ipProps.GetActiveTcpListeners();
                var udpListeners = ipProps.GetActiveUdpListeners();

                bool isTakenByOS = tcpConnections.Any(c => c.LocalEndPoint.Port == port)
                                || tcpListeners.Any(l => l.Port == port)
                                || udpListeners.Any(l => l.Port == port);

                // 2. Megnézzük a saját belső listánkat (amit mi már kiosztottunk, de a Linux még nem lát "foglaltnak")
                bool isReservedByUs = _runningProcesses.Values.Any(x => x.Port == port);

                if (!isTakenByOS && !isReservedByUs) return port;
                port++;
            }
        }

        private static string GetPublicIpAddress(string configIp)
        {
            if (!string.IsNullOrWhiteSpace(configIp) && configIp != "AUTO") return configIp;
            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                return client.GetStringAsync("https://api.ipify.org").GetAwaiter().GetResult().Trim();
            }
            catch { return "127.0.0.1"; }
        }
        #endregion
    }
}