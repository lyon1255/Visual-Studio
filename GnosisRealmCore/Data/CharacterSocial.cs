using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GnosisRealmCore.Data;

[Table("character_social")]
public sealed class CharacterSocial
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("character_id")]
    public int CharacterId { get; set; }

    [Column("target_id")]
    public int TargetId { get; set; }

    [Column("relation_type")]
    public int RelationType { get; set; }

    public Character? Character { get; set; }
}
