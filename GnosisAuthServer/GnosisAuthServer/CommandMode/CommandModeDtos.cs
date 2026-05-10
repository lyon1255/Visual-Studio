namespace GnosisAuthServer.CommandMode;

internal sealed class ServiceImportDocument
{
    public List<ServiceImportClient> Clients { get; set; } = new();
}

internal sealed class ServiceImportClient
{
    public string ServiceId { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public string[] AllowedRealmIds { get; set; } = Array.Empty<string>();
}