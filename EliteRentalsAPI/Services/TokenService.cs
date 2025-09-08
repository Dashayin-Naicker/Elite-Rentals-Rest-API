using EliteRentalsAPI.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace EliteRentalsAPI.Services
{
    public class TokenService
    {
        private readonly IConfiguration _cfg;
        public TokenService(IConfiguration cfg) { _cfg = cfg; }

        public string CreateToken(User user)
        {
            var key = Encoding.UTF8.GetBytes(_cfg["Jwt:Key"]);
            var claims = new[] {
                new Claim("userId", user.UserId.ToString()),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("name", $"{user.FirstName} {user.LastName}")
            };

            var creds = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: _cfg["Jwt:Issuer"],
                audience: _cfg["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(1440),
                signingCredentials: creds
            );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
