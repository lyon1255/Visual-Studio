namespace GnosisAuthServer.Models;

public sealed class RealmListItemResponse
{
    public string RealmId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string RealmType { get; set; } = string.Empty;   // official | community
    public string Kind { get; set; } = string.Empty;        // legacy / tech-kind
    public string PublicBaseUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int CurrentPlayers { get; set; }
    public int MaxPlayers { get; set; }
    public int HealthyZoneCount { get; set; }
    public bool Modded { get; set; }
    public string? Motd { get; set; }
    public string? Version { get; set; }
    public DateTime? LastHeartbeatAtUtc { get; set; }
}
