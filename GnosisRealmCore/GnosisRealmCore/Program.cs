using GnosisRealmCore.Data;
using GnosisRealmCore.Infrastructure;
using GnosisRealmCore.Options;
using GnosisRealmCore.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables(prefix: "GNOSIS_REALM_");

builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<AuthApiOptions>(builder.Configuration.GetSection(AuthApiOptions.SectionName));
builder.Services.Configure<JwtValidationOptions>(builder.Configuration.GetSection(JwtValidationOptions.SectionName));
builder.Services.Configure<ServiceAuthOptions>(builder.Configuration.GetSection(ServiceAuthOptions.SectionName));
builder.Services.Configure<LegacyNodeAuthOptions>(builder.Configuration.GetSection(LegacyNodeAuthOptions.SectionName));
builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection(SecurityOptions.SectionName));
builder.Services.Configure<CorsOptions>(builder.Configuration.GetSection(CorsOptions.SectionName));
builder.Services.Configure<RealmOptions>(builder.Configuration.GetSection(RealmOptions.SectionName));
builder.Services.Configure<GameDataCacheOptions>(builder.Configuration.GetSection(GameDataCacheOptions.SectionName));

var connectionString = builder.Configuration.GetSection(DatabaseOptions.SectionName)["ConnectionString"]
    ?? throw new InvalidOperationException("Missing Database:ConnectionString configuration.");

builder.Services.AddDbContext<RealmDbContext>(options =>
{
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient(nameof(AuthApiClient));
builder.Services.AddControllers();

var corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();
builder.Services.AddCors(options =>
{
    options.AddPolicy("realm-public", policy =>
    {
        if (corsOptions.AllowedOrigins.Length == 0)
        {
            policy.WithOrigins("https://localhost.invalid")
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(corsOptions.AllowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

builder.Services.AddSingleton<INonceStore, MemoryNonceStore>();
builder.Services.AddSingleton<IJwtTokenValidator, JwtTokenValidator>();
builder.Services.AddSingleton<IServiceRequestAuthenticator, HmacServiceRequestAuthenticator>();
builder.Services.AddSingleton<ILegacyNodeApiKeyValidator, LegacyNodeApiKeyValidator>();
builder.Services.AddSingleton<IAdminRequestValidator, HeaderAdminRequestValidator>();

builder.Services.AddScoped<ICharacterService, CharacterService>();
builder.Services.AddScoped<IZoneOrchestrationService, ZoneOrchestrationService>();

builder.Services.AddSingleton<IAuthApiClient, AuthApiClient>();
builder.Services.AddSingleton<IGameDataCacheService, GameDataCacheService>();

builder.Services.AddHostedService<RealmHeartbeatHostedService>();

var app = builder.Build();

var securityOptions = app.Services.GetRequiredService<IConfiguration>()
    .GetSection(SecurityOptions.SectionName)
    .Get<SecurityOptions>() ?? new SecurityOptions();

if (securityOptions.KnownProxies.Length > 0 || securityOptions.KnownIPNetworks.Length > 0)
{
    var forwardedHeadersOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    };

    foreach (var proxy in securityOptions.KnownProxies)
    {
        if (IPAddress.TryParse(proxy, out var ip))
        {
            forwardedHeadersOptions.KnownProxies.Add(ip);
        }
    }

    foreach (var network in securityOptions.KnownIPNetworks)
    {
        if (System.Net.IPNetwork.TryParse(network, out var ipNetwork))
        {
            forwardedHeadersOptions.KnownIPNetworks.Add(ipNetwork);
        }
    }

    app.UseForwardedHeaders(forwardedHeadersOptions);
}

app.UseCors("realm-public");

await using (var scope = app.Services.CreateAsyncScope())
{
    var cache = scope.ServiceProvider.GetRequiredService<IGameDataCacheService>();
    await cache.WarmAsync(CancellationToken.None);
}

app.MapControllers();

app.Run();