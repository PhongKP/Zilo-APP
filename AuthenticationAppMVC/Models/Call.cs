using Microsoft.VisualBasic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace AuthenticationAppMVC.Models
{
    public class Call
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string CallerId { get; set; }

        // Người nhận cuộc gọi (null nếu là cuộc gọi nhóm)
        public string RecipientId { get; set; }

        // GroupId (null nếu là cuộc gọi 1-1)
        public string GroupId { get; set; }

        public CallType Type { get; set; }
        public CallStatus Status { get; set; }

        public long StartTime { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public long? EndTime { get; set; }

        [ForeignKey("CallerId")]
        public virtual User Caller { get; set; }

        [ForeignKey("RecipientId")]
        public virtual User Recipient { get; set; }

        [ForeignKey("GroupId")]
        public virtual Group Group { get; set; }

        // Các participant cho cuộc gọi nhóm
        public virtual ICollection<CallParticipant> Participants { get; set; } = new List<CallParticipant>();
    }
}
