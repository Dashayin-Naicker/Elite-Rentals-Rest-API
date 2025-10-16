using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EliteRentalsAPI.Models
{
    public class Message
    {
        [Key]
        public int MessageId { get; set; }

        [ForeignKey("Sender")]
        public int SenderId { get; set; }
        public User? Sender { get; set; }

        [ForeignKey("Receiver")]
        public int? ReceiverId { get; set; }
        public User? Receiver { get; set; }

        public string MessageText { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool IsChatbot { get; set; } = false;

        public bool IsBroadcast { get; set; } = false; 
        public string? TargetRole { get; set; }
    }
}
