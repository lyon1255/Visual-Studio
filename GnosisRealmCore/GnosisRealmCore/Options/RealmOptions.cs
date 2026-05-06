namespace GnosisRealmCore.Options;

public sealed class RealmOptions
{
    public const string SectionName = "Realm";
    public string RealmId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Kind { get; set; } = "community";
    public string PublicBaseUrl { get; set; } = string.Empty;
    public int MaxPlayers { get; set; } = 500;
    public int ZoneStartupPollSeconds { get; set; } = 15;
    public int OfflineWhenNoHealthyZonesAfterSeconds { get; set; } = 180;
}
