using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace GnosisRealmCore.Data
{
    [Table("character_quest_history")]
    public class CharacterQuestHistory
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("character_id")]
        public int CharacterId { get; set; }

        [Column("quest_id")]
        [JsonPropertyName("questId")]
        public string QuestId { get; set; } = string.Empty;

        [JsonIgnore]
        public virtual Character? Character { get; set; }
    }
}