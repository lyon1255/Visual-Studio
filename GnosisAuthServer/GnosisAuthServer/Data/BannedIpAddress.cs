using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GnosisAuthServer.Data;

[Index(nameof(IpAddress), IsUnique = true)]
[Table("banned_ip_addresses")]
public sealed class BannedIpAddress
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(64)]
    [Column("ip_address")]
    public string IpAddress { get; set; } = string.Empty;

    [MaxLength(256)]
    [Column("reason")]
    public string? Reason { get; set; }

    [Column("enabled")]
    public bool Enabled { get; set; } = true;

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }

    [Column("expires_at_utc")]
    public DateTime? ExpiresAtUtc { get; set; }
}