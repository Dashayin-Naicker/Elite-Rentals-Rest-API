using EliteRentalsAPI.Config;
using EliteRentalsAPI.Data;
using EliteRentalsAPI.Models;
using EliteRentalsAPI.Models.DTOs;
using EliteRentalsAPI.Services;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;

namespace EliteRentalsAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _ctx;
        private readonly TokenService _tokenService;
        private readonly IConfiguration _config;
        private readonly GoogleAuthConfig _googleAuth;

        public UsersController(AppDbContext ctx, TokenService tokenService, IConfiguration config, IOptions<GoogleAuthConfig> googleAuth)
        {
            _ctx = ctx;
            _tokenService = tokenService;
            _config = config;
            _googleAuth = googleAuth.Value;
        }

        // ✅ Register
        [HttpPost("signup")]
        public async Task<IActionResult> Signup([FromBody] RegisterDto dto)
        {
            if (await _ctx.Users.AnyAsync(x => x.Email == dto.Email))
                return Conflict(new { message = "Email already registered" });

            var user = new User
            {
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                Role = dto.Role,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
            };

            _ctx.Users.Add(user);
            await _ctx.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = user.UserId }, new
            {
                user.UserId,
                user.FirstName,
                user.LastName,
                user.Email,
                user.Role
            });
        }

        // ✅ Login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _ctx.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                return Unauthorized(new { message = "Invalid email or password" });

            var token = _tokenService.CreateToken(user);

            return Ok(new LoginResponseDto
            {
                Token = token,
                User = new UserDto
                {
                    UserId = user.UserId,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    Role = user.Role
                }
            });
        }

        // ✅ Google SSO
        [HttpPost("sso")]
        public async Task<IActionResult> SsoLogin([FromBody] SsoLoginDto dto)
        {
            if (dto.Provider != "Google" || string.IsNullOrEmpty(dto.Token))
                return Unauthorized(new { message = "Invalid SSO provider or token" });

            GoogleJsonWebSignature.Payload payload;
            try
            {
                payload = await GoogleJsonWebSignature.ValidateAsync(dto.Token,
                    new GoogleJsonWebSignature.ValidationSettings
                    {
                        Audience = new[] { _googleAuth.ClientId }
                    });
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Google token validation failed: " + ex.Message);
                return Unauthorized(new { message = "Invalid SSO token" });
            }

            // Find or create local user
            var user = await _ctx.Users.FirstOrDefaultAsync(u => u.Email == payload.Email);
            if (user == null)
            {
                user = new User
                {
                    Email = payload.Email,
                    FirstName = payload.GivenName ?? "",
                    LastName = payload.FamilyName ?? "",
                    Role = dto.Role ?? "Tenant"
                };
                _ctx.Users.Add(user);
                await _ctx.SaveChangesAsync();
            }

            // Issue JWT
            var token = _tokenService.CreateToken(user);
            return Ok(new
            {
                Token = token,
                User = new
                {
                    user.UserId,
                    user.FirstName,
                    user.LastName,
                    user.Email,
                    user.Role
                }
            });
        }

        // ✅ DTOs
        public class LoginDto
        {
            public string Email { get; set; } = "";
            public string Password { get; set; } = "";
        }

        [Authorize]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<User>> GetById(int id)
        {
            var u = await _ctx.Users.FindAsync(id);
            if (u == null) return NotFound();
            return u;
        }
    }
}
