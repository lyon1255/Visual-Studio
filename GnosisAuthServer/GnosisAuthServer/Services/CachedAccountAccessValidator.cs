using GnosisAuthServer.Data;
using GnosisAuthServer.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace GnosisAuthServer.Services;

public sealed class CachedAccountAccessValidator : IAccountAccessValidator
{
    private readonly AuthDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheDuration;

    public CachedAccountAccessValidator(AuthDbContext dbContext, IMemoryCache cache, IOptions<AccountAccessOptions> options)
    {
        _dbContext = dbContext;
        _cache = cache;
        _cacheDuration = TimeSpan.FromSeconds(Math.Max(0, options.Value.CacheTtlSeconds));
    }

    public async Task<AccountAccessValidationResult> ValidateAsync(string steamId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(steamId))
        {
            return AccountAccessValidationResult.Denied("Missing subject claim.");
        }

        var cacheKey = GetCacheKey(steamId);
        if (_cache.TryGetValue(cacheKey, out AccountAccessValidationResult? cached) && cached is not null)
        {
            return cached;
        }

        var account = await _dbContext.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SteamId == steamId, cancellationToken);

        var result = account switch
        {
            null => AccountAccessValidationResult.Denied("Account was not found."),
            { IsBanned: true } banned => AccountAccessValidationResult.Denied(banned.BanReason ?? "This account is banned."),
            _ => AccountAccessValidationResult.Allowed()
        };

        _cache.Set(cacheKey, result, _cacheDuration);
        return result;
    }

    public void Invalidate(string steamId)
    {
        if (string.IsNullOrWhiteSpace(steamId))
        {
            return;
        }

        _cache.Remove(GetCacheKey(steamId));
    }

    private static string GetCacheKey(string steamId) => $"account-access:{steamId}";
}
