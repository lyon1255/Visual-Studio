namespace GnosisAuthServer.Options;

public sealed class RealmRegistryOptions
{
    public const string SectionName = "RealmRegistry";
    public int HeartbeatTimeoutSeconds { get; set; } = 90;
    public bool HideUnhealthyRealms { get; set; } = true;
}
