using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GnosisAuthServer.Data;

[Table("accounts")]
public sealed class Account
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(32)]
    [Column("steam_id")]
    public string SteamId { get; set; } = string.Empty;

    [Column("is_banned")]
    public bool IsBanned { get; set; }

    [MaxLength(256)]
    [Column("ban_reason")]
    public string? BanReason { get; set; }

    [MaxLength(32)]
    [Column("account_type")]
    public string AccountType { get; set; } = "player";

    [Column("created_at")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("last_login_at")]
    public DateTime? LastLoginAtUtc { get; set; }
}
