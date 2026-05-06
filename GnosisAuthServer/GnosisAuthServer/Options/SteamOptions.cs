namespace GnosisAuthServer.Options;

public sealed class SteamOptions
{
    public const string SectionName = "Steam";
    public bool Enabled { get; set; } = true;
    public uint AppId { get; set; }
    public string PublisherKey { get; set; } = string.Empty;
    public bool AllowMockTicketsInDevelopment { get; set; }
}
