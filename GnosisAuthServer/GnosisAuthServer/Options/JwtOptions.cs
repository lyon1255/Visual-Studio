namespace GnosisAuthServer.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string PrivateKeyPemPath { get; set; } = string.Empty;
    public string PublicKeyPemPath { get; set; } = string.Empty;
    public string Issuer { get; set; } = "Gnosis.Auth";
    public string Audience { get; set; } = "Gnosis.Clients";
    public int AccessTokenMinutes { get; set; } = 20;
    public string KeyId { get; set; } = "gnosis-auth-default";
}
