using System.ComponentModel.DataAnnotations.Schema;

namespace GnosisRealmCore.Data
{
    [Table("character_mail")]
    public class CharacterMail
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("receiver_id")]
        public int ReceiverId { get; set; }

        [Column("sender_name")]
        public string SenderName { get; set; } = "System";

        [Column("subject")]
        public string Subject { get; set; } = string.Empty;

        [Column("message")]
        public string Message { get; set; } = string.Empty;

        [Column("attached_item_id")]
        public string AttachedItemId { get; set; } = string.Empty;

        [Column("attached_amount")]
        public int AttachedAmount { get; set; }

        [Column("attached_currency")]
        public long AttachedCurrency { get; set; }

        [Column("is_read")]
        public bool IsRead { get; set; } = false;

        [Column("sent_at")]
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
    }
}
