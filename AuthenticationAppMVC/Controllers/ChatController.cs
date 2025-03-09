
using AuthenticationAppMVC.Data;
using AuthenticationAppMVC.Hubs;
using AuthenticationAppMVC.Models;
using AuthenticationAppMVC.Services;
using AuthenticationAppMVC.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationAppMVC.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly AppDBContext _dbContext;
        private readonly UserManager<User> userManager;
        private readonly IHubContext<Chathub> hubContext;
        private readonly FileService fileService;
        private readonly ILogger<ChatController> logger;
        private readonly ICloudService cloudService;

        public ChatController(
            AppDBContext appDBContext,
            UserManager<User> userManager,
            IHubContext<Chathub> hubContext,
            FileService fileService,
            ILogger<ChatController> logger,
            ICloudService cloudService
        )
        {
            _dbContext = appDBContext;
            this.userManager = userManager;
            this.hubContext = hubContext;
            this.fileService = fileService;
            this.logger = logger;
            this.cloudService = cloudService;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var users = await _dbContext.Users.Where(u => u.Id != currentUser.Id).ToListAsync();
            ViewData["HideFooter"] = true;
            ViewBag.ShowCloudStorage = true;
            return View(users);
        }

        [HttpGet]
        public async Task<IActionResult> GetMessages(string receiverId)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            string receiverEmailReal = "";
            var receiver = await userManager.FindByIdAsync(receiverId);
            if (receiver != null)
            {
                receiverEmailReal = receiver.Email!;
            }

            // Lấy danh sách tin nhắn không kèm attachments
            var messages = await _dbContext.Messages
                .Where(m => (m.SenderEmail == currentUser.Email && m.ReceiverEmail == receiverId) ||
                            (m.SenderEmail == receiverEmailReal && m.ReceiverEmail == currentUser.Id))
                .OrderBy(m => m.Timestamp)
                .Select(m => new
                {
                    m.Id,
                    m.Content,
                    m.SenderEmail,
                    m.Timestamp,
                    m.HasAttachment,
                    Status = (int)m.Status
                })
                .ToListAsync();

            // Lấy IDs của các tin nhắn có attachment
            var messageIds = messages.Where(m => m.HasAttachment).Select(m => m.Id).ToList();

            // Lấy các attachments riêng biệt và chuyển thành AttachmentDto ngay
            var attachments = await _dbContext.FileAttachments
                .Where(a => a.MessageId != null && messageIds.Contains(a.MessageId.Value))
                .Select(a => new
                {
                    MessageId = a.MessageId,
                    AttachmentInfo = new AttachmentDTO
                    {
                        Id = a.Id,
                        FileName = a.FileName,
                        ContentType = a.ContentType,
                        FileSize = a.FileSize
                    }
                })
                .ToListAsync();

            // Nhóm attachments theo MessageId
            var attachmentsByMessageId = attachments?
                .GroupBy(a => a.MessageId)
                ?.ToDictionary(g => g.Key, g => g.Select(a => a.AttachmentInfo).ToList());

            var result = messages.Select(m => new
            {
                m.Id,
                m.Content,
                m.SenderEmail,
                m.Timestamp,
                m.HasAttachment,
                m.Status,
                Attachments = m.HasAttachment && attachmentsByMessageId.ContainsKey(m.Id)
                ? attachmentsByMessageId[m.Id]
                    : new List<AttachmentDTO>()
            }).ToList();

            // Đánh dấu tin nhắn là đã xem nếu người dùng hiện tại là người nhận
            var unreadMessages = await _dbContext.Messages
                .Where(m => m.ReceiverEmail == currentUser.Id && m.Status != MessageStatus.Read && m.SenderEmail == receiverEmailReal)
                .ToListAsync();

            if (unreadMessages.Any())
            {
                foreach (var message in unreadMessages)
                {
                    message.Status = MessageStatus.Read;
                }

                await _dbContext.SaveChangesAsync();

                foreach (var message in unreadMessages)
                {
                    await hubContext.Clients.User(message.SenderEmail).SendAsync(
                        "MessageStatusUpdated",
                        message.Id,
                        (int)MessageStatus.Read);
                }
            }

            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> Cloud()
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            try
            {
                // Đảm bảo người dùng có bản ghi dung lượng lưu trữ
                await cloudService.EnsureUserStorageExistsAsync(currentUser.Id);

                // Lấy danh sách tin nhắn cloud storage của người dùng
                var cloudMessages = await cloudService.GetUserCloudMessagesAsync(currentUser.Id);

                // Lấy thông tin về dung lượng lưu trữ
                var storageInfo = await cloudService.GetUserStorageInfoAsync(currentUser.Id);

                // Tính phần trăm dung lượng đã sử dụng
                double usedPercentage = (double)storageInfo.StorageUsed / storageInfo.StorageLimit * 100;

                // Chuyển đổi bytes sang đơn vị dễ đọc
                ViewBag.UsedStorage = FormatFileSize(storageInfo.StorageUsed);
                ViewBag.TotalStorage = FormatFileSize(storageInfo.StorageLimit);
                ViewBag.UsedPercentage = usedPercentage;
                ViewBag.CloudMessages = cloudMessages;
                ViewBag.IsCloud = true;

                // Hiển thị view Chat với flag IsCloudStorage = true
                var model = new ChatViewModel
                {
                    ContactId = null,
                    Messages = new List<Message>(),
                    IsCloudStorage = true
                };

                return View("Chat", model);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi hiển thị Cloud Storage");
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi tải dữ liệu Cloud Storage. Vui lòng thử lại sau.";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public async Task<IActionResult> UploadToCloud([FromForm] string content, List<IFormFile> files)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            if (files == null || !files.Any())
            {
                return Json(new { success = false, message = "Vui lòng chọn ít nhất một file." });
            }

            try
            {
                // Kiểm tra dung lượng của tất cả file
                long totalSize = files.Sum(f => f.Length);
                if (!await cloudService.HasEnoughStorageAsync(currentUser.Id, totalSize))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Không đủ dung lượng lưu trữ. Vui lòng xóa bớt file hoặc nâng cấp gói lưu trữ."
                    });
                }

                // Lưu các file lên cloud
                var cloudMessage = await cloudService.SaveMultipleToCloudAsync(currentUser.Id, content, files);

                return Json(new
                {
                    success = true,
                    message = "Tải file lên thành công!",
                    cloudMessage = new
                    {
                        id = cloudMessage.Id,
                        content = cloudMessage.Content,
                        createdAt = cloudMessage.CreatedAt,
                        attachments = cloudMessage.Attachments.Select(a => new {
                            id = a.Id,
                            fileName = a.FileName,
                            contentType = a.ContentType,
                            fileSize = a.FileSize,
                            storagePath = a.StoragePath
                        }).ToList()
                    }
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi tải file lên cloud");
                return Json(new
                {
                    success = false,
                    message = "Đã xảy ra lỗi khi tải file lên. Vui lòng thử lại sau."
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCloudMessage(string messageId)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            try
            {
                await cloudService.DeleteCloudMessageAsync(messageId, currentUser.Id);
                return Json(new { success = true, message = "Xóa file thành công!" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Lỗi khi xóa tin nhắn cloud {messageId}");
                return Json(new
                {
                    success = false,
                    message = "Đã xảy ra lỗi khi xóa file. Vui lòng thử lại sau."
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCloudStorageData()
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            try
            {
                // Lấy danh sách tin nhắn cloud storage của người dùng
                var cloudMessages = await cloudService.GetUserCloudMessagesAsync(currentUser.Id);

                // Lấy thông tin về dung lượng lưu trữ
                var storageInfo = await cloudService.GetUserStorageInfoAsync(currentUser.Id);

                // Tính phần trăm dung lượng đã sử dụng
                double usedPercentage = (double)storageInfo.StorageUsed / storageInfo.StorageLimit * 100;

                return Json(new
                {
                    success = true,
                    messages = cloudMessages.Select(m => new {
                        id = m.Id,
                        content = m.Content,
                        createdAt = m.CreatedAt,
                        hasAttachment = m.HasAttachment,
                        attachments = m.Attachments.Select(a => new {
                            id = a.Id,
                            fileName = a.FileName,
                            contentType = a.ContentType,
                            fileSize = a.FileSize,
                            storagePath = a.StoragePath
                        }).ToList()
                    }).ToList(),
                    storageInfo = new
                    {
                        used = storageInfo.StorageUsed,
                        limit = storageInfo.StorageLimit,
                        usedFormatted = FormatFileSize(storageInfo.StorageUsed),
                        limitFormatted = FormatFileSize(storageInfo.StorageLimit),
                        percentage = usedPercentage
                    }
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi lấy dữ liệu Cloud Storage");
                return Json(new { success = false, message = "Đã xảy ra lỗi khi tải dữ liệu Cloud Storage." });
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB", "PB" };
            int counter = 0;
            decimal number = bytes;

            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                counter++;
            }

            return $"{number:n2} {suffixes[counter]}";
        }

        [HttpGet]
        public async Task<IActionResult> GetMessageStatus(long messageId)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var message = await _dbContext.Messages.FindAsync(messageId);
            if (message == null) return NotFound();

            // Kiểm tra quyền truy cập (sender hoặc receiver)
            if (message.SenderEmail != currentUser.Email && message.ReceiverEmail != currentUser.Id)
            {
                return Forbid();
            }

            return Ok(new { status = (int)message.Status });
        }

        [HttpGet]
        public async Task<IActionResult> GetMessageStatuses(string receiverId, int limit = 50)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            // Lấy trạng thái của các tin nhắn đã gửi cho người nhận cụ thể
            var messages = await _dbContext.Messages
                .Where(m => m.SenderEmail == currentUser.Email && m.ReceiverEmail == receiverId)
                .OrderByDescending(m => m.Timestamp)
                .Take(limit)
                .Select(m => new { m.Id, m.Timestamp, Status = (int)m.Status })
                .ToListAsync();

            return Ok(messages);
        }

        [HttpPost]
        public async Task<IActionResult> MarkMessageAsRead([FromBody] long messageId)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var message = await _dbContext.Messages
                .FirstOrDefaultAsync(m => m.Id == messageId && m.ReceiverEmail == currentUser.Id);

            if (message == null) return NotFound();

            if (message.Status != MessageStatus.Read)
            {
                message.Status = MessageStatus.Read;
                await _dbContext.SaveChangesAsync();

                await hubContext.Clients.User(message.SenderEmail).SendAsync(
                    "MessageStatusUpdated",
                    message.Id,
                    (int)MessageStatus.Read);
            }

            return Ok(new { status = (int)message.Status });
        }

        [HttpPost]
        public async Task<IActionResult> MarkAllMessagesAsRead([FromBody] string senderId)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var sender = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == senderId);
            if (sender == null) return NotFound();

            var unreadMessages = await _dbContext.Messages
                .Where(m => m.SenderEmail == sender.Email && m.ReceiverEmail == currentUser.Id && m.Status != MessageStatus.Read)
                .ToListAsync();

            if (unreadMessages.Any())
            {
                foreach (var message in unreadMessages)
                {
                    message.Status = MessageStatus.Read;
                }

                await _dbContext.SaveChangesAsync();

                await hubContext.Clients.User(sender.Email).SendAsync(
                    "MessagesRead",
                    unreadMessages.Select(m => m.Id).ToList());
            }

            return Ok(new { success = true });
        }


        [HttpPost]
        public async Task<IActionResult> SendMessage([FromForm] string receiverId, [FromForm] string content)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            if (string.IsNullOrEmpty(receiverId))
            {
                return BadRequest("ReceiverId cannot be null or empty");
            }

            var receiver = await userManager.FindByIdAsync(receiverId);
            if (receiver == null)
            {
                return BadRequest("Receiver not found");
            }

            var message = new Message
            {
                Content = content,
                SenderEmail = currentUser.Email!,
                ReceiverEmail = receiverId,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Status = MessageStatus.Sent
            };

            _dbContext.Messages.Add(message);
            await _dbContext.SaveChangesAsync();

            await hubContext.Clients.User(receiverId).SendAsync("ReceiveMessage", currentUser.Email, content,
                message.Id, message.Timestamp
            );

            await hubContext.Clients.User(currentUser.Id).SendAsync("ReceiveMessage", currentUser.Email, content,
                message.Id, message.Timestamp
            );

            return Ok(new { 
                success = true, 
                messageId = message.Id,
                status = (int) message.Status,
                timestamp = message.Timestamp
            });
        }

        [HttpPost]
        public async Task<IActionResult> SendMessageWithFile([FromForm] string receiverId, [FromForm] string content, IFormFile file)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            if (string.IsNullOrEmpty(receiverId))
            {
                return BadRequest("ReceiverId cannot be null or empty");
            }

            var receiver = await userManager.FindByIdAsync(receiverId);
            if (receiver == null)
            {
                return BadRequest("Receiver not found");
            }

            // Kiểm tra file
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file was uploaded");
            }

            content = content ?? "";

            try
            {
                // Lưu file và tạo record FileAttachment
                var attachment = await fileService.SaveFileAsync(file, currentUser.Id);

                // Tạo tin nhắn với file đính kèm
                var message = new Message
                {
                    Content = content,
                    SenderEmail = currentUser.Email!,
                    ReceiverEmail = receiverId,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    HasAttachment = true,
                    Status = MessageStatus.Sent
                };

                // Thêm tin nhắn vào database
                _dbContext.Messages.Add(message);
                await _dbContext.SaveChangesAsync();

                // Liên kết file với tin nhắn
                attachment.MessageId = message.Id;
                _dbContext.FileAttachments.Update(attachment);
                await _dbContext.SaveChangesAsync();

                // Gửi thông báo qua SignalR
                var attachmentDto = new AttachmentDTO
                {
                    Id = attachment.Id,
                    FileName = attachment.FileName,
                    ContentType = attachment.ContentType,
                    FileSize = attachment.FileSize
                };

                await hubContext.Clients.User(receiverId).SendAsync("ReceiveMessageWithFile",
                    currentUser.Email, content, new List<AttachmentDTO> { attachmentDto },
                    message.Id, message.Timestamp
                );

                await hubContext.Clients.User(currentUser.Id).SendAsync("ReceiveMessageWithFile",
                    currentUser.Email, content, new List<AttachmentDTO> { attachmentDto },
                    message.Id, message.Timestamp
                );

                return Ok(new
                {
                    success = true,
                    messageId = message.Id,
                    status = (int)message.Status,
                    timestamp = message.Timestamp,
                    attachmentId = attachment.Id,
                    fileName = attachment.FileName
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> DownloadFile(string fileId)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var attachment = await _dbContext.FileAttachments.FindAsync(fileId);
            if (attachment == null)
            {
                return NotFound();
            }

            bool hasAccess = false;

            // Kiểm tra quyền truy cập file
            //if (attachment.MessageId.HasValue)
            //{
            //    // Trường hợp 1: Tin nhắn cá nhân
            //    var message = await _dbContext.Messages.FindAsync(attachment.MessageId.Value);
            //    if (message != null && (message.SenderEmail == currentUser.Email || message.ReceiverEmail == currentUser.Id))
            //    {
            //        hasAccess = true;
            //    }
            //    else if (message == null)
            //    {
            //        // Trường hợp 2: Tin nhắn nhóm
            //        var groupMessage = await _dbContext.GroupMessages
            //            .FirstOrDefaultAsync(gm => gm.Id == attachment.MessageId.Value);

            //        if (groupMessage != null)
            //        {
            //            // Kiểm tra xem người dùng hiện tại có phải thành viên của nhóm không
            //            var isGroupMember = await _dbContext.GroupMembers
            //                .AnyAsync(gm => gm.GroupId == groupMessage.GroupId && gm.UserId == currentUser.Id);

            //            if (isGroupMember)
            //            {
            //                hasAccess = true;
            //            }
            //        }
            //    }
            //}
            //else if (attachment.UploaderId == currentUser.Id)
            //{
            //    // Người tải lên file luôn có quyền truy cập
            //    hasAccess = true;
            //}

            hasAccess = true;

            if (!hasAccess)
            {
                return Forbid();
            }


            // Đọc và trả về file
            try
            {
                var fileStream = await fileService.GetFileStreamAsync(attachment.StoragePath);
                if (fileStream == null)
                {
                    return NotFound("File not found on storage");
                }

                return File(fileStream, attachment.ContentType, attachment.FileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetFileInfo(string fileId)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var attachment = await _dbContext.FileAttachments.FindAsync(fileId);
            if (attachment == null)
            {
                return NotFound();
            }

            return Json(new AttachmentDTO
            {
                Id = attachment.Id,
                FileName = attachment.FileName,
                ContentType = attachment.ContentType,
                FileSize = attachment.FileSize
            });
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteMessage(long messageId)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var message = await _dbContext.Messages
                .Include(m => m.Attachments)
                .FirstOrDefaultAsync(m => m.Id == messageId);

            if (message == null)
            {
                return NotFound();
            }

            // Chỉ cho phép người gửi tin nhắn xóa
            if (message.SenderEmail != currentUser.Email)
            {
                return Forbid();
            }

            // Xóa các file đính kèm
            if (message.HasAttachment && message.Attachments != null && message.Attachments.Any())
            {
                foreach (var attachment in message.Attachments)
                {
                    await fileService.DeleteFileAsync(attachment.StoragePath);
                    _dbContext.FileAttachments.Remove(attachment);
                }
            }

            // Xóa tin nhắn
            _dbContext.Messages.Remove(message);
            await _dbContext.SaveChangesAsync();

            // Thông báo cho người nhận qua SignalR về việc xóa tin nhắn
            await hubContext.Clients.User(message.ReceiverEmail).SendAsync("MessageDeleted", messageId);

            return Ok(new { success = true });
        }
    }
}
