using System.ComponentModel.DataAnnotations;

namespace EliteRentalsAPI.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Email { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string Role { get; set; } = ""; // Admin, Tenant, PropertyManager, Caretaker
        public string LanguagePreference { get; set; } = "en";
        public string NotificationPreference { get; set; } = "push";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

