namespace GnosisAuthServer.Services;

public interface IIpBanCacheService
{
    Task<bool> IsBlockedAsync(string ipAddress, CancellationToken cancellationToken = default);

    void Invalidate(string ipAddress);

    void InvalidateAll();
}