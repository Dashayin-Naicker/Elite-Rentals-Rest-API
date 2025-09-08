using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EliteRentalsAPI.Models
{
    public class Notification
    {
        [Key]
        public int NotificationId { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }
        public User? User { get; set; }

        public string Message { get; set; } = "";
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;
    }
}
