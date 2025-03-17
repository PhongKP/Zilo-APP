using AuthenticationAppMVC.Models;
using AuthenticationAppMVC.Services;
using Microsoft.AspNetCore.SignalR;

namespace AuthenticationAppMVC.Hubs
{
    public class CallHub:Hub
    {
        private readonly ICallService _callService;
        // Dictionary để lưu trữ ánh xạ giữa UserId và ConnectionId
        private static readonly Dictionary<string, string> UserConnections = new Dictionary<string, string>();

        public CallHub(ICallService callService)
        {
            _callService = callService;
        }

        // Được gọi khi một kết nối mới được thiết lập
        public override async Task OnConnectedAsync()
        {
            string userId = Context.User.Identity.Name;

            // Cập nhật ánh xạ UserId -> ConnectionId
            lock (UserConnections)
            {
                if (UserConnections.ContainsKey(userId))
                {
                    UserConnections[userId] = Context.ConnectionId;
                }
                else
                {
                    UserConnections.Add(userId, Context.ConnectionId);
                }
            }

            // Thông báo cho người dùng hiện tại rằng họ đã kết nối thành công
            await Clients.Caller.SendAsync("Connected", Context.ConnectionId);

            // Thông báo cho những người khác rằng người dùng này đã online
            await Clients.Others.SendAsync("UserOnline", userId);

            // Kiểm tra xem người dùng có cuộc gọi đang diễn ra không
            var activeCall = await _callService.GetUserActiveCallAsync(userId);
            if (activeCall != null)
            {
                // Thông báo cho người dùng về cuộc gọi đang diễn ra của họ
                await Clients.Caller.SendAsync("ActiveCall", activeCall);
            }

            await base.OnConnectedAsync();
        }

        // Được gọi khi kết nối bị đóng
        public override async Task OnDisconnectedAsync(Exception exception)
        {
            string userId = Context.User.Identity.Name;

            // Kiểm tra xem người dùng có đang trong cuộc gọi nào không
            var activeCall = await _callService.GetUserActiveCallAsync(userId);
            if (activeCall != null)
            {
                // Lấy thông tin participant của người dùng
                var participant = await _callService.GetParticipantAsync(activeCall.Id, userId);
                if (participant != null && participant.HasJoined && !participant.LeaveTime.HasValue)
                {
                    // Đánh dấu người dùng đã rời cuộc gọi
                    await _callService.LeaveCallAsync(activeCall.Id, userId);

                    // Thông báo cho những người khác trong cuộc gọi
                    var otherParticipants = await _callService.GetCallParticipantsAsync(activeCall.Id);
                    foreach (var otherParticipant in otherParticipants.Where(p => p.UserId != userId && p.HasJoined && !p.LeaveTime.HasValue))
                    {
                        if (UserConnections.TryGetValue(otherParticipant.UserId, out string connectionId))
                        {
                            await Clients.Client(connectionId).SendAsync("UserLeftCall", userId);
                        }
                    }
                }
            }

            // Xóa ánh xạ connection của người dùng
            lock (UserConnections)
            {
                if (UserConnections.ContainsKey(userId))
                {
                    UserConnections.Remove(userId);
                }
            }

            // Thông báo cho người khác rằng người dùng đã offline
            await Clients.Others.SendAsync("UserOffline", userId);

            await base.OnDisconnectedAsync(exception);
        }

        // Phương thức để khởi tạo một cuộc gọi
        public async Task InitiateCall(string callId, string recipientId, string callType)
        {
            string callerId = Context.User.Identity.Name;

            // Kiểm tra xem cuộc gọi có tồn tại không
            var call = await _callService.GetCallByIdAsync(callId);
            if (call == null || call.CallerId != callerId)
            {
                await Clients.Caller.SendAsync("CallError", "Cuộc gọi không hợp lệ");
                return;
            }

            // Cập nhật trạng thái cuộc gọi thành đang đổ chuông
            await _callService.UpdateCallStatusAsync(callId, CallStatus.Ringing);

            // Thông báo cho người nhận về cuộc gọi đến
            if (UserConnections.TryGetValue(recipientId, out string recipientConnectionId))
            {
                await Clients.Client(recipientConnectionId).SendAsync("IncomingCall", callId, callerId, (int)call.Type);
            }
            else
            {
                // Người nhận không online, đánh dấu là cuộc gọi nhỡ
                await _callService.UpdateCallStatusAsync(callId, CallStatus.Missed);
                await Clients.Caller.SendAsync("CallMissed", "Người dùng không trực tuyến");
            }
        }

        // Phương thức để khởi tạo cuộc gọi nhóm
        public async Task InitiateGroupCall(string callId, string groupId, string callType)
        {
            string callerId = Context.User.Identity.Name;

            // Kiểm tra xem cuộc gọi có tồn tại không
            var call = await _callService.GetCallByIdAsync(callId);
            if (call == null || call.CallerId != callerId || call.GroupId != groupId)
            {
                await Clients.Caller.SendAsync("CallError", "Cuộc gọi không hợp lệ");
                return;
            }

            // Cập nhật trạng thái cuộc gọi thành đang đổ chuông
            await _callService.UpdateCallStatusAsync(callId, CallStatus.Ringing);

            // Lấy danh sách participant của cuộc gọi
            var participants = await _callService.GetCallParticipantsAsync(callId);

            // Thông báo cho từng người tham gia (trừ người gọi)
            foreach (var participant in participants.Where(p => p.UserId != callerId))
            {
                if (UserConnections.TryGetValue(participant.UserId, out string connectionId))
                {
                    await Clients.Client(connectionId).SendAsync("IncomingGroupCall", callId, callerId, groupId, (int)call.Type);
                }
            }

            // Đánh dấu người gọi đã tham gia
            await _callService.JoinCallAsync(callId, callerId);
        }

        // Phương thức để gửi offer SDP đến người nhận
        public async Task SendOffer(string callId, string targetUserId, string offerSdp)
        {
            string senderId = Context.User.Identity.Name;

            // Kiểm tra xem cả hai người dùng đều tồn tại trong danh sách kết nối
            if (!UserConnections.TryGetValue(targetUserId, out string targetConnectionId))
            {
                await Clients.Caller.SendAsync("CallError", "Người dùng không trực tuyến");
                return;
            }

            // Gửi offer đến người nhận
            await Clients.Client(targetConnectionId).SendAsync("ReceiveOffer", callId, senderId, offerSdp);
        }

        // Phương thức để gửi answer SDP đến người gọi
        public async Task SendAnswer(string callId, string targetUserId, string answerSdp)
        {
            string senderId = Context.User.Identity.Name;

            // Kiểm tra xem người gọi có trực tuyến không
            if (!UserConnections.TryGetValue(targetUserId, out string targetConnectionId))
            {
                await Clients.Caller.SendAsync("CallError", "Người dùng không trực tuyến");
                return;
            }

            // Gửi answer đến người gọi
            await Clients.Client(targetConnectionId).SendAsync("ReceiveAnswer", callId, senderId, answerSdp);

            // Đánh dấu người nhận đã tham gia cuộc gọi
            await _callService.JoinCallAsync(callId, senderId);

            // Cập nhật trạng thái cuộc gọi thành đang diễn ra
            await _callService.UpdateCallStatusAsync(callId, CallStatus.Ongoing);
        }

        // Phương thức để trao đổi ICE candidate
        public async Task SendIceCandidate(string callId, string targetUserId, string candidate)
        {
            string senderId = Context.User.Identity.Name;

            // Kiểm tra xem người nhận có trực tuyến không
            if (!UserConnections.TryGetValue(targetUserId, out string targetConnectionId))
            {
                return; // Không cần thông báo lỗi cho việc này
            }

            // Gửi ICE candidate đến người nhận
            await Clients.Client(targetConnectionId).SendAsync("ReceiveIceCandidate", callId, senderId, candidate);
        }

        // Phương thức để từ chối cuộc gọi
        public async Task DeclineCall(string callId)
        {
            string userId = Context.User.Identity.Name;

            // Lấy thông tin cuộc gọi
            var call = await _callService.GetCallByIdAsync(callId);
            if (call == null)
            {
                await Clients.Caller.SendAsync("CallError", "Cuộc gọi không tồn tại");
                return;
            }

            // Cập nhật trạng thái cuộc gọi thành bị từ chối
            await _callService.UpdateCallStatusAsync(callId, CallStatus.Declined);

            // Thông báo cho người gọi
            if (UserConnections.TryGetValue(call.CallerId, out string callerConnectionId))
            {
                await Clients.Client(callerConnectionId).SendAsync("CallDeclined", callId, userId);
            }
        }

        // Phương thức để kết thúc cuộc gọi
        public async Task EndCall(string callId)
        {
            string userId = Context.User.Identity.Name;

            // Lấy thông tin cuộc gọi
            var call = await _callService.GetCallByIdAsync(callId);
            if (call == null)
            {
                await Clients.Caller.SendAsync("CallError", "Cuộc gọi không tồn tại");
                return;
            }

            // Cập nhật trạng thái cuộc gọi thành đã kết thúc
            await _callService.UpdateCallStatusAsync(callId, CallStatus.Ended);

            // Đánh dấu người dùng hiện tại đã rời cuộc gọi
            await _callService.LeaveCallAsync(callId, userId);

            // Lấy danh sách người tham gia
            var participants = await _callService.GetCallParticipantsAsync(callId);

            // Thông báo cho tất cả người tham gia khác
            foreach (var participant in participants.Where(p => p.UserId != userId && p.HasJoined && !p.LeaveTime.HasValue))
            {
                if (UserConnections.TryGetValue(participant.UserId, out string connectionId))
                {
                    await Clients.Client(connectionId).SendAsync("CallEnded", callId, userId);
                }
            }
        }

        // Phương thức để thông báo tham gia vào cuộc gọi
        public async Task JoinCall(string callId)
        {
            string userId = Context.User.Identity.Name;

            // Đánh dấu người dùng đã tham gia cuộc gọi
            await _callService.JoinCallAsync(callId, userId);

            // Lấy thông tin cuộc gọi
            var call = await _callService.GetCallByIdAsync(callId);
            if (call == null)
            {
                await Clients.Caller.SendAsync("CallError", "Cuộc gọi không tồn tại");
                return;
            }

            // Lấy danh sách người tham gia
            var participants = await _callService.GetCallParticipantsAsync(callId);

            // Thông báo cho tất cả người tham gia khác
            foreach (var participant in participants.Where(p => p.UserId != userId && p.HasJoined && !p.LeaveTime.HasValue))
            {
                if (UserConnections.TryGetValue(participant.UserId, out string connectionId))
                {
                    await Clients.Client(connectionId).SendAsync("UserJoinedCall", callId, userId);
                }
            }

            // Gửi danh sách người đang tham gia cho người vừa join
            var activeParticipants = participants
                .Where(p => p.HasJoined && !p.LeaveTime.HasValue)
                .Select(p => p.UserId)
                .ToList();

            await Clients.Caller.SendAsync("CallParticipants", callId, activeParticipants);
        }

        // Phương thức để thông báo rời khỏi cuộc gọi
        public async Task LeaveCall(string callId)
        {
            string userId = Context.User.Identity.Name;

            // Đánh dấu người dùng đã rời cuộc gọi
            await _callService.LeaveCallAsync(callId, userId);

            // Lấy danh sách người tham gia còn lại
            var participants = await _callService.GetCallParticipantsAsync(callId);
            var activeParticipants = participants
                .Where(p => p.UserId != userId && p.HasJoined && !p.LeaveTime.HasValue)
                .ToList();

            // Thông báo cho tất cả người tham gia khác
            foreach (var participant in activeParticipants)
            {
                if (UserConnections.TryGetValue(participant.UserId, out string connectionId))
                {
                    await Clients.Client(connectionId).SendAsync("UserLeftCall", callId, userId);
                }
            }

            // Nếu không còn ai trong cuộc gọi, kết thúc cuộc gọi
            if (!activeParticipants.Any())
            {
                var call = await _callService.GetCallByIdAsync(callId);
                if (call != null && (call.Status == CallStatus.Ringing || call.Status == CallStatus.Ongoing))
                {
                    await _callService.UpdateCallStatusAsync(callId, CallStatus.Ended);
                }
            }
        }

        // Phương thức để kiểm tra trạng thái online của người dùng
        public async Task CheckOnlineStatus(string userId)
        {
            bool isOnline = UserConnections.ContainsKey(userId);
            await Clients.Caller.SendAsync("UserOnlineStatus", userId, isOnline);
        }

        // Phương thức để lấy danh sách người dùng online
        public async Task GetOnlineUsers()
        {
            await Clients.Caller.SendAsync("OnlineUsers", UserConnections.Keys.ToList());
        }

        // Xử lý tín hiệu WebRTC để điều chỉnh âm thanh/video
        public async Task MuteAudio(string callId, bool isMuted)
        {
            string userId = Context.User.Identity.Name;

            // Lấy danh sách người tham gia cuộc gọi
            var participants = await _callService.GetCallParticipantsAsync(callId);

            // Thông báo cho tất cả người tham gia khác
            foreach (var participant in participants.Where(p => p.UserId != userId && p.HasJoined && !p.LeaveTime.HasValue))
            {
                if (UserConnections.TryGetValue(participant.UserId, out string connectionId))
                {
                    await Clients.Client(connectionId).SendAsync("UserAudioState", callId, userId, isMuted);
                }
            }
        }

        // Xử lý tín hiệu WebRTC để điều chỉnh video
        public async Task MuteVideo(string callId, bool isMuted)
        {
            string userId = Context.User.Identity.Name;

            // Lấy danh sách người tham gia cuộc gọi
            var participants = await _callService.GetCallParticipantsAsync(callId);

            // Thông báo cho tất cả người tham gia khác
            foreach (var participant in participants.Where(p => p.UserId != userId && p.HasJoined && !p.LeaveTime.HasValue))
            {
                if (UserConnections.TryGetValue(participant.UserId, out string connectionId))
                {
                    await Clients.Client(connectionId).SendAsync("UserVideoState", callId, userId, isMuted);
                }
            }
        }

        // Gửi tin nhắn trong cuộc gọi
        public async Task SendCallMessage(string callId, string message)
        {
            string userId = Context.User.Identity.Name;

            // Kiểm tra xem người dùng có đang tham gia cuộc gọi không
            if (!await _callService.IsUserParticipantAsync(callId, userId))
            {
                await Clients.Caller.SendAsync("CallError", "Bạn không phải là thành viên của cuộc gọi này");
                return;
            }

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Lấy danh sách người tham gia cuộc gọi
            var participants = await _callService.GetCallParticipantsAsync(callId);

            // Gửi tin nhắn đến tất cả người tham gia
            foreach (var participant in participants.Where(p => p.HasJoined && !p.LeaveTime.HasValue))
            {
                if (UserConnections.TryGetValue(participant.UserId, out string connectionId))
                {
                    await Clients.Client(connectionId).SendAsync("CallMessage", callId, userId, message, timestamp);
                }
            }
        }

        // Phương thức để gửi tín hiệu sharing screen
        public async Task StartScreenSharing(string callId)
        {
            string userId = Context.User.Identity.Name;

            // Lấy danh sách người tham gia cuộc gọi
            var participants = await _callService.GetCallParticipantsAsync(callId);

            // Thông báo cho tất cả người tham gia khác
            foreach (var participant in participants.Where(p => p.UserId != userId && p.HasJoined && !p.LeaveTime.HasValue))
            {
                if (UserConnections.TryGetValue(participant.UserId, out string connectionId))
                {
                    await Clients.Client(connectionId).SendAsync("UserStartedScreenSharing", callId, userId);
                }
            }
        }

        // Phương thức để dừng sharing screen
        public async Task StopScreenSharing(string callId)
        {
            string userId = Context.User.Identity.Name;

            // Lấy danh sách người tham gia cuộc gọi
            var participants = await _callService.GetCallParticipantsAsync(callId);

            // Thông báo cho tất cả người tham gia khác
            foreach (var participant in participants.Where(p => p.UserId != userId && p.HasJoined && !p.LeaveTime.HasValue))
            {
                if (UserConnections.TryGetValue(participant.UserId, out string connectionId))
                {
                    await Clients.Client(connectionId).SendAsync("UserStoppedScreenSharing", callId, userId);
                }
            }
        }

        // Phương thức để kiểm tra máy ảnh và micro
        public async Task TestMediaDevices(bool hasVideo, bool hasAudio)
        {
            // Trả về kết quả kiểm tra cho client
            await Clients.Caller.SendAsync("MediaDevicesTestResult", hasVideo, hasAudio);
        }

    }
}
