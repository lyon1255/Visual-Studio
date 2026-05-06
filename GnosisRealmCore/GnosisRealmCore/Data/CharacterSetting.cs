using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GnosisRealmCore.Data;

[Table("character_settings")]
public sealed class CharacterSetting
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("character_id")]
    public int CharacterId { get; set; }

    [Column("setting_key")]
    [MaxLength(100)]
    public string SettingKey { get; set; } = string.Empty;

    [Column("setting_value")]
    public string SettingValue { get; set; } = string.Empty;

    public Character? Character { get; set; }
}
