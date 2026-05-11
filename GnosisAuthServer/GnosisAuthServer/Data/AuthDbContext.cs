using Gnosis.AuthServer.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GnosisAuthServer.Data;

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Realm> Realms => Set<Realm>();
    public DbSet<DbItem> GameItems => Set<DbItem>();
    public DbSet<DbEntity> GameEntities => Set<DbEntity>();
    public DbSet<DbQuest> GameQuests => Set<DbQuest>();
    public DbSet<DbSpell> GameSpells => Set<DbSpell>();
    public DbSet<DbAura> GameAuras => Set<DbAura>();
    public DbSet<GameDataVersion> GameDataVersions => Set<GameDataVersion>();
    public DbSet<BannedIpAddress> BannedIpAddresses => Set<BannedIpAddress>();
    public DbSet<RealmNode> RealmNodes => Set<RealmNode>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>()
            .HasIndex(x => x.SteamId)
            .IsUnique();

        modelBuilder.Entity<Realm>()
            .HasIndex(x => x.RealmId)
            .IsUnique();

        modelBuilder.Entity<Realm>()
            .HasIndex(x => new { x.IsListed, x.Status, x.LastHeartbeatAt });

        modelBuilder.Entity<DbItem>().HasIndex(x => x.AssetId).IsUnique();
        modelBuilder.Entity<DbEntity>().HasIndex(x => x.AssetId).IsUnique();
        modelBuilder.Entity<DbQuest>().HasIndex(x => x.AssetId).IsUnique();
        modelBuilder.Entity<DbSpell>().HasIndex(x => x.AssetId).IsUnique();
        modelBuilder.Entity<DbAura>().HasIndex(x => x.AssetId).IsUnique();

        modelBuilder.Entity<GameDataVersion>().HasIndex(x => x.VersionNumber).IsUnique();
        modelBuilder.Entity<GameDataVersion>().HasIndex(x => x.PublishedAtUtc);
        base.OnModelCreating(modelBuilder);
    }
}
