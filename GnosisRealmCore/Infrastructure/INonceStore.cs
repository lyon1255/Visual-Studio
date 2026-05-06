namespace GnosisRealmCore.Infrastructure;

public interface INonceStore
{
    bool TryUseNonce(string scope, string nonce, TimeSpan ttl);
}
