using GnosisAuthServer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GnosisAuthServer.Services;

public sealed class CachedAccountAccessValidator : IAccountAccessValidator
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private readonly AuthDbContext _dbContext;
    private readonly IMemoryCache _cache;

    public CachedAccountAccessValidator(AuthDbContext dbContext, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<AccountAccessValidationResult> ValidateAsync(string steamId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(steamId))
        {
            return AccountAccessValidationResult.Denied("Missing subject claim.");
        }

        var cacheKey = $"account-access:{steamId}";
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

        _cache.Set(cacheKey, result, CacheDuration);
        return result;
    }
}
