using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GnosisAuthServer.Data;

[Table("realms")]
public sealed class Realm
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(64)]
    [Column("realm_id")]
    public string RealmId { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    [Column("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    [Column("region")]
    public string Region { get; set; } = string.Empty;

    [Required]
    [MaxLength(16)]
    [Column("kind")]
    public string Kind { get; set; } = "realmcore";

    [Required]
    [MaxLength(16)]
    [Column("status")]
    public string Status { get; set; } = "offline";

    [Required]
    [MaxLength(256)]
    [Column("public_base_url")]
    public string PublicBaseUrl { get; set; } = string.Empty;

    [Column("current_players")]
    public int CurrentPlayers { get; set; }

    [Column("max_players")]
    public int MaxPlayers { get; set; }

    [Column("healthy_zone_count")]
    public int HealthyZoneCount { get; set; }

    [Column("is_listed")]
    public bool IsListed { get; set; } = true;

    [Column("is_official")]
    public bool IsOfficial { get; set; } = true;

    [Column("last_heartbeat_at")]
    public DateTime? LastHeartbeatAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [Required]
    [MaxLength(32)]
    [Column("realm_type")]
    public string RealmType { get; set; } = "official";

    [MaxLength(256)]
    [Column("motd")]
    public string? Motd { get; set; }

    [MaxLength(64)]
    [Column("version")]
    public string? Version { get; set; }

    [Column("modded")]
    public bool Modded { get; set; }

    [Column("enabled")]
    public bool Enabled { get; set; } = true;
}