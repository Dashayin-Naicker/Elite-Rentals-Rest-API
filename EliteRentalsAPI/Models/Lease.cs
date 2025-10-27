using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EliteRentalsAPI.Models
{
    public class Lease
    {
        [Key]
        public int LeaseId { get; set; }

        [ForeignKey("Property")]
        public int PropertyId { get; set; }
        public Property? Property { get; set; }

        [ForeignKey("Tenant")]
        public int TenantId { get; set; }
        public User? Tenant { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal Deposit { get; set; }
        public string Status { get; set; } = "Active";
        public byte[]? DocumentData { get; set; }
        public string? DocumentType { get; set; }

        public bool IsArchived { get; set; } = false;
        public DateTime? ArchivedDate { get; set; }
    }
}
