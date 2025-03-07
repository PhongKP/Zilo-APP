using AuthenticationAppMVC.Data;
using AuthenticationAppMVC.Models;
using AuthenticationAppMVC.ViewModels;

namespace AuthenticationAppMVC.Services
{
    public interface IFriendsService
    {
        /// <summary>
        /// Gửi lời mời kết bạn từ người dùng gửi đến người dùng nhận
        /// </summary>
        /// <param name="senderId">ID của người gửi lời mời</param>
        /// <param name="receiverId">ID của người nhận lời mời</param>
        /// <returns>True nếu gửi thành công, False nếu không thành công</returns>
        Task<bool> SendFriendRequestAsync(string senderId, string receiverId);

        /// <summary>
        /// Chấp nhận lời mời kết bạn
        /// </summary>
        /// <param name="requestId">ID của lời mời kết bạn</param>
        /// <param name="currentUserId">ID của người dùng hiện tại (phải là người nhận)</param>
        /// <returns>True nếu chấp nhận thành công, False nếu không thành công</returns>
        Task<bool> AcceptFriendRequestAsync(string requestId, string currentUserId);

        /// <summary>
        /// Từ chối lời mời kết bạn
        /// </summary>
        /// <param name="requestId">ID của lời mời kết bạn</param>
        /// <param name="currentUserId">ID của người dùng hiện tại (phải là người nhận)</param>
        /// <returns>True nếu từ chối thành công, False nếu không thành công</returns>
        Task<bool> DeclineFriendRequestAsync(string requestId, string currentUserId);

        /// <summary>
        /// Hủy lời mời kết bạn đã gửi
        /// </summary>
        /// <param name="requestId">ID của lời mời kết bạn</param>
        /// <param name="currentUserId">ID của người dùng hiện tại (phải là người gửi)</param>
        /// <returns>True nếu hủy thành công, False nếu không thành công</returns>
        Task<bool> CancelFriendRequestAsync(string requestId, string currentUserId);

        /// <summary>
        /// Lấy thông tin chi tiết của một lời mời kết bạn
        /// </summary>
        /// <param name="requestId">ID của lời mời kết bạn</param>
        /// <returns>Thông tin lời mời kết bạn, null nếu không tìm thấy</returns>
        Task<FriendRequest> GetFriendRequestByIdAsync(string requestId);

        /// <summary>
        /// Lấy danh sách lời mời kết bạn đã nhận của một người dùng
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <returns>Danh sách lời mời kết bạn đang chờ xử lý</returns>
        Task<List<FriendRequestDTO>> GetPendingFriendRequestsAsync(string userId);

        /// <summary>
        /// Lấy danh sách lời mời kết bạn đã gửi của một người dùng
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <returns>Danh sách lời mời kết bạn đã gửi và đang chờ phản hồi</returns>
        Task<List<FriendRequestDTO>> GetSentFriendRequestsAsync(string userId);

        /// <summary>
        /// Lấy danh sách bạn bè của một người dùng
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <returns>Danh sách người dùng là bạn bè</returns>
        Task<List<User>> GetFriendsAsync(string userId);

        /// <summary>
        /// Kiểm tra trạng thái mối quan hệ giữa hai người dùng
        /// </summary>
        /// <param name="userId1">ID của người dùng thứ nhất</param>
        /// <param name="userId2">ID của người dùng thứ hai</param>
        /// <returns>
        /// - "NotFriends": Không phải bạn bè
        /// - "RequestSent": Đã gửi lời mời kết bạn (từ userId1 đến userId2)
        /// - "RequestReceived": Đã nhận lời mời kết bạn (từ userId2 đến userId1)
        /// - "Friends": Đã là bạn bè
        /// </returns>
        Task<string> GetFriendshipStatusAsync(string userId1, string userId2);

        /// <summary>
        /// Hủy kết bạn giữa hai người dùng
        /// </summary>
        /// <param name="userId1">ID của người dùng thứ nhất</param>
        /// <param name="userId2">ID của người dùng thứ hai</param>
        /// <returns>True nếu hủy thành công, False nếu không thành công</returns>
        Task<bool> RemoveFriendAsync(string userId1, string userId2);

        /// <summary>
        /// Tìm kiếm bạn bè dựa trên từ khóa
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <param name="searchTerm">Từ khóa tìm kiếm (tên, email, etc.)</param>
        /// <returns>Danh sách người dùng là bạn bè và phù hợp với từ khóa</returns>
        Task<List<User>> SearchFriendsAsync(string userId, string searchTerm);
    }
}
