using AuthenticationAppMVC.Models;
using AuthenticationAppMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationAppMVC.Controllers
{
    [Authorize]
    public class UsersController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly IFriendsService friendsService;

        public UsersController(UserManager<User> userManager, IFriendsService friendsService)
        {
            _userManager = userManager;
            this.friendsService = friendsService;
        }

        [HttpGet]
        public async Task<IActionResult> SearchUsers(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword) || keyword.Length < 2)
            {
                return Json(new List<object>());
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            // Tìm kiếm người dùng theo từ khóa
            var users = await _userManager.Users
                .Where(u => u.Id != currentUser.Id &&
                       (u.UserName.Contains(keyword) ||
                        u.Email.Contains(keyword) ||
                        (u.FullName != null && u.FullName.Contains(keyword))))
                .Take(20) // Giới hạn kết quả
                .ToListAsync();

            var result = new List<object>();
            foreach (var user in users)
            {
                var friendshipStatus = await friendsService.GetFriendshipStatusAsync(currentUser.Id, user.Id);
                result.Add(new
                {
                    id = user.Id,
                    name = user.FullName,
                    email = user.Email,
                    username = user.UserName,
                    status = friendshipStatus
                });
            }

            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetCurrentUser()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            return Json(new
            {
                id = currentUser.Id,
                name = currentUser.FullName,
                email = currentUser.Email,
                username = currentUser.UserName
            });
        }
    }
}
