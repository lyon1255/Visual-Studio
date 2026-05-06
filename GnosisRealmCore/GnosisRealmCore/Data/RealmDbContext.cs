using Microsoft.EntityFrameworkCore;

namespace GnosisRealmCore.Data;

public sealed class RealmDbContext : DbContext
{
    public RealmDbContext(DbContextOptions<RealmDbContext> options) : base(options) { }

    public DbSet<Character> Characters => Set<Character>();
    public DbSet<CharacterItem> CharacterItems => Set<CharacterItem>();
    public DbSet<CharacterEquipment> CharacterEquipment => Set<CharacterEquipment>();
    public DbSet<CharacterHotbar> CharacterHotbar => Set<CharacterHotbar>();
    public DbSet<CharacterMail> CharacterMail => Set<CharacterMail>();
    public DbSet<CharacterQuest> CharacterQuests => Set<CharacterQuest>();
    public DbSet<CharacterQuestHistory> CharacterQuestHistory => Set<CharacterQuestHistory>();
    public DbSet<CharacterSetting> CharacterSettings => Set<CharacterSetting>();
    public DbSet<CharacterSocial> CharacterSocial => Set<CharacterSocial>();
    public DbSet<Guild> Guilds => Set<Guild>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RealmNode> RealmNodes => Set<RealmNode>();
    public DbSet<RealmZoneInstance> RealmZoneInstances => Set<RealmZoneInstance>();
    public DbSet<RealmNodeCommand> RealmNodeCommands => Set<RealmNodeCommand>();
    public DbSet<RealmGameDataOverride> RealmGameDataOverrides => Set<RealmGameDataOverride>();
    public DbSet<ServerRate> ServerRates => Set<ServerRate>();
    public DbSet<ClassStat> ClassStats => Set<ClassStat>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Character>()
            .HasIndex(x => x.SteamId);

        modelBuilder.Entity<Character>()
            .HasIndex(x => x.Name)
            .IsUnique();

        modelBuilder.Entity<Character>()
            .HasOne(x => x.Guild)
            .WithMany(g => g.Members)
            .HasForeignKey(x => x.GuildId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<CharacterItem>()
            .HasIndex(x => new { x.CharacterId, x.ContainerType, x.SlotIndex });

        modelBuilder.Entity<CharacterEquipment>()
            .HasIndex(x => new { x.CharacterId, x.SlotType });

        modelBuilder.Entity<CharacterHotbar>()
            .HasIndex(x => new { x.CharacterId, x.SlotIndex })
            .IsUnique();

        modelBuilder.Entity<CharacterQuestHistory>()
            .HasIndex(x => new { x.CharacterId, x.QuestId })
            .IsUnique();

        modelBuilder.Entity<CharacterSocial>()
            .HasIndex(x => new { x.CharacterId, x.TargetId })
            .IsUnique();

        modelBuilder.Entity<RealmNode>()
            .HasIndex(x => x.NodeId)
            .IsUnique();

        modelBuilder.Entity<RealmZoneInstance>()
            .HasIndex(x => x.ZoneName);

        modelBuilder.Entity<RealmNodeCommand>()
            .HasIndex(x => new { x.NodeId, x.Status, x.CreatedAtUtc });

        modelBuilder.Entity<RealmGameDataOverride>()
            .HasIndex(x => new { x.Category, x.AssetId })
            .IsUnique();
    }
}
