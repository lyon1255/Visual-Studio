using System.ComponentModel.DataAnnotations;

namespace GnosisAuthServer.Models;

public sealed class RealmHeartbeatRequest
{
    [Required]
    [MaxLength(64)]
    public string RealmId { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string RealmName { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string RealmType { get; set; } = string.Empty; // official | community

    [Required]
    [MaxLength(32)]
    public string Region { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string Status { get; set; } = "online"; // online | degraded | offline

    [Range(0, int.MaxValue)]
    public int CurrentPlayers { get; set; }

    [Range(0, int.MaxValue)]
    public int MaxPlayers { get; set; }

    [Range(0, int.MaxValue)]
    public int HealthyZoneCount { get; set; }

    [MaxLength(512)]
    public string? PublicBaseUrl { get; set; }

    [MaxLength(256)]
    public string? Motd { get; set; }

    [MaxLength(64)]
    public string? Version { get; set; }

    public bool Modded { get; set; }
}