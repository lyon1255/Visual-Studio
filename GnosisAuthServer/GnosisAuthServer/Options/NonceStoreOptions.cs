namespace GnosisAuthServer.Options;

public sealed class NonceStoreOptions
{
    public const string SectionName = "NonceStore";

    public bool UseDistributedCache { get; set; }
}
