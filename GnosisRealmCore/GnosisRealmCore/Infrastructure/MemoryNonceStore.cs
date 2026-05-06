using Microsoft.Extensions.Caching.Memory;

namespace GnosisRealmCore.Infrastructure;

public sealed class MemoryNonceStore : INonceStore
{
    private readonly IMemoryCache _cache;

    public MemoryNonceStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public bool TryUseNonce(string scope, string nonce, TimeSpan ttl)
    {
        var key = $"nonce:{scope}:{nonce}";
        if (_cache.TryGetValue(key, out _))
        {
            return false;
        }

        _cache.Set(key, true, ttl);
        return true;
    }
}
