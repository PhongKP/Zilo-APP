using AuthenticationAppMVC.Models;

namespace AuthenticationAppMVC.ViewModels
{
    public class CreateCallRequest
    {
        public string RecipientId { get; set; }
        public CallType Type { get; set; }
    }
}
