namespace GnosisAuthServer.Options;

public sealed class SchemaDeliveryOptions
{
    public const string SectionName = "SchemaDelivery";

    public bool Enabled { get; set; } = true;

    public string DirectoryPath { get; set; } = "SchemaMigrations/realmcore";

    public string Channel { get; set; } = "realmcore";
}