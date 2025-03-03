using Microsoft.AspNetCore.SignalR;

namespace AuthenticationAppMVC.Hubs
{
    public class Chathub:Hub
    {
        public async Task SendMessage(string senderEmail, string receiverEmail, string content)
        {
            await Clients.User(receiverEmail).SendAsync("ReceiveMessage", senderEmail, content);
        }

        public async Task SendMessageWithAttachment(string senderEmail, string receiverEmail, string content, string fileId, string fileName)
        {
            await Clients.User(receiverEmail).SendAsync("ReceiveMessageWithAttachment", senderEmail, content, fileId, fileName);
        }

        public async Task JoinGroup(string groupId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupId);
        }

        public async Task LeaveGroup(string groupId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupId);
        }

        public async Task SendGroupMessage(string groupId, string senderEmail, string content)
        {
            await Clients.Group(groupId).SendAsync("ReceiveGroupMessage", groupId, senderEmail, content);
        }

        public async Task SendGroupMessageWithAttachment(string groupId, string senderEmail, string content, string fileId, string fileName)
        {
            await Clients.Group(groupId).SendAsync("ReceiveGroupMessageWithAttachment", groupId, senderEmail, content, fileId, fileName);
        }
    }
}
