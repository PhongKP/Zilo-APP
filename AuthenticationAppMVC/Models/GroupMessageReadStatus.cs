using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace AuthenticationAppMVC.Models
{
    public class GroupMessageReadStatus
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public long GroupMessageId { get; set; }

        [ForeignKey("GroupMessageId")]
        public GroupMessage GroupMessage { get; set; }

        [Required]
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; }

        public long ReadTimestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
