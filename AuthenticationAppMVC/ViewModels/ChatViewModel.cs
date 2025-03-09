using AuthenticationAppMVC.Models;

namespace AuthenticationAppMVC.ViewModels
{
    public class ChatViewModel
    {
        public string ContactId { get; set; }
        public List<Message> Messages { get; set; } = new List<Message>();
        public bool IsCloudStorage { get; set; } = false;
    }
}
