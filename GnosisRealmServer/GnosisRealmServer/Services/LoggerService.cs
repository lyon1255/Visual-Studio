using StackExchange.Redis;

namespace GnosisRealmCore.Services
{
    public enum GnosisLogLevel { Info, Warning, Error, Critical }

    public class LoggerService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly string _logDirectory = "Logs";

        public LoggerService(IConnectionMultiplexer redis, IConfiguration config)
        {
            _redis = redis;
            if (!Directory.Exists(_logDirectory)) Directory.CreateDirectory(_logDirectory);
        }

        public async Task Log(string message, GnosisLogLevel level = GnosisLogLevel.Info)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string logEntry = $"[{timestamp}] [Realm Core] [{level.ToString().ToUpper()}] {message}";

            // 1. Konzol kimenet
            Console.WriteLine(logEntry);

            // 2. Fájl mentés (Napi rotáció: Realm_2026_05_05.log)
            try
            {
                string logFileName = Path.Combine(_logDirectory, $"Realm_{DateTime.Now:yyyy_MM_dd}.log");
                await File.AppendAllTextAsync(logFileName, logEntry + Environment.NewLine);
            }
            catch { /* Hiba esetén nem állunk le */ }
        }

        // Segédmetódus szinkron hívásokhoz (pl. Redis Subscribe-on belül)
        public void LogSync(string message, GnosisLogLevel level = GnosisLogLevel.Info)
        {
            Task.Run(() => Log(message, level));
        }
    }
}