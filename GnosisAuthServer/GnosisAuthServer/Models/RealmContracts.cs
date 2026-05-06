using System.ComponentModel.DataAnnotations;

namespace GnosisAuthServer.Models;

public sealed class RealmListItemResponse
{
    public string RealmId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string PublicBaseUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int CurrentPlayers { get; set; }
    public int MaxPlayers { get; set; }
    public int HealthyZoneCount { get; set; }
}

public sealed class OfficialRealmHeartbeatRequest
{
    [Required]
    [MaxLength(64)]
    public string RealmId { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string Region { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string PublicBaseUrl { get; set; } = string.Empty;

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

public sealed class OfficialRealmHeartbeatResponse
{
    public string RealmId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class AdminRealmUpsertRequest
{
    [Required]
    [MaxLength(64)]
    public string RealmId { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string Region { get; set; } = string.Empty;

    [Required]
    [MaxLength(16)]
    public string Kind { get; set; } = "official";

    [Required]
    [MaxLength(256)]
    public string PublicBaseUrl { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int MaxPlayers { get; set; }

    public bool IsListed { get; set; } = true;
    public bool IsOfficial { get; set; } = true;
}
