using System.ComponentModel.DataAnnotations;

namespace GnosisAuthServer.Models;

public sealed class SteamLoginRequest
{
    [Required]
    [MaxLength(32)]
    public string SteamId { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(4096)]
    public string Ticket { get; set; } = string.Empty;
}

public sealed class SteamLoginResponse
{
    public int AccountId { get; set; }
    public string AccessToken { get; set; } = string.Empty;
    public int ExpiresInSeconds { get; set; }
    public string SteamId { get; set; } = string.Empty;
}

public sealed class AuthMeResponse
{
    public int AccountId { get; set; }
    public string SteamId { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public bool IsBanned { get; set; }
}
