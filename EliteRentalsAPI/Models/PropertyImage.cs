using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EliteRentalsAPI.Models
{
    public class PropertyImage
    {
        [Key]
        public int PropertyImageId { get; set; }

        [ForeignKey("Property")]
        public int PropertyId { get; set; }

        public byte[] ImageData { get; set; } = Array.Empty<byte>();
        public string ImageType { get; set; } = "image/jpeg";

        public Property? Property { get; set; }
    }
}
