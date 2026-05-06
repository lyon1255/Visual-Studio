namespace GnosisRealmCore.Models;

public sealed class AuthUserContext
{
    public string SteamId { get; set; } = string.Empty;
    public int? AccountId { get; set; }
}
