using AuthenticationAppMVC.Models;

namespace AuthenticationAppMVC.Services
{
    public interface ICallService
    {
        // Các phương thức liên quan đến Call
        Task<Call> CreateCallAsync(string callerId, string recipientId, CallType callType);
        Task<Call> CreateGroupCallAsync(string callerId, string groupId, CallType callType);
        Task<Call> UpdateCallStatusAsync(string callId, CallStatus status);
        Task<Call> EndCallAsync(string callId);
        Task<Call> GetCallByIdAsync(string callId);
        Task<IEnumerable<Call>> GetUserCallHistoryAsync(string userId, int pageNumber = 1, int pageSize = 10);
        Task<Call> GetUserActiveCallAsync(string userId);
        Task<IEnumerable<Call>> GetUserMissedCallsAsync(string userId);
        Task<bool> IsUserInCallAsync(string userId);

        // Các phương thức liên quan đến CallParticipant
        Task<CallParticipant> AddParticipantAsync(string callId, string userId);
        Task<CallParticipant> JoinCallAsync(string callId, string userId);
        Task<CallParticipant> LeaveCallAsync(string callId, string userId);
        Task<IEnumerable<CallParticipant>> GetCallParticipantsAsync(string callId);
        Task<bool> IsUserParticipantAsync(string callId, string userId);
        Task<CallParticipant> GetParticipantAsync(string callId, string userId);
    }
}
