using System.ComponentModel.DataAnnotations;

namespace GnosisAuthServer.Models;

public sealed class GameDataEntryDto
{
    [Required]
    [MaxLength(128)]
    public string AssetId { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string ClassType { get; set; } = string.Empty;

    [Required]
    public string JsonData { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;
}

public sealed class ReplaceGlobalGameDataRequest
{
    [MaxLength(64)]
    public string VersionTag { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? Notes { get; set; }

    public List<GameDataEntryDto> Items { get; set; } = [];
    public List<GameDataEntryDto> Entities { get; set; } = [];
    public List<GameDataEntryDto> Quests { get; set; } = [];
    public List<GameDataEntryDto> Spells { get; set; } = [];
    public List<GameDataEntryDto> Auras { get; set; } = [];
}

public sealed class GlobalGameDataSnapshotResponse
{
    public int VersionNumber { get; set; }
    public string VersionTag { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public DateTime PublishedAtUtc { get; set; }
    public List<GameDataEntryDto> Prefabs { get; set; } = [];
    public List<GameDataEntryDto> Items { get; set; } = [];
    public List<GameDataEntryDto> Entities { get; set; } = [];
    public List<GameDataEntryDto> Quests { get; set; } = [];
    public List<GameDataEntryDto> Spells { get; set; } = [];
    public List<GameDataEntryDto> Auras { get; set; } = [];
}

public sealed class GlobalGameDataVersionResponse
{
    public int VersionNumber { get; set; }
    public string VersionTag { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public DateTime PublishedAtUtc { get; set; }
}

public sealed class GlobalPrefabRegistryResponse
{
    public int VersionNumber { get; set; }
    public string VersionTag { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public DateTime PublishedAtUtc { get; set; }
    public List<GameDataEntryDto> Prefabs { get; set; } = [];
}
