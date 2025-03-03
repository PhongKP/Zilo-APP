using AuthenticationAppMVC.Data;
using AuthenticationAppMVC.Models;

namespace AuthenticationAppMVC.Services
{
    public class FileService
    {
        private readonly AppDBContext _dbContext;
        private readonly string _uploadsFolder;

        public FileService(AppDBContext dbContext, IWebHostEnvironment environment)
        {
            _dbContext = dbContext;
            // Tạo thư mục uploads trong wwwroot nếu chưa tồn tại
            _uploadsFolder = Path.Combine(environment.WebRootPath, "uploads");
            if (!Directory.Exists(_uploadsFolder))
            {
                Directory.CreateDirectory(_uploadsFolder);
            }
        }

        public async Task<FileAttachment> SaveFileAsync(IFormFile file, string uploaderId)
        {
            // Tạo tên file độc nhất
            string uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";

            // Đường dẫn đầy đủ đến file
            string filePath = Path.Combine(_uploadsFolder, uniqueFileName);

            // Lưu file vào hệ thống
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            // Tạo bản ghi FileAttachment
            var fileAttachment = new FileAttachment
            {
                FileName = file.FileName,
                ContentType = file.ContentType,
                StoragePath = uniqueFileName, 
                FileSize = file.Length,
                UploaderId = uploaderId
            };

            // Thêm vào database
            _dbContext.FileAttachments.Add(fileAttachment);
            await _dbContext.SaveChangesAsync();

            return fileAttachment;
        }

        /// <param name="storagePath">Tên file được lưu trong StoragePath</param>
        public async Task<byte[]> GetFileStreamAsync(string storagePath)
        {
            if (string.IsNullOrEmpty(storagePath))
            {
                throw new ArgumentNullException(nameof(storagePath));
            }

            // Đường dẫn đầy đủ đến file
            string filePath = Path.Combine(_uploadsFolder, storagePath);

            // Kiểm tra xem file có tồn tại không
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found at: {storagePath}");
            }

            // Đọc nội dung file và trả về byte array
            return await File.ReadAllBytesAsync(filePath);
        }

        /// <param name="storagePath">Tên file được lưu trong StoragePath</param>
        public async Task DeleteFileAsync(string storagePath)
        {
            if (string.IsNullOrEmpty(storagePath))
            {
                throw new ArgumentNullException(nameof(storagePath));
            }

            // Đường dẫn đầy đủ đến file
            string filePath = Path.Combine(_uploadsFolder, storagePath);

            // Kiểm tra xem file có tồn tại không
            if (!File.Exists(filePath))
            {
                return;
            }

            // Xóa file từ hệ thống
            File.Delete(filePath);
            await Task.CompletedTask;
        }
    }
}
