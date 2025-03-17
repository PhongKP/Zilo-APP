using AuthenticationAppMVC.Data;
using AuthenticationAppMVC.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationAppMVC.Services.Impl
{
    public class CallServiceImpl : ICallService
    {

        private readonly AppDBContext _dbContext;

        public CallServiceImpl(AppDBContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<CallParticipant> AddParticipantAsync(string callId, string userId)
        {
            var existingParticipant = await _dbContext.CallParticipants
                .FirstOrDefaultAsync(p => p.CallId == callId && p.UserId == userId);

            if (existingParticipant != null)
                return existingParticipant;

            var participant = new CallParticipant
            {
                CallId = callId,
                UserId = userId,
                HasJoined = false,
            };

            _dbContext.CallParticipants.Add(participant);
            await _dbContext.SaveChangesAsync();

            return participant; 
        }

        public async Task<Call> CreateCallAsync(string callerId, string recipientId, CallType callType)
        {
            var call = new Call
            {
                CallerId = callerId,
                RecipientId = recipientId,
                GroupId = null,
                Type = callType,
                Status = CallStatus.Initiated,
                StartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            _dbContext.Calls.Add(call);
            await _dbContext.SaveChangesAsync();

            // Tự động thêm người gọi và người nhận vào danh sách participant
            await AddParticipantAsync(call.Id, callerId);
            await AddParticipantAsync(call.Id, recipientId);

            return call;
        }

        public async Task<Call> CreateGroupCallAsync(string callerId, string groupId, CallType callType)
        {
            var call = new Call
            {
                CallerId = callerId,
                RecipientId = null,
                GroupId = groupId,
                Type = callType,
                Status = CallStatus.Initiated,
                StartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            _dbContext.Calls.Add(call);
            await _dbContext.SaveChangesAsync();

            // Thêm người gọi vào danh sách participant và đánh dấu là đã tham gia
            await AddParticipantAsync(call.Id, callerId);
            await JoinCallAsync(call.Id, callerId);

            // Lấy danh sách thành viên trong nhóm
            var groupMembers = await _dbContext.GroupMembers
                                .Where(gm => gm.GroupId == groupId)
                                .Select(gm => gm.UserId)
                                .ToListAsync();

            // Thêm tất cả thành viên nhóm (trừ người gọi) vào danh sách participant
            foreach (var memberId in groupMembers.Where(uid => uid != callerId))
            {
                await AddParticipantAsync(call.Id, memberId);
            }

            return call;
        }

        public async Task<Call> EndCallAsync(string callId)
        {
            return await UpdateCallStatusAsync(callId, CallStatus.Ended);
        }

        public async Task<Call?> GetCallByIdAsync(string callId)
        {
            return await _dbContext.Calls
                .Include(c => c.Caller)
                .Include(c => c.Recipient)
                .Include(c => c.Group)
                .Include(c => c.Participants)
                    .ThenInclude(p => p.User)
                .FirstOrDefaultAsync(c => c.Id == callId);
        }

        public async Task<IEnumerable<CallParticipant>> GetCallParticipantsAsync(string callId)
        {
            return await _dbContext.CallParticipants
                .Where(p => p.CallId == callId)
                .Include(p => p.User)
                .ToListAsync();
        }

        public async Task<CallParticipant?> GetParticipantAsync(string callId, string userId)
        {
            return await _dbContext.CallParticipants
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.CallId == callId && p.UserId == userId);
        }

        public async Task<Call?> GetUserActiveCallAsync(string userId)
        {
            return await _dbContext.Calls
                .Where(c =>
                    (c.CallerId == userId || c.RecipientId == userId || c.Participants.Any(p => p.UserId == userId)) &&
                    (c.Status == CallStatus.Initiated || c.Status == CallStatus.Ringing || c.Status == CallStatus.Ongoing))
                .Include(c => c.Caller)
                .Include(c => c.Recipient)
                .Include(c => c.Group)
                .Include(c => c.Participants)
                    .ThenInclude(p => p.User)
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<Call>> GetUserCallHistoryAsync(string userId, int pageNumber = 1, int pageSize = 10)
        {
            return await _dbContext.Calls
                .Where(c =>
                    c.CallerId == userId ||
                    c.RecipientId == userId ||
                    c.Participants.Any(p => p.UserId == userId))
                .OrderByDescending(c => c.StartTime)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Include(c => c.Caller)
                .Include(c => c.Recipient)
                .Include(c => c.Group)
                .ToListAsync();
        }

        public async Task<IEnumerable<Call>> GetUserMissedCallsAsync(string userId)
        {
            return await _dbContext.Calls
                .Where(c =>
                    c.RecipientId == userId &&
                    c.Status == CallStatus.Missed)
                .OrderByDescending(c => c.StartTime)
                .Include(c => c.Caller)
                .Include(c => c.Recipient)
                .ToListAsync();
        }

        public async Task<bool> IsUserInCallAsync(string userId)
        {
            return await _dbContext.Calls
                .AnyAsync(c =>
                    (c.CallerId == userId || c.RecipientId == userId || c.Participants.Any(p => p.UserId == userId)) &&
                    (c.Status == CallStatus.Initiated || c.Status == CallStatus.Ringing || c.Status == CallStatus.Ongoing)
                );
        }

        public async Task<bool> IsUserParticipantAsync(string callId, string userId)
        {
            return await _dbContext.CallParticipants
                .AnyAsync(p => p.CallId == callId && p.UserId == userId);
        }

        public async Task<CallParticipant> JoinCallAsync(string callId, string userId)
        {
            var participant = await _dbContext.CallParticipants
                .FirstOrDefaultAsync(p => p.CallId == callId && p.UserId == userId);

            if (participant == null)
            {
                // Nếu chưa có participant, tạo mới và đánh dấu đã tham gia
                participant = await AddParticipantAsync(callId, userId);
            }

            participant.HasJoined = true;
            participant.JoinTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            _dbContext.CallParticipants.Update(participant);
            await _dbContext.SaveChangesAsync();

            // Cập nhật trạng thái cuộc gọi thành "Đang diễn ra" nếu đây là người đầu tiên tham gia
            var call = await _dbContext.Calls.FindAsync(callId);
            if (call != null && call.Status != CallStatus.Ongoing)
            {
                call.Status = CallStatus.Ongoing;
                _dbContext.Calls.Update(call);
                await _dbContext.SaveChangesAsync();
            }

            return participant;
        }

        public async Task<CallParticipant?> LeaveCallAsync(string callId, string userId)
        {
            var participant = await _dbContext.CallParticipants
                .FirstOrDefaultAsync(p => p.CallId == callId && p.UserId == userId);

            if (participant == null)
                return null;

            participant.LeaveTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            _dbContext.CallParticipants.Update(participant);
            await _dbContext.SaveChangesAsync();

            // Kiểm tra xem còn participant nào đang trong cuộc gọi không
            var activeParticipants = await _dbContext.CallParticipants
                .CountAsync(p => p.CallId == callId && p.HasJoined && p.LeaveTime == null);

            // Nếu không còn ai trong cuộc gọi, kết thúc cuộc gọi
            if (activeParticipants == 0)
            {
                var call = await _dbContext.Calls.FindAsync(callId);
                if (call != null && (call.Status == CallStatus.Ongoing || call.Status == CallStatus.Initiated || call.Status == CallStatus.Ringing))
                {
                    call.Status = CallStatus.Ended;
                    call.EndTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    _dbContext.Calls.Update(call);
                    await _dbContext.SaveChangesAsync();
                }
            }
            return participant;
        }

        public async Task<Call> UpdateCallStatusAsync(string callId, CallStatus status)
        {
            var call = await _dbContext.Calls.FindAsync(callId);

            if (call == null)
                return null;

            call.Status = status;

            // Nếu cuộc gọi kết thúc, cập nhật thời gian kết thúc
            if (status == CallStatus.Ended || status == CallStatus.Declined || status == CallStatus.Missed)
            {
                call.EndTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }

            _dbContext.Calls.Update(call);
            await _dbContext.SaveChangesAsync();

            return call;
        }
    }
}
