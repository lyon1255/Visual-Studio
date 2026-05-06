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
    public string Kind { get; set; } = "official";

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
    public DateTime? LastHeartbeatAtUtc { get; set; }

    [Column("created_at")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
