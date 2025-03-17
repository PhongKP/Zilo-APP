using AuthenticationAppMVC.Models;

namespace AuthenticationAppMVC.ViewModels
{
    public class CreateGroupCallRequest
    {
        public string GroupId { get; set; }
        public CallType  Type { get; set; }
    }
}
