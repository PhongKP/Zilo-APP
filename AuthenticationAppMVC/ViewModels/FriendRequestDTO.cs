using AuthenticationAppMVC.Models;

namespace AuthenticationAppMVC.ViewModels
{
    public class FriendRequestDTO
    {
        public string id { get; set; }
        public string SenderId {  get; set; }
        public string ReceiverId { get; set; }
        public FriendRequestStatus RequestStatus { get; set; }
        public User? Sender { get; set; }

        public User? Receiver { get; set; }
        
        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset? AcceptedAt { get; set; }
    }
}
