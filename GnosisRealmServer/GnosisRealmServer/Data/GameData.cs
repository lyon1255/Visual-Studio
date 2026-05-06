using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GnosisRealmCore.Data
{
    [Table("server_rates")]
    public class ServerRate
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("xp_multiplier")]
        public float XpMultiplier { get; set; }

        [Column("drop_rate_multiplier")]
        public float DropRateMultiplier { get; set; }

        [Column("gold_multiplier")]
        public float GoldMultiplier { get; set; }
    }

    [Table("class_stats")]
    public class ClassStat
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)] // Mivel mi adjuk meg az ID-t (0, 1, 2, 3)
        [Column("class_type")]
        public int ClassType { get; set; }

        [Column("class_name")]
        public string ClassName { get; set; } = string.Empty;

        [Column("base_max_health")] public float BaseMaxHealth { get; set; }
        [Column("base_max_mana")] public float BaseMaxMana { get; set; }
        [Column("base_ad")] public float BaseAD { get; set; }
        [Column("base_ap")] public float BaseAP { get; set; }
        [Column("base_defense")] public float BaseDefense { get; set; }
        [Column("base_attack_speed")] public float BaseAttackSpeed { get; set; }
        [Column("base_crit_chance")] public float BaseCritChance { get; set; }
        [Column("hp_per_level")] public float HpPerLevel { get; set; }
        [Column("mana_per_level")] public float ManaPerLevel { get; set; }
        [Column("ad_per_level")] public float AdPerLevel { get; set; }
        [Column("ap_per_level")] public float ApPerLevel { get; set; }
    }
}