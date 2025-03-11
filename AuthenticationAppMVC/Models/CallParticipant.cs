using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace AuthenticationAppMVC.Models
{
    public class CallParticipant
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string CallId { get; set; }

        [Required]
        public string UserId { get; set; }

        public bool HasJoined { get; set; }
        public long? JoinTime { get; set; }
        public long? LeaveTime { get; set; }

        [ForeignKey("CallId")]
        public virtual Call Call { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }
    }
}
