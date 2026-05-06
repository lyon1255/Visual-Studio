using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GnosisRealmCore.Data;

[Table("character_quest_history")]
public sealed class CharacterQuestHistory
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("character_id")]
    public int CharacterId { get; set; }

    [Column("quest_id")]
    [MaxLength(255)]
    public string QuestId { get; set; } = string.Empty;

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    public Character? Character { get; set; }
}
