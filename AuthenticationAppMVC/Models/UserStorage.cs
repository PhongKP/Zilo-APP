using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace AuthenticationAppMVC.Models
{
    public class UserStorage
    {
        [Key]
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; }

        [Required]
        public long StorageUsed { get; set; } = 0;

        [Required]
        public long StorageLimit { get; set; } = 104857600; // 100MB
    }
}
