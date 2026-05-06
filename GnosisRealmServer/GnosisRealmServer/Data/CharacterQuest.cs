using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace GnosisRealmCore.Data
{
    [Table("character_quests")]
    public class CharacterQuest
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("character_id")]
        public int CharacterId { get; set; }

        [Column("quest_id")]
        [JsonPropertyName("questId")]
        public string QuestId { get; set; } = string.Empty;

        // Ezt látja a MySQL
        [Column("progress")]
        [JsonIgnore] // Nem akarjuk, hogy a JSON-ben stringként jelenjen meg
        public string ProgressString { get; set; } = "0";

        // Ezt látja a Unity / JSON
        [NotMapped] // Nem mentjük közvetlenül a DB-be, mert a MySQL nem tudja mi ez
        [JsonPropertyName("goalProgress")]
        public int[] GoalProgress { get; set; } = Array.Empty<int>();

        [Column("status")]
        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonIgnore]
        public virtual Character? Character { get; set; }
    }
}