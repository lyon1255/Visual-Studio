using Microsoft.Extensions.Caching.Memory;

namespace GnosisAuthServer.Infrastructure;

public sealed class MemoryNonceStore : INonceStore
{
    private readonly IMemoryCache _cache;

    public MemoryNonceStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public bool TryUseNonce(string scope, string nonce, TimeSpan ttl)
    {
        var cacheKey = $"nonce:{scope}:{nonce}";
        if (_cache.TryGetValue(cacheKey, out _))
        {
            return false;
        }

        using var entry = _cache.CreateEntry(cacheKey);
        entry.Value = true;
        entry.AbsoluteExpirationRelativeToNow = ttl;
        entry.Size = 1;

        return true;
    }
}
