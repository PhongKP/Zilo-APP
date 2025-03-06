using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthenticationAppMVC.Models
{
    public class FriendRequest
    {
        [Key]
        public string Id = Guid.NewGuid().ToString();

        [Required]    
        public string SenderId { get; set; }

        [ForeignKey("SenderId")]
        public User Sender { get; set; }

        [Required]
        public string ReceiverId { get; set; }

        [ForeignKey("ReceiverId")]
        public User Receiver { get; set; }

        [Required]
        public FriendRequestStatus requestStatus { get; set; } = FriendRequestStatus.Pending;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset? AcceptedAt { get; set; }
    }
}
