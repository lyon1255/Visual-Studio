namespace GnosisAuthServer.Options;

public sealed class GameDataOptions
{
    public const string SectionName = "GameData";

    public long ReplaceRequestMaxBodyBytes { get; set; } = 5 * 1024 * 1024;

    public int MaxEntriesPerCollection { get; set; } = 10000;

    public int MaxJsonDataLength { get; set; } = 65536;
}
