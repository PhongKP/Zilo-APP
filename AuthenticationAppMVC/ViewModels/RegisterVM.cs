using System.ComponentModel.DataAnnotations;

namespace AuthenticationAppMVC.ViewModels
{
    public class RegisterVM
    {
        [Required(ErrorMessage = "FullName cannot be empty")]
        [RegularExpression(@"^[a-zA-Z0-9\s]+$", ErrorMessage = "FullName can only contain letters and spaces.")]
        public string? FullName { get; set; }

        [Required(ErrorMessage = "Email cannot be empty")]
        [DataType(DataType.EmailAddress)]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Password cannot be empty")]
        [DataType(DataType.Password)]
        public string? Password { get; set; }

        [Required(ErrorMessage = "Confirmed Password cannot be empty")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Password not match")]
        public string? ConfirmPassword { get; set; }
    }
}
