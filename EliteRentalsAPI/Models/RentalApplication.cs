using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EliteRentalsAPI.Models
{
    public class RentalApplication
    {
        [Key]
        public int ApplicationId { get; set; }

        [ForeignKey("Property")]
        public int PropertyId { get; set; }
        public Property? Property { get; set; }

        public string ApplicantName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Status { get; set; } = "Pending";
        public byte[]? DocumentData { get; set; }
        public string? DocumentType { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
