using GnosisAuthServer.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Kestrel konfiguráció
string serverUrl = builder.Configuration.GetValue<string>("Kestrel:Endpoints:Http:Url") ?? "http://0.0.0.0:5158";
builder.WebHost.UseUrls(serverUrl);

// 1. KRITIKUS BEÁLLÍTÁS ELLENÕRZÉSE (Steam API Key)
string steamKey = builder.Configuration["Steam:WebApiKey"] ?? "";
if (string.IsNullOrEmpty(steamKey) || steamKey == "IDE_JON_A_STEAM_WEB_API_KULCSOD")
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("[CRITICAL ERROR] Steam Web API Key is missing or invalid in appsettings.json!");
    Console.ResetColor();
    //return; // STOP: Nincs értelme elindulni. TODO!
}

// 2. ADATBÁZIS ÉS SZOLGÁLTATÁSOK BEÁLLÍTÁSA
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<MasterDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

builder.Services.AddHttpClient();
builder.Services.AddControllers();

// Rate Limiter és Auth beállítások
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("ClientDataSync", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 3;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var jwtKey = builder.Configuration["Jwt:Key"] ?? "";
if (jwtKey.Length < 32)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("[CRITICAL ERROR] JWT Key is missing or too short (min 32 chars)!");
    Console.ResetColor();
    return; // STOP
}

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

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", b => b.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // EZ A LÉNYEG: A kisbetûs "public string assetId;" mezõket is JSON-né alakítja!
        options.JsonSerializerOptions.IncludeFields = true;
    });


var app = builder.Build();

// 3. ADATBÁZIS KAPCSOLAT ELLENÕRZÉSE
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
    Console.Write("[DB CHECK] Connecting to MySQL (gnosis_master)... ");
    if (!context.Database.CanConnect())
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("FAILED!");
        Console.WriteLine("[CRITICAL] Could not establish connection to database.");
        Console.WriteLine("[HINT] Check 'DefaultConnection' in appsettings.json and verify MySQL is running.");
        Console.ResetColor();
        return; // STOP
    }
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("SUCCESS!");
    Console.ResetColor();
}

app.UseCors("AllowAll");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

Console.WriteLine("====================================================");
Console.WriteLine("   GNOSIS AUTH SERVER - STATUS: ONLINE");
Console.WriteLine($"   Started at: {DateTime.Now}");
Console.WriteLine("====================================================");

app.Run();