namespace GnosisAuthServer.Options;

public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    public bool Enabled { get; set; } = true;

    public string HeaderName { get; set; } = "X-Gnosis-Admin-Key";

    public string ApiKey { get; set; } = string.Empty;

    public string[] AllowedIpAddresses { get; set; } = Array.Empty<string>();

    public string[] AllowedIpNetworks { get; set; } = Array.Empty<string>();

    public bool RequireHmac { get; set; } = true;

    public int AllowedClockSkewSeconds { get; set; } = 30;

    public int NonceTtlSeconds { get; set; } = 90;

    public bool RequireExplicitIpAllowlistInProduction { get; set; } = true;
}
