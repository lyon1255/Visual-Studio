using System.ComponentModel.DataAnnotations;

namespace GnosisRealmCore.Models;

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

public sealed class GlobalGameDataVersionResponse
{
    public int VersionNumber { get; set; }
    public string VersionTag { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public DateTime PublishedAtUtc { get; set; }
}

public sealed class GlobalGameDataSnapshotResponse
{
    public int VersionNumber { get; set; }
    public string VersionTag { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public DateTime PublishedAtUtc { get; set; }
    public List<GameDataEntryDto> Items { get; set; } = new();
    public List<GameDataEntryDto> Entities { get; set; } = new();
    public List<GameDataEntryDto> Quests { get; set; } = new();
    public List<GameDataEntryDto> Spells { get; set; } = new();
    public List<GameDataEntryDto> Auras { get; set; } = new();
}

public sealed class RealmGameDataVersionResponse : GlobalGameDataVersionResponse
{
    public DateTime CachedAtUtc { get; set; }
}

public sealed class RealmGameDataSnapshotResponse : GlobalGameDataSnapshotResponse
{
    public DateTime CachedAtUtc { get; set; }
    public int OverrideCount { get; set; }
}
