using Microsoft.AspNetCore.Identity;

namespace AuthenticationAppMVC.Models
{
    public class User:IdentityUser
    {
        public string? FullName { get; set; }
    }
}
