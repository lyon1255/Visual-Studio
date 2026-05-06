using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GnosisRealmCore.Data;

[Table("character_equipment")]
public sealed class CharacterEquipment
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("character_id")]
    public int CharacterId { get; set; }

    [Column("slot_type")]
    public int SlotType { get; set; }

    [Column("item_id")]
    [MaxLength(255)]
    public string ItemId { get; set; } = string.Empty;

    [Column("amount")]
    public int Amount { get; set; } = 1;

    public Character? Character { get; set; }
}
