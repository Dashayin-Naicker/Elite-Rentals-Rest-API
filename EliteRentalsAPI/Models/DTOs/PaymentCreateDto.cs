namespace EliteRentalsAPI.Models.DTOs
{
    public class PaymentCreateDto
    {
        public int TenantId { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
    }

}
