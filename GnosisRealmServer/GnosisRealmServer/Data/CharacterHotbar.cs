using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace GnosisRealmCore.Data
{
    [Table("character_hotbar")]
    public class CharacterHotbar
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("character_id")]
        public int CharacterId { get; set; }

        [Column("slot_index")]
        [JsonPropertyName("slotIndex")]
        public int SlotIndex { get; set; }

        [Column("shortcut_type")]
        [JsonPropertyName("type")]
        public ShortcutType Type { get; set; }

        [Column("shortcut_id")]
        [JsonPropertyName("shortcutID")]
        public string ShortcutId { get; set; } = string.Empty;

        [JsonIgnore]
        public virtual Character? Character { get; set; }
    }
}