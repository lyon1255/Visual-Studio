namespace GnosisRealmCore.Options;

public sealed class SchemaMigrationOptions
{
    public const string SectionName = "SchemaMigrations";
    public bool Enabled { get; set; } = true;
    public string DirectoryPath { get; set; } = "SchemaMigrations";
    public bool AllowDestructiveMigrations { get; set; } = true;
}
