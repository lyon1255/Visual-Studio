using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GnosisRealmCore.Data;

[Table("permissions")]
public sealed class Permission
{
    [Key]
    [Column("steam_id")]
    [MaxLength(25)]
    public string SteamId { get; set; } = string.Empty;

    [Column("permission_level")]
    public int PermissionLevel { get; set; }

    [Column("note")]
    [MaxLength(255)]
    public string? Note { get; set; }
}
