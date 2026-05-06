using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GnosisRealmCore.Data;

public enum ItemContainerType
{
    Inventory = 0,
    Equipment = 1,
    Bank = 2
}

[Table("character_items")]
public sealed class CharacterItem
{
    [Key]
    [Column("id")]
    public ulong Id { get; set; }

    [Column("character_id")]
    public int CharacterId { get; set; }

    [Column("container_type")]
    public ItemContainerType ContainerType { get; set; }

    [Column("slot_index")]
    public int SlotIndex { get; set; }

    [Column("item_id")]
    [MaxLength(50)]
    public string ItemId { get; set; } = string.Empty;

    [Column("amount")]
    public int Amount { get; set; } = 1;

    [Column("item_level")]
    public int ItemLevel { get; set; } = 1;

    [Column("current_durability")]
    public int CurrentDurability { get; set; }

    [Column("is_bound")]
    public bool IsBound { get; set; }

    [Column("is_locked")]
    public bool IsLocked { get; set; }

    [Column("upgrade_level")]
    public byte UpgradeLevel { get; set; }

    [Column("enchant_id")]
    [MaxLength(255)]
    public string EnchantId { get; set; } = string.Empty;

    [Column("transmog_id")]
    [MaxLength(255)]
    public string TransmogId { get; set; } = string.Empty;

    [Column("crafted_by")]
    [MaxLength(255)]
    public string CraftedBy { get; set; } = string.Empty;

    public Character? Character { get; set; }
}
