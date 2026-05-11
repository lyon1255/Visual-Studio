namespace GnosisAuthServer.Options;

public sealed class AccountAccessOptions
{
    public const string SectionName = "AccountAccess";

    public int CacheTtlSeconds { get; set; } = 30;
}
