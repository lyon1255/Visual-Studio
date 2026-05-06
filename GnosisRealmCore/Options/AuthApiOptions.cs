namespace GnosisRealmCore.Options;

public sealed class AuthApiOptions
{
    public const string SectionName = "AuthApi";
    public string BaseUrl { get; set; } = string.Empty;
    public string ServiceId { get; set; } = string.Empty;
    public string ServiceSecret { get; set; } = string.Empty;
    public int HeartbeatIntervalSeconds { get; set; } = 30;
    public int GameDataRefreshMinutes { get; set; } = 5;
}
