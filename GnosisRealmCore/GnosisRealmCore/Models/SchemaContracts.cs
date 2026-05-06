namespace GnosisRealmCore.Models;

public sealed class SchemaManifestResponse
{
    public string Channel { get; set; } = string.Empty;
    public string LatestMigrationId { get; set; } = string.Empty;
    public int MigrationCount { get; set; }
    public DateTime PublishedAtUtc { get; set; }
    public List<SchemaMigrationDescriptorResponse> Migrations { get; set; } = new();
}

public sealed class SchemaMigrationDescriptorResponse
{
    public string Id { get; set; } = string.Empty;
    public string ChecksumSha256 { get; set; } = string.Empty;
    public bool IsDestructive { get; set; }
}

public sealed class SchemaMigrationContentResponse
{
    public string Id { get; set; } = string.Empty;
    public string ChecksumSha256 { get; set; } = string.Empty;
    public bool IsDestructive { get; set; }
    public string Sql { get; set; } = string.Empty;
}