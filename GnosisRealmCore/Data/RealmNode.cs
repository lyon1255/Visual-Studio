using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GnosisRealmCore.Data;

[Table("realm_nodes")]
public sealed class RealmNode
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("node_id")]
    [MaxLength(64)]
    public string NodeId { get; set; } = string.Empty;

    [Column("name")]
    [MaxLength(50)]
    public string Name { get; set; } = "Node";

    [Column("api_url")]
    [MaxLength(255)]
    public string ApiUrl { get; set; } = string.Empty;

    [Column("api_key")]
    [MaxLength(255)]
    public string ApiKey { get; set; } = string.Empty;

    [Column("max_zones")]
    public int MaxZones { get; set; } = 10;

    [Column("active_zones")]
    public int ActiveZones { get; set; }

    [Column("public_ip")]
    [MaxLength(64)]
    public string PublicIp { get; set; } = "127.0.0.1";

    [Column("status")]
    [MaxLength(16)]
    public string Status { get; set; } = "online";

    [Column("last_heartbeat_utc")]
    public DateTime? LastHeartbeatUtc { get; set; }
}
