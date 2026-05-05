using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace GnosisAuthServer.Data
{
    [Table("accounts")]
    public class Account
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("steam_id")]
        public string SteamId { get; set; } = string.Empty;

        [Column("is_banned")]
        public bool IsBanned { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}