namespace GnosisAuthServer.Options;

public sealed class NonceStoreOptions
{
    public const string SectionName = "NonceStore";

    public bool UseDistributedCache { get; set; }

    public string RedisConnectionString { get; set; } = string.Empty;

    public string RedisInstanceName { get; set; } = "gnosis-auth";
}
