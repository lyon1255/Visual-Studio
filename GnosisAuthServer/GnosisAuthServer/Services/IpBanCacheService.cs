using GnosisAuthServer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GnosisAuthServer.Services;

public sealed class IpBanCacheService : IIpBanCacheService
{
    private const string GlobalVersionCacheKey = "ip-ban:version";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private readonly IMemoryCache _memoryCache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IpBanCacheService> _logger;

    public IpBanCacheService(
        IMemoryCache memoryCache,
        IServiceScopeFactory scopeFactory,
        ILogger<IpBanCacheService> logger)
    {
        _memoryCache = memoryCache;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<bool> IsBlockedAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return false;
        }

        var normalizedIp = ipAddress.Trim();
        var version = GetVersion();
        var cacheKey = BuildIpCacheKey(normalizedIp, version);

        if (_memoryCache.TryGetValue(cacheKey, out bool cached))
        {
            return cached;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var nowUtc = DateTime.UtcNow;

        var isBlocked = await dbContext.BannedIpAddresses
            .AsNoTracking()
            .AnyAsync(x =>
                x.Enabled &&
                x.IpAddress == normalizedIp &&
                (x.ExpiresAtUtc == null || x.ExpiresAtUtc > nowUtc),
                cancellationToken);

        _memoryCache.Set(cacheKey, isBlocked, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheDuration,
            Size = 1
        });

        return isBlocked;
    }

    public void Invalidate(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return;
        }

        BumpVersion();
        _logger.LogDebug("IP ban cache invalidated for IP {IpAddress}.", ipAddress);
    }

    public void InvalidateAll()
    {
        BumpVersion();
        _logger.LogDebug("IP ban cache invalidated globally.");
    }

    private int GetVersion()
    {
        if (_memoryCache.TryGetValue(GlobalVersionCacheKey, out int version))
        {
            return version;
        }

        version = 1;
        _memoryCache.Set(GlobalVersionCacheKey, version);
        return version;
    }

    private void BumpVersion()
    {
        var next = GetVersion() + 1;
        _memoryCache.Set(GlobalVersionCacheKey, next);
    }

    private static string BuildIpCacheKey(string ipAddress, int version)
    {
        return $"ip-ban:{version}:{ipAddress}";
    }
}