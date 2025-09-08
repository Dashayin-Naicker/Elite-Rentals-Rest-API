using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EliteRentalsAPI.Models
{
    public class Invoice
    {
        [Key]
        public int InvoiceId { get; set; }

        [ForeignKey("Tenant")]
        public int TenantId { get; set; }
        public User? Tenant { get; set; }

        [ForeignKey("Lease")]
        public int LeaseId { get; set; }
        public Lease? Lease { get; set; }

        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = "Unpaid";
        public byte[]? PdfData { get; set; }
        public string? PdfType { get; set; }
    }
}
