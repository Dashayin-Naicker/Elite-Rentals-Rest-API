namespace EliteRentalsAPI.Models.DTOs
{
    public class MaintenanceCommentDto
    {
        public int SenderId { get; set; }
        public string Comment { get; set; } = string.Empty;
    }
}
