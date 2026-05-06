using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GnosisRealmCore.Data;

[Table("realm_zone_instances")]
public sealed class RealmZoneInstance
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("zone_name")]
    [MaxLength(64)]
    public string ZoneName { get; set; } = string.Empty;

    [Column("node_id")]
    [MaxLength(64)]
    public string NodeId { get; set; } = string.Empty;

    [Column("ip_address")]
    [MaxLength(64)]
    public string IpAddress { get; set; } = "127.0.0.1";

    [Column("port")]
    public int Port { get; set; }

    [Column("status")]
    [MaxLength(16)]
    public string Status { get; set; } = "starting";

    [Column("current_players")]
    public int CurrentPlayers { get; set; }

    [Column("max_players")]
    public int MaxPlayers { get; set; }

    [Column("started_at_utc")]
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("last_heartbeat_utc")]
    public DateTime? LastHeartbeatUtc { get; set; }
}
