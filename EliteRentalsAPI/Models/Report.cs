using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EliteRentalsAPI.Models
{
    public class Report
    {
        [Key]
        public int ReportId { get; set; }

        [ForeignKey("Manager")]
        public int ManagerId { get; set; }
        public User? Manager { get; set; }

        public string Type { get; set; } = "";
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public byte[]? ReportData { get; set; }
        public string? FileType { get; set; }
    }
}
