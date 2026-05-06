using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GnosisRealmCore.Data;

[Table("character_quests")]
public sealed class CharacterQuest
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("character_id")]
    public int CharacterId { get; set; }

    [Column("quest_id")]
    [MaxLength(255)]
    public string QuestId { get; set; } = string.Empty;

    [Column("progress")]
    [MaxLength(255)]
    public string Progress { get; set; } = "0";

    [Column("status")]
    public int Status { get; set; }

    [Column("is_daily")]
    public bool IsDaily { get; set; }

    public Character? Character { get; set; }
}
