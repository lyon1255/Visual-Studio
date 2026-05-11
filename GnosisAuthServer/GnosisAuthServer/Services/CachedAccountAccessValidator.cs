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
    private readonly ILogger<CachedAccountAccessValidator> _logger;

    public CachedAccountAccessValidator(
        AuthDbContext dbContext,
        IMemoryCache cache,
        IOptions<AccountAccessOptions> options,
        ILogger<CachedAccountAccessValidator> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _cacheDuration = TimeSpan.FromSeconds(Math.Max(0, options.Value.CacheTtlSeconds));
        _logger = logger;
    }

    public async Task<AccountAccessValidationResult> ValidateAsync(string steamId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(steamId))
        {
            _logger.LogWarning("Bearer account access rejected because subject claim is missing.");
            return AccountAccessValidationResult.Denied("Missing subject claim.");
        }

        var cacheKey = GetCacheKey(steamId);
        if (_cache.TryGetValue(cacheKey, out AccountAccessValidationResult? cached) && cached is not null)
        {
            if (!cached.IsAllowed)
            {
                _logger.LogWarning("Bearer account access rejected from cache. SteamId={SteamId} Reason={Reason}", steamId, cached.DenialReason ?? string.Empty);
            }

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

        if (!result.IsAllowed)
        {
            _logger.LogWarning("Bearer account access rejected after database validation. SteamId={SteamId} Reason={Reason}", steamId, result.DenialReason ?? string.Empty);
        }

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
        _logger.LogInformation("Account access cache invalidated. SteamId={SteamId}", steamId);
    }

    private static string GetCacheKey(string steamId) => $"account-access:{steamId}";
}
