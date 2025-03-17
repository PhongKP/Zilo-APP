using AuthenticationAppMVC.Models;

namespace AuthenticationAppMVC.ViewModels
{
    public class CallViewModel
    {
        public string CallId { get; set; }

        // ID của người dùng hiện tại
        public string CurrentUserId { get; set; }

        // Thông tin người nhận (cho cuộc gọi 1-1)
        public string RecipientId { get; set; }
        public string RecipientName { get; set; }
        public string RecipientAvatar { get; set; }

        // Thông tin nhóm (cho cuộc gọi nhóm)
        public string GroupId { get; set; }
        public string GroupName { get; set; }
        public string GroupAvatar { get; set; }

        // Loại cuộc gọi
        public CallType CallType { get; set; }

        // Flag để xác định loại cuộc gọi
        public bool IsGroup { get; set; }

        // Helper properties
        public bool IsVideoCall => CallType == CallType.Video;
        public bool IsAudioCall => CallType == CallType.Audio;

        // Tên hiển thị (người nhận hoặc tên nhóm)
        public string DisplayName => IsGroup ? GroupName : RecipientName;

        // Avatar hiển thị
        public string DisplayAvatar => IsGroup ? GroupAvatar : RecipientAvatar;
    }
}
