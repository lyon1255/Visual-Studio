namespace GnosisRealmCore.Options;

public sealed class JwtValidationOptions
{
    public const string SectionName = "JwtValidation";
    public string PublicKeyPemPath { get; set; } = string.Empty;
    public string Issuer { get; set; } = "Gnosis.Auth";
    public string Audience { get; set; } = "Gnosis.Clients";
    public int ClockSkewSeconds { get; set; } = 30;
}
