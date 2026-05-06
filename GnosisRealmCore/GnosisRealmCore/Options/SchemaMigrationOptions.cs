namespace GnosisRealmCore.Options;

public sealed class SchemaMigrationOptions
{
    public const string SectionName = "SchemaMigration";

    public bool Enabled { get; set; } = true;

    public bool AllowDestructiveMigrations { get; set; } = false;

    public string Channel { get; set; } = "realmcore";
}