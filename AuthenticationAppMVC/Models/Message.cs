using System.ComponentModel.DataAnnotations;

namespace AuthenticationAppMVC.Models
{
    public class Message
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public string? Content { get; set; }

        [Required]
        public string? SenderEmail { get; set; }

        [Required]
        public string? ReceiverEmail { get; set; }

        public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public bool HasAttachment { get; set; } = false;

        public virtual ICollection<FileAttachment> Attachments { get; set; }
    }
}
