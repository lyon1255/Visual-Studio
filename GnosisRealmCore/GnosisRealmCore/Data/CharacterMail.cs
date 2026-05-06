using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GnosisRealmCore.Data;

[Table("character_mail")]
public sealed class CharacterMail
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("receiver_id")]
    public int ReceiverId { get; set; }

    [Column("sender_name")]
    [MaxLength(50)]
    public string SenderName { get; set; } = "System";

    [Column("subject")]
    [MaxLength(100)]
    public string? Subject { get; set; }

    [Column("message")]
    public string? Message { get; set; }

    [Column("attached_item_id")]
    [MaxLength(100)]
    public string? AttachedItemId { get; set; }

    [Column("attached_amount")]
    public int AttachedAmount { get; set; }

    [Column("attached_currency")]
    public long AttachedCurrency { get; set; }

    [Column("is_read")]
    public bool IsRead { get; set; }

    [Column("sent_at")]
    public DateTime? SentAt { get; set; }
}
