namespace EliteRentalsAPI.Models.DTOs
{
    // DTO for SSO
    public class SsoLoginDto
    {
        public string Provider { get; set; } = "";   // e.g. "Google"
        public string Token { get; set; } = "";      // Google ID token
        public string Email { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string? Role { get; set; }
    }
}
