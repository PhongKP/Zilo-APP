using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace AuthenticationAppMVC.Models
{
    public class GroupMember
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string GroupId { get; set; }

        [Required]
        public string UserId { get; set; }

        public bool IsAdmin { get; set; } = false;

        public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation properties - không cần thêm các thuộc tính ForeignKey
        public virtual Group Group { get; set; }
        public virtual User User { get; set; }
    }
}
