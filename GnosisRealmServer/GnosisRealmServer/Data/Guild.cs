using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace GnosisRealmCore.Data
{
    [Table("guilds")]
    public class Guild
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("leader_id")]
        public int LeaderId { get; set; }

        [Column("level")]
        public int Level { get; set; } = 1;

        [Column("motd")]
        public string MessageOfTheDay { get; set; } = "Üdv a klánban!";

        [JsonIgnore]
        public virtual ICollection<Character> Members { get; set; } = new List<Character>();
    }
}
