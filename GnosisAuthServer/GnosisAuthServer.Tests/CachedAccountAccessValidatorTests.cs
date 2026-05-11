using GnosisAuthServer.Data;
using GnosisAuthServer.Options;
using GnosisAuthServer.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GnosisAuthServer.Tests;

public sealed class CachedAccountAccessValidatorTests
{
    [Fact]
    public async Task ValidateAsync_DeniesBannedAccount()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        await using var dbContext = new AuthDbContext(options);
        dbContext.Accounts.Add(new Account
        {
            SteamId = "76561198000000000",
            IsBanned = true,
            BanReason = "Test ban"
        });
        await dbContext.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var validator = new CachedAccountAccessValidator(
            dbContext,
            cache,
            Options.Create(new AccountAccessOptions { CacheTtlSeconds = 30 }),
            NullLogger<CachedAccountAccessValidator>.Instance);

        var result = await validator.ValidateAsync("76561198000000000", CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.Equal("Test ban", result.DenialReason);
    }

    [Fact]
    public async Task Invalidate_RemovesCachedDecision()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        await using var dbContext = new AuthDbContext(options);
        dbContext.Accounts.Add(new Account
        {
            SteamId = "76561198000000001",
            IsBanned = false
        });
        await dbContext.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var validator = new CachedAccountAccessValidator(
            dbContext,
            cache,
            Options.Create(new AccountAccessOptions { CacheTtlSeconds = 300 }),
            NullLogger<CachedAccountAccessValidator>.Instance);

        var first = await validator.ValidateAsync("76561198000000001", CancellationToken.None);
        Assert.True(first.IsAllowed);

        var account = await dbContext.Accounts.FirstAsync(x => x.SteamId == "76561198000000001");
        account.IsBanned = true;
        account.BanReason = "Updated ban";
        await dbContext.SaveChangesAsync();

        validator.Invalidate("76561198000000001");
        var second = await validator.ValidateAsync("76561198000000001", CancellationToken.None);

        Assert.False(second.IsAllowed);
        Assert.Equal("Updated ban", second.DenialReason);
    }
}
