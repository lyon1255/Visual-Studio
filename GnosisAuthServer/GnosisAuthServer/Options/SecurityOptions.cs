namespace GnosisAuthServer.Options;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";
    public bool RequireHttps { get; set; } = true;
    public string[] KnownProxies { get; set; } = Array.Empty<string>();
}
