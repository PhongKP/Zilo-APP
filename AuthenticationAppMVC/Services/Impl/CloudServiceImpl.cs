using AuthenticationAppMVC.Data;
using AuthenticationAppMVC.Models;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationAppMVC.Services.Impl
{
    public class CloudServiceImpl : ICloudService
    {
        private readonly AppDBContext _appDbContext;
        private readonly Cloudinary _cloudinary;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly ILogger<CloudServiceImpl> _logger;

        public CloudServiceImpl(AppDBContext appDbContext, Cloudinary cloudinary, ILogger<CloudServiceImpl> logger,
            IWebHostEnvironment hostingEnvironment
        )
        {
            this._appDbContext = appDbContext;
            this._logger = logger;
            _hostingEnvironment = hostingEnvironment;
            this._cloudinary = cloudinary;
        }

        public async Task DeleteCloudMessageAsync(string messageId, string userId)
        {
            try
            {
                var message = await _appDbContext.CloudMessages
                    .Include(m => m.Attachments)
                    .FirstOrDefaultAsync(m => m.Id == messageId && m.UserId == userId);

                if (message == null)
                {
                    throw new InvalidOperationException("Tin nhắn không tồn tại hoặc không thuộc về bạn.");
                }

                // Tính tổng dung lượng các file đính kèm
                long totalSize = message.Attachments?.Sum(a => a.FileSize) ?? 0;

                // Xóa file trên Cloudinary
                foreach (var attachment in message.Attachments)
                {
                    if (!string.IsNullOrEmpty(attachment.PublicId))
                    {
                        var deletionParams = new DeletionParams(attachment.PublicId);
                        var result = await _cloudinary.DestroyAsync(deletionParams);

                        if (result.Error != null)
                        {
                            _logger.LogWarning($"Failed to delete file from Cloudinary: {result.Error.Message}");
                        }
                    }

                    _appDbContext.CloudAttachments.Remove(attachment);
                }

                // Xóa tin nhắn
                _appDbContext.CloudMessages.Remove(message);

                // Cập nhật dung lượng đã sử dụng
                var userStorage = await _appDbContext.UserStorages.FindAsync(userId);
                userStorage.StorageUsed -= totalSize;
                if (userStorage.StorageUsed < 0)
                    userStorage.StorageUsed = 0;

                await _appDbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting cloud message {messageId} for user {userId}");
                throw;
            }
        }

        public async Task EnsureUserStorageExistsAsync(string userId)
        {
            var userStorage = await _appDbContext.UserStorages.FindAsync(userId);
            if (userStorage == null)
            {
                userStorage = new UserStorage
                {
                    UserId = userId,
                    StorageUsed = 0,
                    StorageLimit = 104857600
                };

                _appDbContext.UserStorages.Add(userStorage);
                await _appDbContext.SaveChangesAsync();
            }
        }

        public async Task<CloudMessage> GetCloudMessageDetailsAsync(string messageId, string userId)
        {
            try
            {
                var message = await _appDbContext.CloudMessages
                    .Include(m => m.Attachments)
                    .FirstOrDefaultAsync(m => m.Id == messageId && m.UserId == userId);

                if (message == null)
                {
                    throw new InvalidOperationException("Tin nhắn không tồn tại hoặc không thuộc về bạn.");
                }

                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting cloud message details for message {messageId}, user {userId}");
                throw;
            }
        }

        public async Task<List<CloudMessage>> GetUserCloudMessagesAsync(string userId)
        {
            try
            {
                return await _appDbContext.CloudMessages
                    .AsNoTracking()
                    .Include(m => m.Attachments)
                    .Where(m => m.UserId == userId)
                    .OrderBy(m => m.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting cloud messages for user {userId}");
                throw;
            }
        }

        public async Task<UserStorage> GetUserStorageInfoAsync(string userId)
        {
            try
            {
                await EnsureUserStorageExistsAsync(userId);
                return await _appDbContext.UserStorages.FindAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting storage info for user {userId}");
                throw;
            }
        }

        public async Task<bool> HasEnoughStorageAsync(string userId, long fileSize)
        {
            await EnsureUserStorageExistsAsync(userId);
            var userStorage = await _appDbContext.UserStorages.FindAsync(userId);
            return (userStorage?.StorageUsed + fileSize) <= userStorage?.StorageLimit;
        }

        public async Task<CloudMessage> SaveMultipleToCloudAsync(string userId, string content, List<IFormFile> files)
        {
            try
            {
                // Kiểm tra dung lượng
                long totalSize = files.Sum(f => f.Length);
                if (!await HasEnoughStorageAsync(userId, totalSize))
                {
                    throw new InvalidOperationException("Không đủ dung lượng lưu trữ.");
                }

                // Tạo CloudMessage
                var cloudMessage = new CloudMessage
                {
                    UserId = userId,
                    Content = content,
                    HasAttachment = files.Count > 0
                };

                _appDbContext.CloudMessages.Add(cloudMessage);
                await _appDbContext.SaveChangesAsync();

                // Upload các file và tạo CloudAttachment
                foreach (var file in files)
                {
                    using var stream = file.OpenReadStream();
                    var uploadParams = new ImageUploadParams
                    {
                        File = new FileDescription(file.FileName, stream),
                        Folder = $"zilo-cloud/{userId}",
                        UseFilename = true,
                    };

                    var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                    if (uploadResult.Error != null)
                    {
                        _logger.LogError($"Cloudinary upload error: {uploadResult.Error.Message}");
                        throw new InvalidOperationException($"Lỗi khi tải file lên Cloudinary: {uploadResult.Error.Message}");
                    }

                    var attachment = new CloudAttachment
                    {
                        CloudMessageId = cloudMessage.Id,
                        FileName = file.FileName,
                        ContentType = file.ContentType,
                        StoragePath = uploadResult.SecureUrl.ToString(),
                        PublicId = uploadResult.PublicId,
                        FileSize = file.Length,
                        UploaderId = userId
                    };

                    _appDbContext.CloudAttachments.Add(attachment);
                }

                // Cập nhật dung lượng đã sử dụng
                var userStorage = await _appDbContext.UserStorages.FindAsync(userId);
                userStorage!.StorageUsed += totalSize;

                await _appDbContext.SaveChangesAsync();

                return cloudMessage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving multiple files to cloud for user {userId}");
                throw;
            }
        }

        public async Task<CloudMessage> SaveTextToCloudAsync(string userId, string content)
        {
            // Kiểm tra người dùng có tồn tại không
            var user = await _appDbContext.Users.FindAsync(userId);
            if (user == null)
            {
                throw new ArgumentException("Người dùng không tồn tại");
            }

            // Tạo cloud message
            var cloudMessage = new CloudMessage
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                Content = content,
                CreatedAt = DateTime.UtcNow,
                HasAttachment = false
            };

            // Lưu vào database
            _appDbContext.CloudMessages.Add(cloudMessage);
            await _appDbContext.SaveChangesAsync();

            return cloudMessage;
        }

        public async Task<CloudMessage> SaveToCloudAsync(string userId, string content, IFormFile file)
        {
            try
            {
                if (!await HasEnoughStorageAsync(userId, file.Length))
                {
                    throw new InvalidOperationException("Không đủ dung lượng lưu trữ.");
                }

                // Upload file lên Cloudinary
                using var stream = file.OpenReadStream();
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = $"zilo-cloud/{userId}",
                    UseFilename = true
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                if (uploadResult.Error != null)
                {
                    _logger.LogError($"Cloudinary upload error: {uploadResult.Error.Message}");
                    throw new InvalidOperationException($"Lỗi khi tải file lên Cloudinary: {uploadResult.Error.Message}");
                }

                // Tạo CloudMessage
                var cloudMessage = new CloudMessage
                {
                    UserId = userId,
                    Content = content,
                    HasAttachment = true
                };

                _appDbContext.CloudMessages.Add(cloudMessage);
                await _appDbContext.SaveChangesAsync();

                // Tạo CloudAttachment
                var attachment = new CloudAttachment
                {
                    CloudMessageId = cloudMessage.Id,
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    StoragePath = uploadResult.SecureUrl.ToString(),
                    PublicId = uploadResult.PublicId,
                    FileSize = file.Length,
                    UploaderId = userId
                };

                _appDbContext.CloudAttachments.Add(attachment);

                // Cập nhật dung lượng đã sử dụng
                var userStorage = await _appDbContext.UserStorages.FindAsync(userId);
                userStorage!.StorageUsed += file.Length;

                await _appDbContext.SaveChangesAsync();

                return cloudMessage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving file to cloud for user {userId}");
                throw;
            }
        }

        public async Task UpdateStorageLimitAsync(string userId, long newLimit)
        {
            try
            {
                await EnsureUserStorageExistsAsync(userId);
                var userStorage = await _appDbContext.UserStorages.FindAsync(userId);
                userStorage!.StorageLimit = newLimit;
                await _appDbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating storage limit for user {userId}");
                throw;
            }
        }
    }
}
