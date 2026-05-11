using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GnosisAuthServer.Data;

public abstract class GameDataBaseEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("version_number")]
    public int VersionNumber { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("asset_id")]
    public string AssetId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("class_type")]
    public string ClassType { get; set; } = string.Empty;

    [Column("json_data", TypeName = "LONGTEXT")]
    public string JsonData { get; set; } = string.Empty;

    [Column("is_enabled")]
    public bool IsEnabled { get; set; } = true;

    [Column("last_updated")]
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}

[Table("gamedata_items")]
public sealed class DbItem : GameDataBaseEntity { }

[Table("gamedata_entities")]
public sealed class DbEntity : GameDataBaseEntity { }

[Table("gamedata_quests")]
public sealed class DbQuest : GameDataBaseEntity { }

[Table("gamedata_spells")]
public sealed class DbSpell : GameDataBaseEntity { }

[Table("gamedata_auras")]
public sealed class DbAura : GameDataBaseEntity { }

[Table("gamedata_versions")]
public sealed class GameDataVersion
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("version_number")]
    public int VersionNumber { get; set; }

    [Required]
    [MaxLength(64)]
    [Column("version_tag")]
    public string VersionTag { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    [Column("content_hash")]
    public string ContentHash { get; set; } = string.Empty;

    [Column("is_active")]
    public bool IsActive { get; set; }

    [MaxLength(512)]
    [Column("notes")]
    public string? Notes { get; set; }

    [Column("published_at")]
    public DateTime PublishedAtUtc { get; set; } = DateTime.UtcNow;
}
