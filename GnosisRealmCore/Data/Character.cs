using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace GnosisRealmCore.Data;

[Table("characters")]
public sealed class Character
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("steam_id")]
    [MaxLength(50)]
    public string SteamId { get; set; } = string.Empty;

    [Column("name")]
    [MaxLength(32)]
    public string Name { get; set; } = string.Empty;

    [Column("is_banned")]
    public bool IsBanned { get; set; }

    [Column("currency")]
    public long Currency { get; set; }

    [Column("class_type")]
    public int ClassType { get; set; }

    [Column("level")]
    public int Level { get; set; } = 1;

    [Column("experience")]
    public int Experience { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("current_hp")]
    public float CurrentHp { get; set; } = 100;

    [Column("current_mp")]
    public float CurrentMp { get; set; } = 100;

    [Column("last_zone")]
    [MaxLength(50)]
    public string LastZone { get; set; } = "City";

    [Column("last_pos_x")]
    public float LastPosX { get; set; }

    [Column("last_pos_y")]
    public float LastPosY { get; set; }

    [Column("last_pos_z")]
    public float LastPosZ { get; set; }

    [Column("last_rot_y")]
    public float LastRotY { get; set; }

    [Column("is_online")]
    public bool IsOnline { get; set; }

    [Column("last_logout")]
    public DateTime? LastLogout { get; set; }

    [Column("guild_id")]
    public int? GuildId { get; set; }

    public Guild? Guild { get; set; }

    public ICollection<CharacterItem> Items { get; set; } = new List<CharacterItem>();
    public ICollection<CharacterEquipment> Equipment { get; set; } = new List<CharacterEquipment>();
    public ICollection<CharacterHotbar> Hotbar { get; set; } = new List<CharacterHotbar>();
    public ICollection<CharacterQuest> Quests { get; set; } = new List<CharacterQuest>();
    public ICollection<CharacterQuestHistory> QuestHistory { get; set; } = new List<CharacterQuestHistory>();
    public ICollection<CharacterSetting> Settings { get; set; } = new List<CharacterSetting>();
    public ICollection<CharacterSocial> Social { get; set; } = new List<CharacterSocial>();
}
