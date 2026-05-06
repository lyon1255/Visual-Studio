namespace GnosisRealmCore.Options;

public sealed class LegacyNodeAuthOptions
{
    public const string SectionName = "LegacyNodeAuth";
    public bool Enabled { get; set; } = true;
    public string HeaderName { get; set; } = "X-Server-Admin-Key";
    public string ApiKey { get; set; } = string.Empty;
}
