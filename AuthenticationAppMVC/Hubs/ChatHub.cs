using AuthenticationAppMVC.Data;
using AuthenticationAppMVC.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationAppMVC.Hubs
{
    public class Chathub:Hub
    {
        private readonly AppDBContext _dbContext;
        private static readonly Dictionary<string, string> _userConnections = new Dictionary<string, string>();

        public Chathub(AppDBContext dBContext)
        {
            _dbContext = dBContext;
        }

        public bool IsUserOnline(string email)
        {
            return _userConnections.ContainsKey(email);
        }

        public override async Task OnConnectedAsync()
        {
            var userEmail = Context.User.Identity.Name;
            if (!string.IsNullOrEmpty(userEmail))
            {
                _userConnections[userEmail] = Context.ConnectionId;

                // Cập nhật trạng thái tin nhắn từ Sent -> Delivered
                await UpdatePendingDirectMessages(userEmail);

                // Các nhóm chat vẫn giữ nguyên như cũ
                var groupIds = await _dbContext.GroupMembers
                    .Include(gm => gm.User)
                    .Where(gm => gm.User.Email == userEmail)
                    .Select(gm => gm.GroupId)
                    .ToListAsync();

                foreach (var groupId in groupIds)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, groupId);
                    await UpdatePendingGroupMessages(userEmail, groupId);
                }
            }

            await base.OnConnectedAsync();
        }

        // Cập nhật OnDisconnectedAsync
        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var userEmail = Context.User?.Identity?.Name;
            if (!string.IsNullOrEmpty(userEmail))
            {
                _userConnections.Remove(userEmail);
                // Rời khỏi các group chat
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
                if (user != null)
                {
                    var groupIds = await _dbContext.GroupMembers
                        .Where(gm => gm.UserId == user.Id)
                        .Select(gm => gm.GroupId)
                        .ToListAsync();

                    foreach (var groupId in groupIds)
                    {
                        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupId);
                    }
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        // Cập nhật tin nhắn trực tiếp khi người nhận online
        private async Task UpdatePendingDirectMessages(string receiverEmail)
        {
            // Tìm các tin nhắn được gửi đến người dùng và có trạng thái là Sent
            var pendingMessages = await _dbContext.Messages
                .Where(m => m.ReceiverEmail == receiverEmail && m.Status == MessageStatus.Sent)
                .ToListAsync();

            if (pendingMessages.Any())
            {
                foreach (var message in pendingMessages)
                {
                    message.Status = MessageStatus.Delivered;

                    if (_userConnections.TryGetValue(message.SenderEmail, out var senderConnectionId))
                    {
                        await Clients.Client(senderConnectionId).SendAsync("MessageStatusUpdated", message.Id, (int)MessageStatus.Delivered);
                    }
                }

                await _dbContext.SaveChangesAsync();
            }
        }

        // Đánh dấu một tin nhắn đã được đọc
        public async Task MarkMessageAsRead(long messageId)
        {
            var currentUser = Context?.User?.Identity?.Name;
            if (string.IsNullOrEmpty(currentUser)) return;

            var message = await _dbContext.Messages
                .FirstOrDefaultAsync(m => m.Id == messageId && m.ReceiverEmail == currentUser);

            if (message != null && message.Status != MessageStatus.Read)
            {
                message.Status = MessageStatus.Read;
                await _dbContext.SaveChangesAsync();

                if (_userConnections.TryGetValue(message.SenderEmail, out var senderConnectionId))
                {
                    await Clients.Client(senderConnectionId).SendAsync("MessageStatusUpdated", message.Id, (int)MessageStatus.Read);
                }
            }
        }

        // Đánh dấu tất cả tin nhắn từ một người là đã đọc
        public async Task MarkAllMessagesAsRead(string senderEmail)
        {
            var currentUser = Context.User.Identity.Name;
            if (string.IsNullOrEmpty(currentUser)) return;

            var unreadMessages = await _dbContext.Messages
                .Where(m => m.SenderEmail == senderEmail && m.ReceiverEmail == currentUser && m.Status != MessageStatus.Read)
                .ToListAsync();

            if (unreadMessages.Any())
            {
                foreach (var message in unreadMessages)
                {
                    message.Status = MessageStatus.Read;
                }

                await _dbContext.SaveChangesAsync();

                if (_userConnections.TryGetValue(senderEmail, out var senderConnectionId))
                {
                    await Clients.Client(senderConnectionId).SendAsync("MessagesRead", unreadMessages.Select(m => m.Id).ToList());
                }
            }
        }

        // Cập nhật trạng thái tin nhắn nhóm khi người dùng online
        private async Task UpdatePendingGroupMessages(string userEmail, string groupId)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
            if (user == null) return;

            // Tìm tin nhắn nhóm mà người dùng chưa đánh dấu là đã đọc
            var unreadMessages = await _dbContext.GroupMessages
                .Include(gm => gm.ReadBys)
                .Where(gm => gm.GroupId == groupId)
                .Where(gm => !gm.ReadBys.Any(rb => rb.UserId == user.Id))
                .ToListAsync();

            if (unreadMessages.Any())
            {
                // Đối với tin nhắn nhóm, chúng ta cập nhật trạng thái "đã nhận" mà không đánh dấu là "đã đọc"
                // Vì "đã đọc" yêu cầu tương tác trực tiếp của người dùng (mở cuộc trò chuyện)
                foreach (var message in unreadMessages.Where(m => m.SenderId != user.Id))
                {
                    // Nếu trạng thái là Sent, cập nhật thành Delivered
                    if (message.Status == MessageStatus.Sent)
                    {
                        message.Status = MessageStatus.Delivered;
                    }
                }

                await _dbContext.SaveChangesAsync();
            }
        }

        // Đánh dấu một tin nhắn nhóm đã được đọc
        public async Task MarkGroupMessageAsRead(long messageId)
        {
            var userEmail = Context.User.Identity.Name;
            if (string.IsNullOrEmpty(userEmail)) return;

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
            if (user == null) return;

            // Tìm tin nhắn nhóm
            var groupMessage = await _dbContext.GroupMessages
                .Include(gm => gm.ReadBys)
                .FirstOrDefaultAsync(gm => gm.Id == messageId);

            if (groupMessage == null) return;

            // Kiểm tra xem người dùng có trong nhóm không
            var isMember = await _dbContext.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupMessage.GroupId && gm.UserId == user.Id);

            if (!isMember) return;

            // Kiểm tra xem đã đọc chưa
            var alreadyRead = groupMessage.ReadBys?.Any(rb => rb.UserId == user.Id) == true;

            if (!alreadyRead)
            {
                // Thêm vào danh sách người đã đọc
                var readStatus = new GroupMessageReadStatus
                {
                    GroupMessageId = messageId,
                    UserId = user.Id,
                    ReadTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                _dbContext.GroupMessageReadStatuses.Add(readStatus);

                // Nếu tất cả thành viên đã đọc, cập nhật trạng thái tin nhắn
                await UpdateGroupMessageStatus(groupMessage);

                await _dbContext.SaveChangesAsync();

                // Thông báo cho nhóm
                await Clients.Group(groupMessage.GroupId).SendAsync("GroupMessageRead", groupMessage.Id, user.Id, userEmail);
            }
        }

        // Đánh dấu tất cả tin nhắn trong nhóm là đã đọc
        public async Task MarkAllGroupMessagesAsRead(string groupId)
        {
            var userEmail = Context.User.Identity.Name;
            if (string.IsNullOrEmpty(userEmail)) return;

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
            if (user == null) return;

            // Kiểm tra xem người dùng có trong nhóm không
            var isMember = await _dbContext.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == user.Id);

            if (!isMember) return;

            // Tìm tất cả tin nhắn chưa đọc trong nhóm
            var unreadMessages = await _dbContext.GroupMessages
                .Include(gm => gm.ReadBys)
                .Where(gm => gm.GroupId == groupId)
                .Where(gm => !gm.ReadBys.Any(rb => rb.UserId == user.Id))
                .ToListAsync();

            if (unreadMessages.Any())
            {
                foreach (var message in unreadMessages)
                {
                    // Thêm vào danh sách người đã đọc
                    var readStatus = new GroupMessageReadStatus
                    {
                        GroupMessageId = message.Id,
                        UserId = user.Id,
                        ReadTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };

                    _dbContext.GroupMessageReadStatuses.Add(readStatus);

                    // Kiểm tra và cập nhật trạng thái tin nhắn
                    await UpdateGroupMessageStatus(message);
                }

                await _dbContext.SaveChangesAsync();

                await Clients.Group(groupId).SendAsync("GroupMessagesReadByUser", groupId, user.Id, userEmail);
            }
        }

        // Cập nhật trạng thái tin nhắn nhóm dựa vào người đã đọc
        private async Task UpdateGroupMessageStatus(GroupMessage groupMessage)
        {
            // Nếu tin nhắn không phải trạng thái Read, kiểm tra xem có nên cập nhật không
            if (groupMessage.Status != MessageStatus.Read)
            {
                // Lấy tất cả thành viên trong nhóm trừ người gửi
                var groupMembersCount = await _dbContext.GroupMembers
                    .CountAsync(gm => gm.GroupId == groupMessage.GroupId && gm.UserId != groupMessage.SenderId);

                // Đếm số người đã đọc tin nhắn (trừ người gửi)
                var readCount = await _dbContext.GroupMessageReadStatuses
                    .CountAsync(r => r.GroupMessageId == groupMessage.Id && r.UserId != groupMessage.SenderId);

                // Nếu ít nhất 1 người đã đọc, cập nhật trạng thái thành Read
                if (readCount > 0)
                {
                    groupMessage.Status = MessageStatus.Read;

                    // Thông báo cho người gửi rằng tin nhắn đã được đọc
                    var sender = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == groupMessage.SenderId);
                    if (sender != null && _userConnections.TryGetValue(sender.Email, out var senderConnectionId))
                    {
                        await Clients.Client(senderConnectionId).SendAsync("GroupMessageStatusUpdated", groupMessage.Id, (int)MessageStatus.Read);
                    }
                }
                // Nếu chưa ai đọc nhưng có người nhận, cập nhật thành Delivered
                else if (groupMessage.Status == MessageStatus.Sent)
                {
                    groupMessage.Status = MessageStatus.Delivered;

                    // Thông báo cho người gửi rằng tin nhắn đã được nhận
                    var sender = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == groupMessage.SenderId);
                    if (sender != null && _userConnections.TryGetValue(sender.Email, out var senderConnectionId))
                    {
                        await Clients.Client(senderConnectionId).SendAsync("GroupMessageStatusUpdated", groupMessage.Id, (int)MessageStatus.Delivered);
                    }
                }
            }
        }

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
