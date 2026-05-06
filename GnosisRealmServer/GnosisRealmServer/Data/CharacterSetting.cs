using System.ComponentModel.DataAnnotations.Schema;

namespace GnosisRealmCore.Data
{
    [Table("character_settings")]
    public class CharacterSetting
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("character_id")]
        public int CharacterId { get; set; }

        [Column("setting_key")]
        public string SettingKey { get; set; } = string.Empty;

        [Column("setting_value")]
        public string SettingValue { get; set; } = string.Empty;
    }
}
