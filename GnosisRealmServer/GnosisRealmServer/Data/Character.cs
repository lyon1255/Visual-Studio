using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace GnosisRealmCore.Data
{
    [Table("characters")]
    public class Character
    {
        [Column("id")]
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [Column("steam_id")]
        [JsonPropertyName("steam_id")]
        public string SteamId { get; set; } = string.Empty;

        [Column("name")]
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [Column("class_type")]
        [JsonPropertyName("classType")]
        public int ClassType { get; set; }

        [Column("level")]
        [JsonPropertyName("level")]
        public int Level { get; set; }

        [Column("experience")]
        [JsonPropertyName("experience")]
        public int Experience { get; set; }

        // --- BASE STATS JSON TÖRÖLVE! ---

        [Column("current_hp")]
        [JsonPropertyName("currentHp")]
        public float CurrentHp { get; set; }

        [Column("current_mp")]
        [JsonPropertyName("currentMp")]
        public float CurrentMp { get; set; }

        [Column("last_pos_x")]
        [JsonPropertyName("lastPosX")]
        public float LastPosX { get; set; }

        [Column("last_pos_y")]
        [JsonPropertyName("lastPosY")]
        public float LastPosY { get; set; }

        [Column("last_pos_z")]
        [JsonPropertyName("lastPosZ")]
        public float LastPosZ { get; set; }

        [Column("last_rot_y")]
        [JsonPropertyName("lastRotY")]
        public float LastRotY { get; set; }

        [Column("last_zone")]
        [JsonPropertyName("lastZone")]
        public string? LastZone { get; set; }

        [Column("last_logout")]
        [JsonPropertyName("lastLogout")]
        public DateTime? LastLogout { get; set; }

        [Column("currency")]
        [JsonPropertyName("currency")]
        public long Currency { get; set; }

        [JsonPropertyName("items")]
        public virtual ICollection<CharacterItem> Items { get; set; } = new List<CharacterItem>();
        [JsonPropertyName("hotbar")]
        public virtual ICollection<CharacterHotbar> Hotbar { get; set; } = new List<CharacterHotbar>();
        [JsonPropertyName("quests")]
        public virtual ICollection<CharacterQuest> Quests { get; set; } = new List<CharacterQuest>();
        public virtual ICollection<CharacterQuestHistory> QuestHistory { get; set; } = new List<CharacterQuestHistory>();
        [JsonPropertyName("settings")]
        public virtual ICollection<CharacterSetting> Settings { get; set; } = new List<CharacterSetting>();
        [JsonPropertyName("social")]
        public virtual ICollection<CharacterSocial> Social { get; set; } = new List<CharacterSocial>();

        [Column("guild_id")]
        [JsonPropertyName("guildId")]
        public int? GuildId { get; set; }
        [JsonIgnore]
        public virtual Guild? Guild { get; set; }
        [NotMapped]
        [JsonPropertyName("completedQuestHistory")]
        public List<string> CompletedQuestHistoryIds { get; set; } = new List<string>();
        [NotMapped]
        [JsonPropertyName("permissionLevel")]
        public int PermissionLevel { get; set; } = 0;

        [Column("is_online")]
        public int IsOnline { get; set; } = 0;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}