using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace GnosisRealmCore.Data
{
    public enum ItemContainerType
    {
        Inventory = 0,
        Equipment = 1,
        Bank = 2
    }

    [Table("character_items")]
    public class CharacterItem
    {
        [Column("id")]
        public ulong Id { get; set; }

        [Column("character_id")]
        public int CharacterId { get; set; }

        [Column("container_type")]
        [JsonPropertyName("containerType")]
        public ItemContainerType ContainerType { get; set; }

        [Column("slot_index")]
        [JsonPropertyName("slotIndex")]
        public int SlotIndex { get; set; }

        [Column("item_id")]
        [JsonPropertyName("itemId")]
        public string ItemId { get; set; } = string.Empty;

        [Column("amount")]
        [JsonPropertyName("amount")]
        public int Amount { get; set; }

        // --- HIÁNYZÓ ADAT PÓTOLVA ---
        [Column("item_level")]
        [JsonPropertyName("itemLevel")]
        public int ItemLevel { get; set; }

        [Column("current_durability")]
        [JsonPropertyName("currentDurability")]
        public int CurrentDurability { get; set; }

        [Column("is_bound")]
        [JsonPropertyName("isBound")]
        public bool IsBound { get; set; }

        [Column("is_locked")]
        [JsonPropertyName("isLocked")]
        public bool IsLocked { get; set; }

        [Column("upgrade_level")]
        [JsonPropertyName("upgradeLevel")]
        public byte UpgradeLevel { get; set; }

        [Column("enchant_id")]
        [JsonPropertyName("enchantId")]
        public string EnchantId { get; set; } = string.Empty;

        [Column("transmog_id")]
        [JsonPropertyName("transmogId")]
        public string TransmogId { get; set; } = string.Empty;

        [Column("crafted_by")]
        [JsonPropertyName("craftedBy")]
        public string CraftedBy { get; set; } = string.Empty;

        [JsonIgnore]
        public virtual Character? Character { get; set; }
    }
}