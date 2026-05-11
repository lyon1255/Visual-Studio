using StackExchange.Redis;

namespace GnosisAuthServer.Infrastructure;

public sealed class RedisNonceStore : INonceStore
{
    private static readonly RedisValue Marker = 1;
    private readonly IDatabase _database;
    private readonly string _instanceName;

    public RedisNonceStore(IConnectionMultiplexer connectionMultiplexer, string instanceName)
    {
        _database = connectionMultiplexer.GetDatabase();
        _instanceName = string.IsNullOrWhiteSpace(instanceName) ? "gnosis-auth" : instanceName.Trim();
    }

    public bool TryUseNonce(string scope, string nonce, TimeSpan ttl)
    {
        var cacheKey = BuildKey(scope, nonce);
        return _database.StringSet(cacheKey, Marker, ttl, when: When.NotExists);
    }

    private RedisKey BuildKey(string scope, string nonce)
        => $"{_instanceName}:nonce:{scope}:{nonce}";
}
