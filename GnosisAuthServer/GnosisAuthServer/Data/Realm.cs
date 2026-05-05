using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace GnosisAuthServer.Data
{
    [Table("realms")]
    public class Realm
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty; // Pl. "Gnosis Official EU-West"

        [Column("region")]
        public string Region { get; set; } = string.Empty;

        [Column("api_key")]
        public string ApiKey { get; set; } = string.Empty; // A MasterHub titkos kulcsa

        [Column("api_url")]
        public string ApiUrl { get; set; } = string.Empty; // A kliens ide csatlakozik a karaktereiért

        [Column("status")]
        public int Status { get; set; } // 0 = Offline, 1 = Online, 2 = Karbantartás

        [Column("current_players")]
        public int CurrentPlayers { get; set; } // Az összes zóna összesített játékosszáma

        [Column("max_players")]
        public int MaxPlayers { get; set; }

        [Column("last_heartbeat")]
        public DateTime LastHeartbeat { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}