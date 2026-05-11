using Microsoft.Extensions.Caching.Distributed;

namespace GnosisAuthServer.Infrastructure;

public sealed class DistributedNonceStore : INonceStore
{
    private static readonly byte[] Marker = [1];
    private readonly IDistributedCache _cache;

    public DistributedNonceStore(IDistributedCache cache)
    {
        _cache = cache;
    }

    public bool TryUseNonce(string scope, string nonce, TimeSpan ttl)
    {
        var cacheKey = $"nonce:{scope}:{nonce}";
        var existing = _cache.Get(cacheKey);
        if (existing is not null)
        {
            return false;
        }

        _cache.Set(cacheKey, Marker, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl
        });

        return true;
    }
}
