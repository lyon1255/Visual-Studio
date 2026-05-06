namespace GnosisRealmCore.Options;

public sealed class GameDataCacheOptions
{
    public const string SectionName = "GameDataCache";
    public bool WarmOnStartup { get; set; } = true;
    public bool AllowStaleCacheWhenAuthApiIsUnavailable { get; set; } = true;
}
