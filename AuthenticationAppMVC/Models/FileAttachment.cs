using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace AuthenticationAppMVC.Models
{
    public class FileAttachment
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string FileName { get; set; }

        [Required]
        public string ContentType { get; set; }

        [Required]
        public string StoragePath { get; set; }

        public long FileSize { get; set; }

        public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;

        [Required]
        public string UploaderId { get; set; }

        [ForeignKey("UploaderId")]
        public User Uploader { get; set; }

        public long? MessageId { get; set; }

        [ForeignKey("MessageId")]
        public Message Message { get; set; }

        public long? GroupMessageId { get; set; }

        [ForeignKey("GroupMessageId")]
        public GroupMessage GroupMessage { get; set; }
    }
}
