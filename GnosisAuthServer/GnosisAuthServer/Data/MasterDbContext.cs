using Microsoft.EntityFrameworkCore;

namespace GnosisAuthServer.Data
{
    public class MasterDbContext : DbContext
    {
        public MasterDbContext(DbContextOptions<MasterDbContext> options) : base(options) { }

        public DbSet<Account> Accounts { get; set; }
        public DbSet<Realm> Realms { get; set; }

        // --- ÚJ: GAME DATA TÁBLÁK ---
        public DbSet<DbItem> GameItems { get; set; }
        public DbSet<DbEntity> GameEntities { get; set; }
        public DbSet<DbQuest> GameQuests { get; set; }
        public DbSet<DbSpell> GameSpells { get; set; }
        public DbSet<DbAura> GameAuras { get; set; }
    }
}