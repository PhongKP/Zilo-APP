using AuthenticationAppMVC.Hubs;
using AuthenticationAppMVC.Models;
using AuthenticationAppMVC.Services;
using AuthenticationAppMVC.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace AuthenticationAppMVC.Controllers
{
    [Authorize]
    public class CallController : Controller
    {
        private readonly ICallService _callService;
        private readonly IHubContext<CallHub> _callHubContext;

        public CallController(ICallService callService, IHubContext<CallHub> callHubContext)
        {
            _callService = callService;
            _callHubContext = callHubContext;
        }

        // Lấy ID của người dùng hiện tại
        private string GetCurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        // Action để xem trang cuộc gọi cá nhân
        [HttpGet("Call/User/{userId}")]
        public async Task<IActionResult> CallUser(string userId, int type = 0)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest("Thiếu ID người dùng");
            }

            var callType = (CallType)type;
            var currentUserId = GetCurrentUserId();

            // Kiểm tra xem người dùng có đang trong cuộc gọi khác không
            if (await _callService.IsUserInCallAsync(currentUserId))
            {
                var activeCall = await _callService.GetUserActiveCallAsync(currentUserId);
                if (activeCall != null)
                {
                    // Nếu có cuộc gọi đang diễn ra, điều hướng đến cuộc gọi đó
                    return RedirectToAction("Join", new { callId = activeCall.Id });
                }
            }

            // Lấy thông tin người dùng từ DB hoặc service nào đó
            // (Giả sử đây là đoạn code tạm thời, bạn sẽ cần thay thế với cách lấy thông tin user thực tế từ database)
            var recipient = await GetUserInfoAsync(userId);
            if (recipient == null)
            {
                return NotFound("Không tìm thấy người dùng");
            }

            // Tạo ViewModel cho trang cuộc gọi
            var viewModel = new CallViewModel
            {
                RecipientId = userId,
                RecipientName = recipient.UserName,
                RecipientAvatar = recipient.ProfilePicture ?? "/img/user_ava.svg",
                IsGroup = false,
                CallType = callType,
                CurrentUserId = currentUserId
            };

            return View("Call", viewModel);
        }

        // Action để xem trang cuộc gọi nhóm
        [HttpGet("Call/Group/{groupId}")]
        public async Task<IActionResult> CallGroup(string groupId, int type = 0)
        {
            if (string.IsNullOrEmpty(groupId))
            {
                return BadRequest("Thiếu ID nhóm");
            }

            var callType = (CallType)type;
            var currentUserId = GetCurrentUserId();

            // Kiểm tra xem người dùng có đang trong cuộc gọi khác không
            if (await _callService.IsUserInCallAsync(currentUserId))
            {
                var activeCall = await _callService.GetUserActiveCallAsync(currentUserId);
                if (activeCall != null)
                {
                    // Nếu có cuộc gọi đang diễn ra, điều hướng đến cuộc gọi đó
                    return RedirectToAction("Join", new { callId = activeCall.Id });
                }
            }

            // Lấy thông tin nhóm từ DB hoặc service
            // (Giả sử đây là đoạn code tạm thời, bạn sẽ cần thay thế với cách lấy thông tin nhóm thực tế từ database)
            var group = await GetGroupInfoAsync(groupId);
            if (group == null)
            {
                return NotFound("Không tìm thấy nhóm");
            }

            // Tạo ViewModel cho trang cuộc gọi
            var viewModel = new CallViewModel
            {
                GroupId = groupId,
                GroupName = group.Name,
                GroupAvatar = group.Avatar ?? "/img/user_ava.svg",
                IsGroup = true,
                CallType = callType,
                CurrentUserId = currentUserId
            };

            return View("Call", viewModel);
        }

        // Action để tham gia vào một cuộc gọi đã tạo
        [HttpGet("Call/Join/{callId}")]
        public async Task<IActionResult> Join(string callId)
        {
            if (string.IsNullOrEmpty(callId))
            {
                return BadRequest("Thiếu ID cuộc gọi");
            }

            var currentUserId = GetCurrentUserId();
            var call = await _callService.GetCallByIdAsync(callId);

            if (call == null)
            {
                return NotFound("Không tìm thấy cuộc gọi");
            }

            // Kiểm tra xem cuộc gọi còn hiệu lực không
            if (call.Status == CallStatus.Ended || call.Status == CallStatus.Missed || call.Status == CallStatus.Declined)
            {
                return BadRequest("Cuộc gọi đã kết thúc");
            }

            // Kiểm tra xem người dùng có quyền tham gia cuộc gọi không
            bool canJoin = call.CallerId == currentUserId || call.RecipientId == currentUserId ||
                          (call.GroupId != null && await _callService.IsUserParticipantAsync(callId, currentUserId));

            if (!canJoin)
            {
                return Forbid("Bạn không có quyền tham gia cuộc gọi này");
            }

            // Tự động tham gia vào cuộc gọi khi vào trang
            await _callService.JoinCallAsync(callId, currentUserId);

            // Tạo ViewModel dựa trên loại cuộc gọi
            CallViewModel viewModel;

            if (!string.IsNullOrEmpty(call.GroupId))
            {
                // Đây là cuộc gọi nhóm
                var group = await GetGroupInfoAsync(call.GroupId);

                viewModel = new CallViewModel
                {
                    CallId = callId,
                    GroupId = call.GroupId,
                    GroupName = group.Name,
                    GroupAvatar = group.Avatar ?? "/img/user_ava.svg",
                    IsGroup = true,
                    CallType = call.Type,
                    CurrentUserId = currentUserId
                };
            }
            else
            {
                // Xác định người nhận (người còn lại trong cuộc gọi)
                string otherUserId = call.CallerId == currentUserId ? call.RecipientId : call.CallerId;

                // Lấy thông tin người nhận
                var otherUser = await GetUserInfoAsync(otherUserId);

                viewModel = new CallViewModel
                {
                    CallId = callId,
                    RecipientId = otherUserId,
                    RecipientName = otherUser.UserName,
                    RecipientAvatar = otherUser.ProfilePicture ?? "/images/default-avatar.png",
                    IsGroup = false,
                    CallType = call.Type,
                    CurrentUserId = currentUserId
                };
            }

            // Báo cho mọi người biết người dùng này đã tham gia cuộc gọi
            await _callHubContext.Clients.Group(callId).SendAsync("UserJoinedCall", callId, currentUserId);

            return View("Call", viewModel);
        }

        // Helper method để lấy thông tin người dùng (tạm thời)
        private async Task<dynamic> GetUserInfoAsync(string userId)
        {
            // Trong thực tế, bạn sẽ lấy thông tin từ database
            // Đây chỉ là code tạm để demo
            await Task.Delay(1); // Giả lập một async operation

            return new
            {
                UserName = "User " + userId.Substring(0, 4), // Giả lập tên người dùng
                ProfilePicture = "/images/default-avatar.png" // Giả lập ảnh người dùng
            };
        }

        // Helper method để lấy thông tin nhóm (tạm thời)
        private async Task<dynamic> GetGroupInfoAsync(string groupId)
        {
            // Trong thực tế, bạn sẽ lấy thông tin từ database
            // Đây chỉ là code tạm để demo
            await Task.Delay(1); // Giả lập một async operation

            return new
            {
                Name = "Nhóm " + groupId.Substring(0, 4), // Giả lập tên nhóm
                Avatar = "/images/default-group.png" // Giả lập ảnh nhóm
            };
        }

        // API để tạo cuộc gọi 1-1
        [HttpPost("create")]
        public async Task<IActionResult> CreateCall([FromBody] CreateCallRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                string callerId = GetCurrentUserId();

                // Kiểm tra xem người dùng có đang trong cuộc gọi khác không
                if (await _callService.IsUserInCallAsync(callerId))
                    return BadRequest(new { message = "Bạn đang trong một cuộc gọi khác" });

                // Tạo cuộc gọi mới
                var call = await _callService.CreateCallAsync(callerId, request.RecipientId, request.Type);

                return Ok(new
                {
                    callId = call.Id,
                    message = "Cuộc gọi đã được tạo thành công"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi tạo cuộc gọi", error = ex.Message });
            }
        }

        // API để tạo cuộc gọi nhóm
        [HttpPost("create-group")]
        public async Task<IActionResult> CreateGroupCall([FromBody] CreateGroupCallRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                string callerId = GetCurrentUserId();

                // Kiểm tra xem người dùng có đang trong cuộc gọi khác không
                if (await _callService.IsUserInCallAsync(callerId))
                    return BadRequest(new { message = "Bạn đang trong một cuộc gọi khác" });

                // Tạo cuộc gọi nhóm mới
                var call = await _callService.CreateGroupCallAsync(callerId, request.GroupId, request.Type);

                return Ok(new
                {
                    callId = call.Id,
                    message = "Cuộc gọi nhóm đã được tạo thành công"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi tạo cuộc gọi nhóm", error = ex.Message });
            }
        }

        // API để cập nhật trạng thái cuộc gọi
        [HttpPut("{callId}/status")]
        public async Task<IActionResult> UpdateCallStatus(string callId, [FromBody] UpdateCallStatusRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                // Kiểm tra xem cuộc gọi có tồn tại không
                var call = await _callService.GetCallByIdAsync(callId);
                if (call == null)
                    return NotFound(new { message = "Không tìm thấy cuộc gọi" });

                // Kiểm tra quyền cập nhật trạng thái cuộc gọi
                string userId = GetCurrentUserId();
                if (call.CallerId != userId && call.RecipientId != userId &&
                    !await _callService.IsUserParticipantAsync(callId, userId))
                {
                    return Forbid();
                }

                // Cập nhật trạng thái
                await _callService.UpdateCallStatusAsync(callId, request.Status);

                return Ok(new { message = "Trạng thái cuộc gọi đã được cập nhật" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi cập nhật trạng thái cuộc gọi", error = ex.Message });
            }
        }

        // API để kết thúc cuộc gọi
        [HttpPut("{callId}/end")]
        public async Task<IActionResult> EndCall(string callId)
        {
            try
            {
                // Kiểm tra xem cuộc gọi có tồn tại không
                var call = await _callService.GetCallByIdAsync(callId);
                if (call == null)
                    return NotFound(new { message = "Không tìm thấy cuộc gọi" });

                // Kiểm tra quyền kết thúc cuộc gọi (mọi người tham gia đều có thể kết thúc)
                string userId = GetCurrentUserId();
                if (call.CallerId != userId && call.RecipientId != userId &&
                    !await _callService.IsUserParticipantAsync(callId, userId))
                {
                    return Forbid();
                }

                // Kết thúc cuộc gọi
                await _callService.EndCallAsync(callId);

                // Báo cho tất cả người tham gia biết cuộc gọi đã kết thúc thông qua SignalR
                var participants = await _callService.GetCallParticipantsAsync(callId);
                foreach (var participant in participants)
                {
                    await _callHubContext.Clients.User(participant.UserId).SendAsync("CallEnded", callId, userId);
                }

                return Ok(new { message = "Cuộc gọi đã kết thúc" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi kết thúc cuộc gọi", error = ex.Message });
            }
        }

        // API để tham gia vào cuộc gọi
        [HttpPost("{callId}/join")]
        public async Task<IActionResult> JoinCall(string callId)
        {
            try
            {
                string userId = GetCurrentUserId();

                // Kiểm tra xem cuộc gọi có tồn tại không
                var call = await _callService.GetCallByIdAsync(callId);
                if (call == null)
                    return NotFound(new { message = "Không tìm thấy cuộc gọi" });

                // Kiểm tra xem người dùng có được phép tham gia không
                bool canJoin = call.CallerId == userId || call.RecipientId == userId ||
                               (call.GroupId != null && await _callService.IsUserParticipantAsync(callId, userId));

                if (!canJoin)
                    return Forbid();

                // Tham gia vào cuộc gọi
                await _callService.JoinCallAsync(callId, userId);

                return Ok(new
                {
                    message = "Đã tham gia vào cuộc gọi",
                    callDetails = call
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi tham gia cuộc gọi", error = ex.Message });
            }
        }

        // API để rời khỏi cuộc gọi
        [HttpPost("{callId}/leave")]
        public async Task<IActionResult> LeaveCall(string callId)
        {
            try
            {
                string userId = GetCurrentUserId();

                // Kiểm tra xem người dùng có đang trong cuộc gọi không
                if (!await _callService.IsUserParticipantAsync(callId, userId))
                    return BadRequest(new { message = "Bạn không tham gia cuộc gọi này" });

                // Rời cuộc gọi
                await _callService.LeaveCallAsync(callId, userId);

                return Ok(new { message = "Đã rời khỏi cuộc gọi" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi rời khỏi cuộc gọi", error = ex.Message });
            }
        }

        // API để từ chối cuộc gọi
        [HttpPost("{callId}/decline")]
        public async Task<IActionResult> DeclineCall(string callId)
        {
            try
            {
                string userId = GetCurrentUserId();

                // Kiểm tra xem cuộc gọi có tồn tại không
                var call = await _callService.GetCallByIdAsync(callId);
                if (call == null)
                    return NotFound(new { message = "Không tìm thấy cuộc gọi" });

                // Kiểm tra xem người dùng có phải là người được gọi không
                if (call.RecipientId != userId && !await _callService.IsUserParticipantAsync(callId, userId))
                    return Forbid();

                // Cập nhật trạng thái thành Declined
                await _callService.UpdateCallStatusAsync(callId, CallStatus.Declined);

                // Thông báo cho người gọi qua SignalR
                await _callHubContext.Clients.User(call.CallerId).SendAsync("CallDeclined", callId, userId);

                return Ok(new { message = "Đã từ chối cuộc gọi" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi từ chối cuộc gọi", error = ex.Message });
            }
        }

        // API để lấy lịch sử cuộc gọi
        [HttpGet("history")]
        public async Task<IActionResult> GetCallHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                string userId = GetCurrentUserId();
                var callHistory = await _callService.GetUserCallHistoryAsync(userId, page, pageSize);

                return Ok(callHistory);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy lịch sử cuộc gọi", error = ex.Message });
            }
        }

        // API để lấy cuộc gọi đang diễn ra của người dùng
        [HttpGet("active")]
        public async Task<IActionResult> GetActiveCall()
        {
            try
            {
                string userId = GetCurrentUserId();
                var activeCall = await _callService.GetUserActiveCallAsync(userId);

                if (activeCall == null)
                    return Ok(new { active = false });

                return Ok(new
                {
                    active = true,
                    callDetails = activeCall
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy thông tin cuộc gọi đang diễn ra", error = ex.Message });
            }
        }

        // API để lấy thông tin chi tiết cuộc gọi
        [HttpGet("details/{callId}")]
        public async Task<IActionResult> GetCallDetails(string callId)
        {
            try
            {
                var call = await _callService.GetCallByIdAsync(callId);

                if (call == null)
                    return NotFound(new { message = "Không tìm thấy cuộc gọi" });

                // Kiểm tra quyền truy cập thông tin cuộc gọi
                string userId = GetCurrentUserId();
                if (call.CallerId != userId && call.RecipientId != userId &&
                    !await _callService.IsUserParticipantAsync(callId, userId))
                {
                    return Forbid();
                }

                // Lấy danh sách tất cả người tham gia
                var participants = await _callService.GetCallParticipantsAsync(callId);

                return Ok(new
                {
                    call,
                    participants
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy thông tin chi tiết cuộc gọi", error = ex.Message });
            }
        }
    }
}
