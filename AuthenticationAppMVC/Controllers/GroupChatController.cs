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
    public class GroupChatController : Controller
    {
        private readonly AppDBContext appDBContext;
        private readonly UserManager<User> userManager;
        private readonly IHubContext<Chathub> hubContext;
        private readonly FileService fileService;

        public GroupChatController(
            AppDBContext appDBContext, 
            UserManager<User> userManager, 
            IHubContext<Chathub> hubContext,
            FileService fileService
        )
        {
            this.appDBContext = appDBContext;
            this.userManager = userManager;
            this.hubContext = hubContext;
            this.fileService = fileService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateGroup([FromForm] string name, [FromForm] string description)
        {
            if (string.IsNullOrEmpty(name))
            {
                return BadRequest("Group Name Cannot Be Empty");
            }

            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var group = new Group
            {
                Name = name,
                Description = description ?? "",
                CreatorId = currentUser.Id
            };

            appDBContext.Groups.Add(group);
            await appDBContext.SaveChangesAsync();

            // Thêm người tạo làm admin của nhóm
            var membership = new GroupMember
            {
                GroupId = group.Id,
                UserId = currentUser.Id,
                IsAdmin = true
            };

            appDBContext.GroupMembers.Add(membership);
            await appDBContext.SaveChangesAsync();

            return Json(new { success = true, groupId = group.Id, name = group.Name });
        }

        [HttpGet]
        public async Task<IActionResult> GetUserGroups()
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var groups = await appDBContext.GroupMembers
                .Where(gm => gm.UserId == currentUser.Id)
                .Include(gm => gm.Group)
                .Select(gm => new
                {
                    gm.Group.Id,
                    gm.Group.Name,
                    MemberCount = appDBContext.GroupMembers.Count(m => m.GroupId == gm.GroupId)
                })
                .ToListAsync();
            return Json(groups);
        }

        [HttpGet]
        public async Task<IActionResult> GetGroupMessages(string groupId)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var isMember = await appDBContext.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == currentUser.Id);

            if (!isMember)
            {
                return Forbid();
            }

            // Lấy thông tin cơ bản của tin nhắn
            var messages = await appDBContext.GroupMessages
                .Where(m => m.GroupId == groupId)
                .OrderBy(m => m.Timestamp)
                .Include(m => m.Sender)
                .Select(m => new
                {
                    m.Id,
                    m.Content,
                    SenderEmail = m.Sender.Email,
                    SenderName = m.Sender.UserName,
                    m.Timestamp,
                    m.HasAttachment
                })
                .ToListAsync();

            // Nếu có tin nhắn chứa file đính kèm
            var messageIds = messages.Where(m => m.HasAttachment).Select(m => m.Id).ToList();

            if (messageIds.Any())
            {
                // Lấy thông tin file đính kèm
                var attachments = await appDBContext.FileAttachments
                    .Where(a => a.GroupMessageId != null && messageIds.Contains(a.GroupMessageId.Value))
                    .Select(a => new
                    {
                        MessageId = a.GroupMessageId,
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
                var attachmentsByMessageId = attachments
                    .GroupBy(a => a.MessageId)
                    .ToDictionary(g => g.Key, g => g.Select(a => a.AttachmentInfo).ToList());

                // Kết hợp tin nhắn với file đính kèm
                var result = messages.Select(m => new
                {
                    m.Id,
                    m.Content,
                    m.SenderEmail,
                    m.SenderName,
                    m.Timestamp,
                    m.HasAttachment,
                    Attachments = m.HasAttachment && attachmentsByMessageId.ContainsKey(m.Id)
                        ? attachmentsByMessageId[m.Id]
                        : new List<AttachmentDTO>()
                }).ToList();

                return Json(result);
            }
            else
            {
                // Nếu không có file đính kèm, trả về tin nhắn với danh sách đính kèm rỗng
                var result = messages.Select(m => new
                {
                    m.Id,
                    m.Content,
                    m.SenderEmail,
                    m.SenderName,
                    m.Timestamp,
                    m.HasAttachment,
                    Attachments = new List<AttachmentDTO>()
                }).ToList();

                return Json(result);
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage([FromForm] string groupId, [FromForm] string content)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var isMember = await appDBContext.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == currentUser.Id);

            if (!isMember)
            {
                return Forbid();
            }

            var message = new GroupMessage
            {
                GroupId = groupId,
                SenderId = currentUser.Id,
                Content = content,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Status = MessageStatus.Sent
            };

            appDBContext.GroupMessages.Add(message);
            await appDBContext.SaveChangesAsync();

            await hubContext.Clients.Group(groupId).SendAsync(
                "ReceiveGroupMessage",
                groupId,
                currentUser.Email,
                content,
                message.Timestamp);

            return Ok(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> SendMessageWithFile([FromForm] string groupId, [FromForm] string content, IFormFile file)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            content = content ?? "";

            var isMember = await appDBContext.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == currentUser.Id);

            if (!isMember)
            {
                return Forbid();
            }

            // Kiểm tra file
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file was uploaded");
            }

            try
            {
                // Lưu file và tạo record FileAttachment
                var attachment = await fileService.SaveFileAsync(file, currentUser.Id);

                // Tạo tin nhắn với file đính kèm
                var message = new GroupMessage
                {
                    GroupId = groupId,
                    SenderId = currentUser.Id,
                    Content = content,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    HasAttachment = true,
                    Status = MessageStatus.Sent
                };

                // Thêm tin nhắn vào database
                appDBContext.GroupMessages.Add(message);
                await appDBContext.SaveChangesAsync();

                // Liên kết file với tin nhắn nhóm
                attachment.GroupMessageId = message.Id;
                appDBContext.FileAttachments.Update(attachment);
                await appDBContext.SaveChangesAsync();

                // Tạo đối tượng attachmentDto để gửi qua SignalR
                var attachmentDto = new AttachmentDTO
                {
                    Id = attachment.Id,
                    FileName = attachment.FileName,
                    ContentType = attachment.ContentType,
                    FileSize = attachment.FileSize
                };

                // Gửi tin nhắn đến tất cả thành viên trong nhóm qua SignalR
                await hubContext.Clients.Group(groupId).SendAsync(
                    "ReceiveGroupMessageWithFile",
                    groupId,
                    currentUser.Email,
                    currentUser.UserName,
                    content,
                    message.Timestamp,
                    new List<AttachmentDTO> { attachmentDto });

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendMessageWithMultipleFiles([FromForm] string groupId, [FromForm] string content, [FromForm] List<IFormFile> files)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var isMember = await appDBContext.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == currentUser.Id);

            if (!isMember)
            {
                return Forbid();
            }

            content = content ?? "";

            // Kiểm tra files
            if (files == null || !files.Any() || files.All(f => f.Length == 0))
            {
                return BadRequest("No files were uploaded");
            }

            try
            {
                // Tạo tin nhắn với file đính kèm
                var message = new GroupMessage
                {
                    GroupId = groupId,
                    SenderId = currentUser.Id,
                    Content = content,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    HasAttachment = true
                };

                // Thêm tin nhắn vào database
                appDBContext.GroupMessages.Add(message);
                await appDBContext.SaveChangesAsync();

                var attachmentDtos = new List<AttachmentDTO>();

                // Lưu từng file và liên kết với tin nhắn
                foreach (var file in files)
                {
                    if (file.Length > 0)
                    {
                        // Lưu file và tạo record FileAttachment
                        var attachment = await fileService.SaveFileAsync(file, currentUser.Id);

                        // Liên kết file với tin nhắn nhóm
                        attachment.GroupMessageId = message.Id;
                        appDBContext.FileAttachments.Update(attachment);

                        // Thêm vào danh sách DTO để gửi qua SignalR
                        attachmentDtos.Add(new AttachmentDTO
                        {
                            Id = attachment.Id,
                            FileName = attachment.FileName,
                            ContentType = attachment.ContentType,
                            FileSize = attachment.FileSize
                        });
                    }
                }

                // Lưu các thay đổi về attachments
                await appDBContext.SaveChangesAsync();

                // Gửi tin nhắn đến tất cả thành viên trong nhóm qua SignalR
                await hubContext.Clients.Group(groupId).SendAsync(
                    "ReceiveGroupMessageWithFile",
                    groupId,
                    currentUser.Email,
                    currentUser.UserName,
                    content,
                    message.Timestamp,
                    attachmentDtos);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetMembers(string groupId)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var isMember = await appDBContext.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == currentUser.Id);

            if (!isMember)
            {
                return Forbid();
            }

            var members = await appDBContext.GroupMembers
                .Where(gm => gm.GroupId == groupId)
                .Include(gm => gm.User)
                .Select(gm => new
                {
                    gm.User.Id,
                    gm.User.UserName,
                    gm.User.Email,
                    gm.IsAdmin
                })
                .ToListAsync();

            return Json(members);
        }

        [HttpGet]
        public async Task<IActionResult> GetNonMembers(string groupId)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var isAdmin = await appDBContext.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == currentUser.Id && gm.IsAdmin);

            if (!isAdmin)
            {
                return Forbid();
            }

            // Lấy danh sách ID những người đã là thành viên
            var memberIds = await appDBContext.GroupMembers
                .Where(gm => gm.GroupId == groupId)
                .Select(gm => gm.UserId)
                .ToListAsync();

            // Lấy danh sách người không phải là thành viên
            var nonMembers = await appDBContext.Users
                .Where(u => !memberIds.Contains(u.Id))
                .Select(u => new
                {
                    u.Id,
                    u.UserName,
                    u.Email
                })
                .ToListAsync();

            return Json(nonMembers);
        }

        [HttpPost]
        public async Task<IActionResult> AddMember([FromForm] string groupId, [FromForm] string userId)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var isAdmin = await appDBContext.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == currentUser.Id && gm.IsAdmin);

            if (!isAdmin)
            {
                return Forbid();
            }

            var alreadyMember = await appDBContext.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId);

            if (alreadyMember)
            {
                return BadRequest("Member have already been member of group");
            }

            var userToAdd = await userManager.FindByIdAsync(userId);
            if (userToAdd == null)
            {
                return NotFound("User not found");
            }

            var membership = new GroupMember
            {
                GroupId = groupId,
                UserId = userId,
                IsAdmin = false
            };

            appDBContext.GroupMembers.Add(membership);
            await appDBContext.SaveChangesAsync();

            return Json(new { success = true, userId = userId, userName = userToAdd.UserName, email = userToAdd.Email });
        }

        [HttpPost]
        public async Task<IActionResult> LeaveGroup([FromForm] string groupId)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var membership = await appDBContext.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == currentUser.Id);

            if (membership == null)
            {
                return NotFound();
            }

            appDBContext.GroupMembers.Remove(membership);
            await appDBContext.SaveChangesAsync();

            // Nếu không còn thành viên nào, xóa nhóm
            var remainingMembers = await appDBContext.GroupMembers
                .CountAsync(gm => gm.GroupId == groupId);

            if (remainingMembers == 0)
            {
                var group = await appDBContext.Groups.FindAsync(groupId);
                if (group != null)
                {
                    appDBContext.Groups.Remove(group);
                    await appDBContext.SaveChangesAsync();
                }
            }
            // Nếu là admin và vẫn có thành viên khác, chọn một người khác làm admin
            else if (membership.IsAdmin)
            {
                var newAdmin = await appDBContext.GroupMembers
                    .FirstOrDefaultAsync(gm => gm.GroupId == groupId);

                if (newAdmin != null)
                {
                    newAdmin.IsAdmin = true;
                    await appDBContext.SaveChangesAsync();
                }
            }

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsDelivered([FromForm] string messageId)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var message = await appDBContext.GroupMessages.FindAsync(messageId);
            if (message == null) return NotFound("Message not found");

            var isMember = await appDBContext.GroupMembers
                .AnyAsync(gm => gm.GroupId == message.GroupId && gm.UserId == currentUser.Id);

            if (!isMember)
            {
                return Forbid();
            }

            message.Status = MessageStatus.Delivered;
            appDBContext.GroupMessages.Update(message);
            await appDBContext.SaveChangesAsync();

            await hubContext.Clients.Group(message.GroupId).SendAsync(
                "MessageDelivered",
                message.Id);

            return Ok(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsRead([FromForm] string messageId)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var message = await appDBContext.GroupMessages.FindAsync(messageId);
            if (message == null) return NotFound("Message not found");

            var isMember = await appDBContext.GroupMembers
                .AnyAsync(gm => gm.GroupId == message.GroupId && gm.UserId == currentUser.Id);

            if (!isMember)
            {
                return Forbid();
            }

            message.Status = MessageStatus.Read;
            appDBContext.GroupMessages.Update(message);
            await appDBContext.SaveChangesAsync();

            await hubContext.Clients.Group(message.GroupId).SendAsync(
                "MessageRead",
                message.Id);

            return Ok(new { success = true });
        }
    }
}
