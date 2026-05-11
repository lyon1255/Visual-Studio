using System.ComponentModel.DataAnnotations;

namespace GnosisAuthServer.Models;

public sealed class AdminAccountBanRequest
{
    [Required]
    [MaxLength(32)]
    public string SteamId { get; set; } = string.Empty;

    public bool IsBanned { get; set; }

    [MaxLength(256)]
    public string? BanReason { get; set; }
}
