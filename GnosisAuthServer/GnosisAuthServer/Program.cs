using GnosisAuthServer.CommandMode;
using GnosisAuthServer.CommandMode.Modules;
using GnosisAuthServer.Data;
using GnosisAuthServer.Infrastructure;
using GnosisAuthServer.Options;
using GnosisAuthServer.Security;
using GnosisAuthServer.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
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
builder.Services.Configure<GameDataOptions>(builder.Configuration.GetSection(GameDataOptions.SectionName));
builder.Services.Configure<AccountAccessOptions>(builder.Configuration.GetSection(AccountAccessOptions.SectionName));
builder.Services.Configure<NonceStoreOptions>(builder.Configuration.GetSection(NonceStoreOptions.SectionName));

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
    options.UseMySql(
        databaseOptions.ConnectionString,
        ServerVersion.AutoDetect(databaseOptions.ConnectionString)));

var rsaKeyProvider = new FileRsaKeyProvider(Options.Create(jwtOptions));
builder.Services.AddSingleton<IRsaKeyProvider>(rsaKeyProvider);

builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSingleton<MemoryCache>(_ =>
    new MemoryCache(new MemoryCacheOptions
    {
        SizeLimit = 10_000
    }));

builder.Services.AddHttpClient<ISteamTicketValidator, SteamTicketValidator>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IRealmRegistryService, RealmRegistryService>();
builder.Services.AddScoped<IGameDataService, GameDataService>();
builder.Services.AddScoped<IAccountAccessValidator, CachedAccountAccessValidator>();
builder.Services.AddSingleton<ISchemaCatalogService, SchemaCatalogService>();
builder.Services.AddSingleton<INonceStore>(sp =>
{
    var nonceStoreOptions = sp.GetRequiredService<IOptions<NonceStoreOptions>>().Value;
    if (nonceStoreOptions.UseDistributedCache)
    {
        return new DistributedNonceStore(sp.GetRequiredService<IDistributedCache>());
    }

    return new MemoryNonceStore(sp.GetRequiredService<MemoryCache>());
});
builder.Services.AddSingleton<IServiceRequestAuthenticator, HmacServiceRequestAuthenticator>();
builder.Services.AddSingleton<IAdminRequestValidator, HeaderAdminRequestValidator>();
builder.Services.AddSingleton<IIpBanCacheService, IpBanCacheService>();

builder.Services.AddSingleton<IAuthCommandModule, VersionCommandModule>();
builder.Services.AddSingleton<IAuthCommandModule, DoctorCommandModule>();
builder.Services.AddSingleton<IAuthCommandModule, DbCommandModule>();
builder.Services.AddSingleton<IAuthCommandModule, JwtCommandModule>();
builder.Services.AddSingleton<IAuthCommandModule, EnvironmentCommandModule>();
builder.Services.AddSingleton<IAuthCommandModule, RealmsCommandModule>();
builder.Services.AddSingleton<IAuthCommandModule, ServicesCommandModule>();
builder.Services.AddSingleton<IAuthCommandModule, GameDataCommandModule>();
builder.Services.AddSingleton<IAuthCommandModule, SecurityCommandModule>();

builder.Services.AddProblemDetails();
builder.Services.AddControllers();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
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

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var steamId = context.Principal?.FindFirst("sub")?.Value
                    ?? context.Principal?.Identity?.Name;

                var accountAccessValidator = context.HttpContext.RequestServices.GetRequiredService<IAccountAccessValidator>();
                var result = await accountAccessValidator.ValidateAsync(steamId ?? string.Empty, context.HttpContext.RequestAborted);

                if (!result.IsAllowed)
                {
                    context.Fail(result.DenialReason ?? "Account access denied.");
                }
            }
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

    options.AddPolicy("realm-heartbeat", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetPreAuthPartitionKey(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 12,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("realm-gamedata-read", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetPreAuthPartitionKey(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("realm-schema-read", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetPreAuthPartitionKey(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
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
    var configuredOrigins =
        builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>()?.AllowedOrigins
        ?? Array.Empty<string>();

    options.AddPolicy("ConfiguredOrigins", policy =>
    {
        if (configuredOrigins.Length == 0)
        {
            policy.WithOrigins("https://localhost.invalid")
                .AllowAnyHeader()
                .AllowAnyMethod();
            return;
        }

        policy.WithOrigins(configuredOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 2;

    var configuredProxies =
        builder.Configuration.GetSection("Security:KnownProxies").Get<string[]>()
        ?? Array.Empty<string>();

    foreach (var proxy in configuredProxies)
    {
        if (IPAddress.TryParse(proxy, out var ip))
        {
            options.KnownProxies.Add(ip);
        }
    }

    var configuredNetworks =
        builder.Configuration.GetSection("Security:KnownIPNetworks").Get<string[]>()
        ?? Array.Empty<string>();

    foreach (var network in configuredNetworks)
    {
        if (System.Net.IPNetwork.TryParse(network, out var ipNetwork))
        {
            options.KnownIPNetworks.Add(ipNetwork);
        }
    }
});

var app = builder.Build();

await ValidateStartupAsync(app);

var commandExitCode = await AuthCommandModeRunner.TryRunAsync(app, args);
if (commandExitCode.HasValue)
{
    Environment.ExitCode = commandExitCode.Value;
    return;
}

app.UseForwardedHeaders();
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

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

app.Use(async (context, next) =>
{
    var remoteIp = context.Connection.RemoteIpAddress?.ToString();
    if (!string.IsNullOrWhiteSpace(remoteIp))
    {
        var ipBanCache = context.RequestServices.GetRequiredService<IIpBanCacheService>();
        var isBlocked = await ipBanCache.IsBlockedAsync(remoteIp, context.RequestAborted);

        if (isBlocked)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "This IP address is blocked." });
            return;
        }
    }

    await next();
});

app.UseCors("ConfiguredOrigins");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static string GetPreAuthPartitionKey(HttpContext context)
{
    var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var path = context.Request.Path.HasValue ? context.Request.Path.Value! : "/";
    return $"{remoteIp}:{path}";
}

static async Task ValidateStartupAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();

    var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    var canConnect = await dbContext.Database.CanConnectAsync();
    if (!canConnect)
    {
        throw new InvalidOperationException("Auth database connection check failed during startup.");
    }

    var modules = scope.ServiceProvider.GetServices<IAuthCommandModule>().ToList();
    if (modules.Count == 0)
    {
        throw new InvalidOperationException("No command modules are registered.");
    }

    var expectedModuleTypes = new[]
    {
        typeof(VersionCommandModule),
        typeof(DoctorCommandModule),
        typeof(DbCommandModule),
        typeof(JwtCommandModule),
        typeof(EnvironmentCommandModule),
        typeof(RealmsCommandModule),
        typeof(ServicesCommandModule),
        typeof(GameDataCommandModule),
        typeof(SecurityCommandModule)
    };

    var registeredModuleTypes = modules
        .Select(x => x.GetType())
        .ToHashSet();

    var missingModules = expectedModuleTypes
        .Where(x => !registeredModuleTypes.Contains(x))
        .Select(x => x.Name)
        .ToArray();

    if (missingModules.Length > 0)
    {
        throw new InvalidOperationException(
            $"Missing command module registrations: {string.Join(", ", missingModules)}");
    }
}
