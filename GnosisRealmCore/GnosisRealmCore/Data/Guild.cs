using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GnosisRealmCore.Data;

[Table("guilds")]
public sealed class Guild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [Column("leader_id")]
    public int LeaderId { get; set; }

    [Column("level")]
    public int Level { get; set; } = 1;

    [Column("motd")]
    [MaxLength(255)]
    public string? Motd { get; set; } = "Welcome to the guild!";

    public ICollection<Character> Members { get; set; } = new List<Character>();
}
