namespace GnosisRealmCore.Models;

public sealed class RealmHeartbeatRequest
{
    public string RealmId { get; set; } = string.Empty;
    public string RealmName { get; set; } = string.Empty;
    public string RealmType { get; set; } = "community"; // official | community
    public string Region { get; set; } = string.Empty;
    public string Status { get; set; } = "online";
    public int CurrentPlayers { get; set; }
    public int MaxPlayers { get; set; }
    public int HealthyZoneCount { get; set; }
    public string? PublicBaseUrl { get; set; }
    public string? Motd { get; set; }
    public string? Version { get; set; }
    public bool Modded { get; set; }
}
