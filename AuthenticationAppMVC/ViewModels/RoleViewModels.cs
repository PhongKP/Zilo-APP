using System.ComponentModel.DataAnnotations;

namespace AuthenticationAppMVC.ViewModels
{
    public class RoleViewModels
    {
        [Required]
        [Display(Name = "Role Name")]
        public string? roleName { get; set; }
    }
}
