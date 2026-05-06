namespace GnosisRealmCore.Infrastructure;

public static class ServiceAuthHeaderNames
{
    public const string ServiceId = "X-Gnosis-ServiceId";
    public const string Timestamp = "X-Gnosis-Timestamp";
    public const string Nonce = "X-Gnosis-Nonce";
    public const string Signature = "X-Gnosis-Signature";
    public const string BodySha256 = "X-Gnosis-BodySha256";
}
