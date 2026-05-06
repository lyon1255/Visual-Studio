namespace GnosisRealmCore.Options;

public sealed class AdminOptions
{
    public const string SectionName = "Admin";
    public bool Enabled { get; set; } = true;
    public string HeaderName { get; set; } = "X-Gnosis-Admin-Key";
    public string ApiKey { get; set; } = string.Empty;
    public string[] AllowedIpAddresses { get; set; } = Array.Empty<string>();
}
