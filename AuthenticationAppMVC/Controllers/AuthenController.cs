using AuthenticationAppMVC.Models;
using AuthenticationAppMVC.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AuthenticationAppMVC.Controllers
{
    public class AuthenController : Controller
    {
        private readonly SignInManager<User> _signInManager;
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> roleManager;

        public AuthenController(SignInManager<User> signInManager, UserManager<User> userManager, RoleManager<IdentityRole> roleManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            this.roleManager = roleManager;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginVM loginDTO)
        {
            if (!ModelState.IsValid || loginDTO == null)
            {
                ModelState.AddModelError("", "Login Infor is null");
                return View(loginDTO);
            }
            var user = await _userManager.FindByEmailAsync(loginDTO.Email);
            if (user != null && await _userManager.CheckPasswordAsync(user, loginDTO.Password))
            {
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Index", "Home");
            }
            ModelState.AddModelError("", "Invalid login error");
            return View(loginDTO);
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterVM registerDTO)
        {
            if (!ModelState.IsValid || registerDTO == null)
            {
                ModelState.AddModelError("", "Register Infor is not valid");
                return View(registerDTO);
            }

            User user = new User()
            {
                UserName = registerDTO.Email?.Split("@")[0],
                Email = registerDTO.Email,
                FullName = registerDTO.FullName,
            };
            var res = await _userManager.CreateAsync(user, registerDTO.Password);
            if (res.Succeeded)
            {
                string roleName = "USER";
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
                await _userManager.AddToRoleAsync(user, roleName);
                await _signInManager.SignInAsync(user, false);
                return RedirectToAction("Index", "Home");
            }

            // Truong hop khi dang ky khong thanh cong
            foreach (var error in res.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            return View(registerDTO);
        }

        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult Role()
        {
            return View();
        }

        [Authorize(Roles = "ADMIN")]
        [HttpPost]
        public async Task<IActionResult> Role(RoleViewModels roleViewModels)
        {
            if (ModelState.IsValid)
            {
                bool roleExists = await roleManager.RoleExistsAsync(roleViewModels?.roleName);
                if (roleExists)
                {
                    ModelState.AddModelError("", "Role Already Exists");
                }
                else
                {
                    IdentityRole identityRole = new IdentityRole
                    {
                        Name = roleViewModels?.roleName
                    };

                    IdentityResult result = await roleManager.CreateAsync(identityRole);
                    if (result.Succeeded)
                    {
                        return RedirectToAction("Index", "Home");
                    }
                    foreach (IdentityError error in result.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                }
            }
            return View(roleViewModels);
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpGet("api/user")]
        public async Task<IActionResult> GetUserInfo()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var email = user.Email ?? await _userManager.GetEmailAsync(user);
            return Ok(new { Email = email });
        }
    }
}
