using GnosisRealmCore.Data;
using GnosisRealmCore.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. Konfigurációk beolvasása
string redisHost = builder.Configuration["Redis:RedisHost"] ?? "127.0.0.1:6379";
string redisPass = builder.Configuration["Redis:RedisPassword"] ?? "";
string redisConnStr = $"{redisHost},password={redisPass},abortConnect=false";

string connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new Exception("CRITICAL: MySQL Connection missing!");
string jwtKey = builder.Configuration["Jwt:Key"] ?? throw new Exception("CRITICAL: JWT Key missing!");

// 2. REDIS CSATLAKOZÁS
IConnectionMultiplexer redis;
try
{
    redis = ConnectionMultiplexer.Connect(redisConnStr);
}
catch (Exception ex)
{
    Console.WriteLine($"[CRITICAL ERROR] Redis Failure: {ex.Message}");
    throw;
}

// 3. SZOLGÁLTATÁSOK REGISZTRÁLÁSA (Dependency Injection)
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
builder.Services.AddSingleton<LoggerService>();
builder.Services.AddSingleton<ServerOrchestratorService>();
builder.Services.AddHostedService<MasterHubService>();

builder.Services.AddDbContext<RealmDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

builder.Services.AddControllers();

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("GeneralClient", opt => { opt.Window = TimeSpan.FromMinutes(1); opt.PermitLimit = 20; });
    options.AddFixedWindowLimiter("HeavyAction", opt => { opt.Window = TimeSpan.FromMinutes(1); opt.PermitLimit = 3; });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// 4. JWT AUTHENTICATION
var keyBytes = Encoding.UTF8.GetBytes(jwtKey);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes)
        };
    });

builder.Services.AddCors(o => o.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// 5. LOGGER ELÉRÉSE AZ APP-BÓL
var logger = app.Services.GetRequiredService<LoggerService>();

// --- REDIS PUB/SUB ---
var subscriber = redis.GetSubscriber();
subscriber.Subscribe(RedisChannel.Literal("gnosis.cluster.status"), async (channel, message) =>
{
    string msg = message.ToString();
    app.Logger.LogInformation($"[NODE AGENT] {msg}");
    await logger.Log($"[NODE AGENT] {msg}", GnosisLogLevel.Info);
});

// --- ADATBÁZIS ELLENÕRZÉS ---
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<RealmDbContext>();
    if (!context.Database.CanConnect())
    {
        await logger.Log("CRITICAL: Cannot connect to MySQL!", GnosisLogLevel.Critical);
        throw new Exception("MySQL Connection Failure");
    }
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

logger.LogSync("Gnosis Realm Server & Master Hub elindult!");
app.Run();