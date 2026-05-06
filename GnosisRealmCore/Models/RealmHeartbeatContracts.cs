namespace GnosisRealmCore.Models;

public sealed class OfficialRealmHeartbeatRequest
{
    public string RealmId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string PublicBaseUrl { get; set; } = string.Empty;
    public string Status { get; set; } = "online";
    public int CurrentPlayers { get; set; }
    public int MaxPlayers { get; set; }
    public int HealthyZoneCount { get; set; }
}
