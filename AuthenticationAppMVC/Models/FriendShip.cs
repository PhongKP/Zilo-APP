using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthenticationAppMVC.Models
{
    public class FriendShip
    {
        [Key]
        public string Id = Guid.NewGuid().ToString();
        [Required]
        public string User1Id { get; set; }
        [ForeignKey("User1Id")]
        public User User1 { get; set; }
        [Required]
        public string User2Id { get; set; }
        [ForeignKey("User2Id")]
        public User User2 { get; set; }

        public FriendShipStatus friendShipStatus { get; set; } = FriendShipStatus.Active;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
