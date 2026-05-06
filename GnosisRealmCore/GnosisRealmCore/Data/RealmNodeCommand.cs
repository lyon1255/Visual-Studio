using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GnosisRealmCore.Data;

[Table("realm_node_commands")]
public sealed class RealmNodeCommand
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("node_id")]
    [MaxLength(64)]
    public string NodeId { get; set; } = string.Empty;

    [Column("command_type")]
    [MaxLength(32)]
    public string CommandType { get; set; } = string.Empty;

    [Column("payload_json")]
    public string PayloadJson { get; set; } = "{}";

    [Column("status")]
    [MaxLength(16)]
    public string Status { get; set; } = "pending";

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("claimed_at_utc")]
    public DateTime? ClaimedAtUtc { get; set; }

    [Column("completed_at_utc")]
    public DateTime? CompletedAtUtc { get; set; }

    [Column("error_text")]
    [MaxLength(1024)]
    public string? ErrorText { get; set; }
}
