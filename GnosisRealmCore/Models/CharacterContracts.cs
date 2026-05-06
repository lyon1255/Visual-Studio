using System.ComponentModel.DataAnnotations;
using GnosisRealmCore.Data;

namespace GnosisRealmCore.Models;

public sealed class CharacterListItemResponse
{
    public int Id { get; set; }
    public string SteamId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int ClassType { get; set; }
    public int Level { get; set; }
    public int Experience { get; set; }
    public long Currency { get; set; }
    public string LastZone { get; set; } = string.Empty;
    public float LastPosX { get; set; }
    public float LastPosY { get; set; }
    public float LastPosZ { get; set; }
    public float LastRotY { get; set; }
    public float CurrentHp { get; set; }
    public float CurrentMp { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastLogout { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class CharacterDetailsResponse : CharacterListItemResponse
{
    public List<CharacterItemDto> Items { get; set; } = new();
    public List<CharacterEquipmentDto> Equipment { get; set; } = new();
    public List<CharacterHotbarDto> Hotbar { get; set; } = new();
    public List<CharacterQuestDto> Quests { get; set; } = new();
    public List<CharacterQuestHistoryDto> QuestHistory { get; set; } = new();
    public List<CharacterSettingDto> Settings { get; set; } = new();
    public List<CharacterSocialDto> Social { get; set; } = new();
    public int PermissionLevel { get; set; }
}

public sealed class CreateCharacterRequest
{
    [Required, MaxLength(32)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 255)]
    public int ClassType { get; set; }

    [MaxLength(64)]
    public string? StartingZone { get; set; }
}

public sealed class SaveCharacterRequest
{
    [Required]
    public CharacterDetailsResponse Character { get; set; } = new();
}

public sealed class CharacterItemDto
{
    public ulong Id { get; set; }
    public int CharacterId { get; set; }
    public ItemContainerType ContainerType { get; set; }
    public int SlotIndex { get; set; }
    public string ItemId { get; set; } = string.Empty;
    public int Amount { get; set; }
    public int ItemLevel { get; set; }
    public int CurrentDurability { get; set; }
    public bool IsBound { get; set; }
    public bool IsLocked { get; set; }
    public byte UpgradeLevel { get; set; }
    public string EnchantId { get; set; } = string.Empty;
    public string TransmogId { get; set; } = string.Empty;
    public string CraftedBy { get; set; } = string.Empty;
}

public sealed class CharacterEquipmentDto
{
    public int Id { get; set; }
    public int CharacterId { get; set; }
    public int SlotType { get; set; }
    public string ItemId { get; set; } = string.Empty;
    public int Amount { get; set; }
}

public sealed class CharacterHotbarDto
{
    public int Id { get; set; }
    public int CharacterId { get; set; }
    public int SlotIndex { get; set; }
    public int ShortcutType { get; set; }
    public string ShortcutId { get; set; } = string.Empty;
}

public sealed class CharacterQuestDto
{
    public int Id { get; set; }
    public int CharacterId { get; set; }
    public string QuestId { get; set; } = string.Empty;
    public string Progress { get; set; } = "0";
    public int Status { get; set; }
    public bool IsDaily { get; set; }
}

public sealed class CharacterQuestHistoryDto
{
    public int Id { get; set; }
    public int CharacterId { get; set; }
    public string QuestId { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
}

public sealed class CharacterSettingDto
{
    public int Id { get; set; }
    public int CharacterId { get; set; }
    public string SettingKey { get; set; } = string.Empty;
    public string SettingValue { get; set; } = string.Empty;
}

public sealed class CharacterSocialDto
{
    public int Id { get; set; }
    public int CharacterId { get; set; }
    public int TargetId { get; set; }
    public int RelationType { get; set; }
}
