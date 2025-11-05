using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EliteRentalsAPI.Models
{
    public class Property
    {
        [Key]
        public int PropertyId { get; set; }

        [ForeignKey("Manager")]
        public int ManagerId { get; set; }
        public User? Manager { get; set; }   // Navigation

        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Address { get; set; } = "";
        public string City { get; set; } = "";
        public string Province { get; set; } = "";
        public string Country { get; set; } = "";
        public decimal RentAmount { get; set; }
        public int NumOfBedrooms { get; set; }
        public int NumOfBathrooms { get; set; }
        public string ParkingType { get; set; } = "";
        public int NumOfParkingSpots { get; set; }
        public bool PetFriendly { get; set; }
        public string Status { get; set; } = "Available";
        public DateTime ListingDate { get; set; } = DateTime.UtcNow;

        /*        public byte[]? ImageData { get; set; }
                public string? ImageType { get; set; }*/

        public ICollection<PropertyImage>? Images { get; set; }

        //Leases navigation
        public ICollection<Lease>? Leases { get; set; }
    }
}
