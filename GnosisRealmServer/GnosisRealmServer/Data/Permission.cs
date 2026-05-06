using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GnosisRealmCore.Data
{
    [Table("permissions")]
    public class Permission
    {
        [Key]
        [Column("steam_id")]
        public string SteamId { get; set; } = string.Empty;

        [Column("permission_level")]
        public int PermissionLevel { get; set; } = 0;

        [Column("note")]
        public string? Note { get; set; }
    }
}