using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Gnosis.AuthServer.Domain.Entities
{
    [Table("realm_nodes")]
    public sealed class RealmNode
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [MaxLength(64)]
        [Column("realm_id")]
        public string RealmId { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(32)]
        [Column("region")]
        public string Region { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        [Column("public_base_url")]
        public string PublicBaseUrl { get; set; } = string.Empty;

        [MaxLength(255)]
        [Column("internal_base_url")]
        public string? InternalBaseUrl { get; set; }

        [Required]
        [MaxLength(255)]
        [Column("service_secret_hash")]
        public string ServiceSecretHash { get; set; } = string.Empty;

        [Column("enabled")]
        public bool Enabled { get; set; } = true;

        [Required]
        [MaxLength(32)]
        [Column("status")]
        public string Status { get; set; } = "offline";

        [Column("current_players")]
        public int CurrentPlayers { get; set; }

        [Column("max_players")]
        public int MaxPlayers { get; set; }

        [MaxLength(32)]
        [Column("build_version")]
        public string? BuildVersion { get; set; }

        [Column("protocol_version")]
        public int ProtocolVersion { get; set; } = 1;

        [Column("last_heartbeat_utc")]
        public DateTime? LastHeartbeatUtc { get; set; }

        [Column("created_at_utc")]
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [Column("updated_at_utc")]
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}