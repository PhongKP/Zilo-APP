using AuthenticationAppMVC.Models;

namespace AuthenticationAppMVC.Services
{
    public interface ICloudService
    {
        /// <summary>
        /// Khởi tạo bản ghi UserStorage cho người dùng nếu chưa có
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        Task EnsureUserStorageExistsAsync(string userId);

        /// <summary>
        /// Kiểm tra người dùng có đủ dung lượng lưu trữ không
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <param name="fileSize">Kích thước file (bytes)</param>
        /// <returns>True nếu có đủ dung lượng, ngược lại là False</returns>
        Task<bool> HasEnoughStorageAsync(string userId, long fileSize);

        /// <summary>
        /// Lưu một file lên cloud
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <param name="content">Nội dung tin nhắn</param>
        /// <param name="file">File cần lưu</param>
        /// <returns>CloudMessage đã được tạo</returns>
        Task<CloudMessage> SaveToCloudAsync(string userId, string content, IFormFile file);

        /// <summary>
        /// Lưu nhiều file lên cloud
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <param name="content">Nội dung tin nhắn</param>
        /// <param name="files">Danh sách file cần lưu</param>
        /// <returns>CloudMessage đã được tạo</returns>
        Task<CloudMessage> SaveMultipleToCloudAsync(string userId, string content, List<IFormFile> files);

        /// <summary>
        /// Lấy danh sách tin nhắn cloud của người dùng
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <returns>Danh sách CloudMessage</returns>
        Task<List<CloudMessage>> GetUserCloudMessagesAsync(string userId);

        /// <summary>
        /// Xóa tin nhắn cloud và các file đính kèm
        /// </summary>
        /// <param name="messageId">ID của tin nhắn</param>
        /// <param name="userId">ID của người dùng</param>
        Task DeleteCloudMessageAsync(string messageId, string userId);

        /// <summary>
        /// Lấy thông tin dung lượng lưu trữ của người dùng
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <returns>Thông tin dung lượng đã dùng và giới hạn</returns>
        Task<UserStorage> GetUserStorageInfoAsync(string userId);

        /// <summary>
        /// Cập nhật giới hạn dung lượng lưu trữ của người dùng
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <param name="newLimit">Giới hạn mới (bytes)</param>
        Task UpdateStorageLimitAsync(string userId, long newLimit);

        /// <summary>
        /// Lấy chi tiết của một CloudMessage
        /// </summary>
        /// <param name="messageId">ID của tin nhắn</param>
        /// <param name="userId">ID của người dùng</param>
        /// <returns>CloudMessage với các Attachment</returns>
        Task<CloudMessage> GetCloudMessageDetailsAsync(string messageId, string userId);

        /// <summary>
        /// Lưu một tin nhắn lên cloud
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <param name="content">Nội dung tin nhắn</param>
        /// <returns>CloudMessage đã được tạo</returns>
        Task<CloudMessage> SaveTextToCloudAsync(string userId, string content);
    }
}
