using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GnosisRealmCore.Data;

[Table("realm_game_data_overrides")]
public sealed class RealmGameDataOverride
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("category")]
    [MaxLength(32)]
    public string Category { get; set; } = string.Empty;

    [Column("asset_id")]
    [MaxLength(128)]
    public string AssetId { get; set; } = string.Empty;

    [Column("class_type")]
    [MaxLength(128)]
    public string ClassType { get; set; } = string.Empty;

    [Column("json_data")]
    public string JsonData { get; set; } = "{}";

    [Column("override_action")]
    [MaxLength(16)]
    public string OverrideAction { get; set; } = "override";

    [Column("is_enabled")]
    public bool IsEnabled { get; set; } = true;

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
