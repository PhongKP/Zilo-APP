using AuthenticationAppMVC.Hubs;
using AuthenticationAppMVC.Models;
using AuthenticationAppMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace AuthenticationAppMVC.Controllers
{
    [Authorize]
    public class FriendsController : Controller
    {
        private readonly IFriendsService _friendsService;
        private readonly UserManager<User> _userManager;
        private readonly IHubContext<FriendsHub> _hubContext;

        public FriendsController(IFriendsService friendsService, UserManager<User> userManager, IHubContext<FriendsHub> hubContext)
        {
            _friendsService = friendsService;
            _userManager = userManager;
            _hubContext = hubContext;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var currentUser = _userManager.GetUserAsync(User).Result;
            if (currentUser == null)
            {
                return NotFound();
            }

            var friends = _friendsService.GetFriendsAsync(currentUser.Id);
            var pendingFriendRequests = _friendsService.GetPendingFriendRequestsAsync(currentUser.Id);
            var sentFriendRequests = _friendsService.GetSentFriendRequestsAsync(currentUser.Id);

            ViewBag.Friends = friends;
            ViewBag.PendingFriendRequests = pendingFriendRequests;
            ViewBag.SentFriendRequests = sentFriendRequests;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SendFriendRequest(string receiverId)
        {
            var currentUser = _userManager.GetUserAsync(User).Result;
            if (currentUser == null)
            {
                return NotFound();
            }
            var result = await _friendsService.SendFriendRequestAsync(currentUser.Id, receiverId);
            if (result)
            {
                await _hubContext.Clients.User(receiverId).SendAsync("ReceiveFriendRequest", currentUser.Id);
            }
            return Json(new { success = result });
        }

        [HttpPost]
        public async Task<IActionResult> AcceptFriendRequest(string requestId)
        {
            var currentUser = _userManager.GetUserAsync(User).Result;
            if (currentUser == null)
            {
                return NotFound();
            }
            var result = await _friendsService.AcceptFriendRequestAsync(requestId, currentUser.Id);
            if (result)
            {
                var request = await _friendsService.GetFriendRequestByIdAsync(requestId);
                await _hubContext.Clients.User(request.SenderId).SendAsync("ReceiveFriendRequestAccepted", currentUser.Id);
            }


            return Json(new { success = result });
        }

        [HttpPost]
        public async Task<IActionResult> DeclineFriendRequest(string requestId)
        {
            var currentUser = _userManager.GetUserAsync(User).Result;
            if (currentUser == null)
            {
                return NotFound();
            }
            var result = await _friendsService.DeclineFriendRequestAsync(requestId, currentUser.Id);
            return Json(new { success = result });
        }

        [HttpPost]
        public async Task<IActionResult> CancelFriendRequest(string requestId)
        {
            var currentUser = _userManager.GetUserAsync(User).Result;
            if (currentUser == null)
            {
                return NotFound();
            }
            var result = await _friendsService.CancelFriendRequestAsync(requestId, currentUser.Id);
            return Json(new { success = result });
        }

        [HttpPost]
        public async Task<IActionResult> Unfriend(string friendId)
        {
            var currentUser = _userManager.GetUserAsync(User).Result;
            if (currentUser == null)
            {
                return NotFound();
            }
            var result = await _friendsService.RemoveFriendAsync(currentUser.Id, friendId);
            return Json(new { success = result });
        }

        [HttpGet]
        public async Task<IActionResult> SearchFriends(string keyword)
        {
            var currentUser = _userManager.GetUserAsync(User).Result;
            if (currentUser == null)
            {
                return NotFound();
            }
            var users = await _friendsService.SearchFriendsAsync(currentUser.Id, keyword);
            if (users == null)
            {
                return NotFound("Không tìm thấy bạn");
            }
            var result = new List<object>();
            foreach (var user in users)
            {
                var frStatus = await _friendsService.GetFriendshipStatusAsync(currentUser.Id, user.Id);
                result.Add(new
                {
                    id = user.Id,
                    name = user.FullName,
                    email = user.Email,
                    status = frStatus
                });
            }
            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetFriendshipStatus(string userId)
        {
            var currentUser = _userManager.GetUserAsync(User).Result;
            if (currentUser == null)
            {
                return NotFound();
            }
            var result = await _friendsService.GetFriendshipStatusAsync(currentUser.Id, userId);
            return Json(new { status = result });
        }

        [HttpGet]
        public async Task<IActionResult> GetFriends()
        {
            var currentUser = _userManager.GetUserAsync(User).Result;
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var friends = await _friendsService.GetFriendsAsync(currentUser.Id);
            return Json(friends);
        }

        [HttpGet]
        public async Task<IActionResult> GetPendingRequests()
        {
            var currentUser = _userManager.GetUserAsync(User).Result;
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var requests = await _friendsService.GetPendingFriendRequestsAsync(currentUser.Id);
            return Json(requests);
        }

        [HttpGet]
        public async Task<IActionResult> GetSentRequests()
        {
            var currentUser = _userManager.GetUserAsync(User).Result;
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var requests = await _friendsService.GetSentFriendRequestsAsync(currentUser.Id);
            return Json(requests);
        }
    }
}
