using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GnosisRealmCore.Data;

[Table("character_hotbar")]
public sealed class CharacterHotbar
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("character_id")]
    public int CharacterId { get; set; }

    [Column("slot_index")]
    public int SlotIndex { get; set; }

    [Column("shortcut_type")]
    public int ShortcutType { get; set; }

    [Column("shortcut_id")]
    [MaxLength(50)]
    public string ShortcutId { get; set; } = string.Empty;

    public Character? Character { get; set; }
}
