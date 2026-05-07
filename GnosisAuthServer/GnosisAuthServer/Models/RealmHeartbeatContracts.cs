using System.ComponentModel.DataAnnotations;

namespace GnosisAuthServer.Models;

public sealed class RealmHeartbeatRequest
{
    [Required]
    [MaxLength(64)]
    public string RealmId { get; set; } = string.Empty;

    [Required]
    [MaxLength(16)]
    public string Status { get; set; } = "online";

    [Range(0, int.MaxValue)]
    public int CurrentPlayers { get; set; }

    [Range(0, int.MaxValue)]
    public int MaxPlayers { get; set; }

    [Range(0, int.MaxValue)]
    public int HealthyZoneCount { get; set; }
}