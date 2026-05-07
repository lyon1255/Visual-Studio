using GnosisAuthServer.Data;
using GnosisAuthServer.Infrastructure;
using GnosisAuthServer.Options;
using GnosisAuthServer.Security;
using GnosisAuthServer.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Net;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables(prefix: "GNOSIS_AUTH_");

builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<SteamOptions>(builder.Configuration.GetSection(SteamOptions.SectionName));
builder.Services.Configure<RealmRegistryOptions>(builder.Configuration.GetSection(RealmRegistryOptions.SectionName));
builder.Services.Configure<ServiceAuthOptions>(builder.Configuration.GetSection(ServiceAuthOptions.SectionName));
builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection(SecurityOptions.SectionName));
builder.Services.Configure<CorsOptions>(builder.Configuration.GetSection(CorsOptions.SectionName));
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.SectionName));
builder.Services.Configure<SchemaDeliveryOptions>(builder.Configuration.GetSection(SchemaDeliveryOptions.SectionName));

var databaseOptions = builder.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()
    ?? throw new InvalidOperationException("Database configuration is missing.");
if (string.IsNullOrWhiteSpace(databaseOptions.ConnectionString))
{
    throw new InvalidOperationException("Database:ConnectionString is missing.");
}

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration is missing.");
if (string.IsNullOrWhiteSpace(jwtOptions.PrivateKeyPemPath))
{
    throw new InvalidOperationException("Jwt:PrivateKeyPemPath is missing.");
}
if (string.IsNullOrWhiteSpace(jwtOptions.PublicKeyPemPath))
{
    throw new InvalidOperationException("Jwt:PublicKeyPemPath is missing.");
}

builder.WebHost.UseUrls(builder.Configuration["Urls"] ?? "http://127.0.0.1:5158");

builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseMySql(databaseOptions.ConnectionString, ServerVersion.AutoDetect(databaseOptions.ConnectionString)));

var rsaKeyProvider = new FileRsaKeyProvider(Options.Create(jwtOptions));
builder.Services.AddSingleton<IRsaKeyProvider>(rsaKeyProvider);

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<ISteamTicketValidator, SteamTicketValidator>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IRealmRegistryService, RealmRegistryService>();
builder.Services.AddScoped<IGameDataService, GameDataService>();
builder.Services.AddSingleton<INonceStore, MemoryNonceStore>();
builder.Services.AddSingleton<IServiceRequestAuthenticator, HmacServiceRequestAuthenticator>();
builder.Services.AddSingleton<IAdminRequestValidator, HeaderAdminRequestValidator>();
builder.Services.AddSingleton<ISchemaCatalogService, SchemaCatalogService>();

builder.Services.AddControllers();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = rsaKeyProvider.GetValidationKey(),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 8,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("realm-list", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("official-heartbeat", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Request.Headers[ServiceAuthHeaderNames.ServiceId].ToString() switch
            {
                { Length: > 0 } value => value,
                _ => "unknown"
            },
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 12,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("realm-gamedata-read", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Request.Headers[ServiceAuthHeaderNames.ServiceId].ToString() switch
            {
                { Length: > 0 } value => value,
                _ => "unknown"
            },
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("admin-write", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.AddCors(options =>
{
    var configured = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>()?.AllowedOrigins ?? [];
    options.AddPolicy("ConfiguredOrigins", policy =>
    {
        if (configured.Length == 0)
        {
            policy.WithOrigins("https://localhost.invalid").AllowAnyHeader().AllowAnyMethod();
            return; 
        }

        policy.WithOrigins(configured).AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 2;

    var configuredProxies = builder.Configuration.GetSection("Security:KnownProxies").Get<string[]>() ?? [];
    foreach (var proxy in configuredProxies)
    {
        if (IPAddress.TryParse(proxy, out var ip))
        {
            options.KnownProxies.Add(ip);
        }
    }
});

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    var securityOptions = context.RequestServices.GetRequiredService<IOptions<SecurityOptions>>().Value;
    if (securityOptions.RequireHttps && !context.Request.IsHttps)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { error = "HTTPS is required." });
        return;
    }

    await next();
});

app.UseCors("ConfiguredOrigins");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    await dbContext.Database.CanConnectAsync();
}

app.Run();
