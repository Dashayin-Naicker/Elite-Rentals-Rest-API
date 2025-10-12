using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EliteRentalsAPI.Models
{
    public class Maintenance
    {
        [Key]
        public int MaintenanceId { get; set; }

        [ForeignKey("Tenant")]
        public int TenantId { get; set; }
        public User? Tenant { get; set; }

        [ForeignKey("Property")]
        public int PropertyId { get; set; }
        public Property? Property { get; set; }

        [ForeignKey("Caretaker")]
        public int? AssignedCaretakerId { get; set; }
        public User? Caretaker { get; set; }

        public string Description { get; set; } = "";
        public string Category { get; set; } = "";
        public string Urgency { get; set; } = "Low";
        public string Status { get; set; } = "Pending";
        public byte[]? ProofData { get; set; }
        public string? ProofType { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
