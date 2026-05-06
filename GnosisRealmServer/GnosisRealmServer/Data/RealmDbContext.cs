using Microsoft.EntityFrameworkCore;

namespace GnosisRealmCore.Data
{
    public class RealmDbContext : DbContext
    {
        public RealmDbContext(DbContextOptions<RealmDbContext> options) : base(options) { }

        public DbSet<Character> Characters { get; set; }
        public DbSet<CharacterItem> Items { get; set; }
        public DbSet<CharacterHotbar> Hotbars { get; set; }
        public DbSet<Permission> Permissions { get; set; }

        // Küldetés Rendszer
        public DbSet<CharacterQuest> Quests { get; set; }
        public DbSet<CharacterQuestHistory> QuestHistory { get; set; }

        // Szociális és Kommunikáció
        public DbSet<CharacterSocial> Social { get; set; } // <--- ÚJ: Barátok és Tiltólista
        public DbSet<CharacterMail> Mails { get; set; }
        public DbSet<Guild> Guilds { get; set; }
        // RealmDbContext.cs részlet
        public DbSet<ServerRate> ServerRates { get; set; }
        public DbSet<ClassStat> ClassStats { get; set; }

        // Beállítások
        public DbSet<CharacterSetting> Settings { get; set; }
        public DbSet<RealmNode> RealmNodes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Elsődleges kulcsok és alapbeállítások
            modelBuilder.Entity<CharacterHotbar>().HasKey(h => h.Id);
            modelBuilder.Entity<Permission>().HasKey(p => p.SteamId);

            // --- Klán Kapcsolat ---
            modelBuilder.Entity<Character>()
                .HasOne(c => c.Guild)
                .WithMany(g => g.Members)
                .HasForeignKey(c => c.GuildId)
                .OnDelete(DeleteBehavior.SetNull); // Ha törlik a klánt, a karakter maradjon meg

            // --- Quest History Egyediség ---
            // Megakadályozza, hogy egy küldetés többször szerepeljen a történelemben
            modelBuilder.Entity<CharacterQuestHistory>()
                .HasIndex(h => new { h.CharacterId, h.QuestId })
                .IsUnique();

            // --- Social Egyediség (ÚJ) ---
            // Megakadályozza, hogy ugyanazt a célszemélyt többször hozzáadjuk barátként/tiltottként
            modelBuilder.Entity<CharacterSocial>()
                .HasIndex(s => new { s.CharacterId, s.TargetId })
                .IsUnique();
        }
    }
}