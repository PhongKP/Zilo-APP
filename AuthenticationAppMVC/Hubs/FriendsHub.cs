using Microsoft.AspNetCore.SignalR;

namespace AuthenticationAppMVC.Hubs
{
    public class FriendsHub:Hub
    {
        public async Task SendFriendRequest(string receiverId)
        {
            await Clients.User(receiverId).SendAsync("ReceiveFriendRequest", Context.UserIdentifier);
        }

        public async Task AcceptFriendRequest(string senderId)
        {
            await Clients.User(senderId).SendAsync("ReceiveFriendRequestAccepted", Context.UserIdentifier);
        }
    }
}
