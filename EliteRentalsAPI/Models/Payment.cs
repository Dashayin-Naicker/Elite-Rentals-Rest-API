using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EliteRentalsAPI.Models
{
    public class Payment
    {
        [Key]
        public int PaymentId { get; set; }

        [ForeignKey("Tenant")]
        public int TenantId { get; set; }
        public User? Tenant { get; set; }

        public decimal Amount { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Pending";
        public byte[]? ProofData { get; set; }
        public string? ProofType { get; set; }
    }
}
