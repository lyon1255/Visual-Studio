using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GnosisAuthServer.Data
{
    // --- KÖZÖS ŐS A JÁTÉKADATOKNAK ---
    public abstract class GameDataBaseEntity
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        [Column("asset_id")]
        public string AssetId { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [Column("class_type")]
        public string ClassType { get; set; } = string.Empty; // Pl. "ConsumableItem", "PointBlankAoESpell"

        [Column("json_data", TypeName = "LONGTEXT")]
        public string JsonData { get; set; } = string.Empty;

        [Column("last_updated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    [Table("data_items")] public class DbItem : GameDataBaseEntity { }
    [Table("data_entities")] public class DbEntity : GameDataBaseEntity { }
    [Table("data_quests")] public class DbQuest : GameDataBaseEntity { }
    [Table("data_spells")] public class DbSpell : GameDataBaseEntity { }
    [Table("data_auras")] public class DbAura : GameDataBaseEntity { }
}