using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace AuthenticationAppMVC.Models
{
    public class CloudAttachment
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string CloudMessageId { get; set; }

        [ForeignKey("CloudMessageId")]
        public CloudMessage CloudMessage { get; set; }

        [Required]
        public string FileName { get; set; }

        [Required]
        public string ContentType { get; set; }

        // Đường dẫn Cloudinary
        [Required]
        public string StoragePath { get; set; }

        // Public ID trên Cloudinary
        [Required]
        public string PublicId { get; set; }

        public long FileSize { get; set; }

        public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;

        [Required]
        public string UploaderId { get; set; }

        [ForeignKey("UploaderId")]
        public User Uploader { get; set; }
    }
}
