using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace AuthenticationAppMVC.Models
{
    public class GroupMessage
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public string GroupId { get; set; }

        [ForeignKey("GroupId")]
        public Group Group { get; set; }

        [Required]
        public string SenderId { get; set; }

        [ForeignKey("SenderId")]
        public User Sender { get; set; }

        [Required]
        public string Content { get; set; }

        public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public bool HasAttachment { get; set; } = false;

        public virtual ICollection<FileAttachment> Attachments { get; set; }
    }
}
