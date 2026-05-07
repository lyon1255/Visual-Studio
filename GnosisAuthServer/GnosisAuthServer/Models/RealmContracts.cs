using System.ComponentModel.DataAnnotations;

namespace GnosisAuthServer.Models;

public sealed class AdminRealmUpsertRequest
{
    [Required]
    [MaxLength(64)]
    public string RealmId { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string Region { get; set; } = string.Empty;

    [Required]
    [MaxLength(16)]
    public string Kind { get; set; } = "realmcore";

    [Required]
    [MaxLength(256)]
    public string PublicBaseUrl { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int MaxPlayers { get; set; }

    public bool IsListed { get; set; } = true;

    public bool IsOfficial { get; set; } = true;

    public bool Enabled { get; set; } = true;
}
