
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

        public ChatController(
            AppDBContext appDBContext,
            UserManager<User> userManager,
            IHubContext<Chathub> hubContext,
            FileService fileService)
        {
            _dbContext = appDBContext;
            this.userManager = userManager;
            this.hubContext = hubContext;
            this.fileService = fileService;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var users = await _dbContext.Users.Where(u => u.Id != currentUser.Id).ToListAsync();
            ViewData["HideFooter"] = true;
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
                    m.HasAttachment
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
                Attachments = m.HasAttachment && attachmentsByMessageId.ContainsKey(m.Id)
                ? attachmentsByMessageId[m.Id]
                    : new List<AttachmentDTO>()
            }).ToList();

            return Json(result);
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
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            _dbContext.Messages.Add(message);
            await _dbContext.SaveChangesAsync();

            await hubContext.Clients.User(receiverId).SendAsync("ReceiveMessage", currentUser.Email, content);
            await hubContext.Clients.User(currentUser.Id).SendAsync("ReceiveMessage", currentUser.Email, content);

            return Ok(new { success = true });
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
                    HasAttachment = true
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
                    currentUser.Email, content, new List<AttachmentDTO> { attachmentDto });

                await hubContext.Clients.User(currentUser.Id).SendAsync("ReceiveMessageWithFile",
                    currentUser.Email, content, new List<AttachmentDTO> { attachmentDto });

                return Ok(new { success = true });
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
