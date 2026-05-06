namespace GnosisRealmCore.Options;

public sealed class ServiceAuthOptions
{
    public const string SectionName = "ServiceAuth";
    public bool Enabled { get; set; } = true;
    public int AllowedClockSkewSeconds { get; set; } = 30;
    public int NonceTtlSeconds { get; set; } = 90;
    public List<ServiceClientOptions> Clients { get; set; } = new();
}

public sealed class ServiceClientOptions
{
    public string ServiceId { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public string[] Roles { get; set; } = Array.Empty<string>();
    public string[] AllowedRealmIds { get; set; } = Array.Empty<string>();
}
