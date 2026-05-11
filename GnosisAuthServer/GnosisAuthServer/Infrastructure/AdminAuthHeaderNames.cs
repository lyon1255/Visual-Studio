namespace GnosisAuthServer.Infrastructure;

public static class AdminAuthHeaderNames
{
    public const string Timestamp = "X-Gnosis-Admin-Timestamp";
    public const string Nonce = "X-Gnosis-Admin-Nonce";
    public const string Signature = "X-Gnosis-Admin-Signature";
    public const string BodySha256 = "X-Gnosis-Admin-Body-Sha256";
}
