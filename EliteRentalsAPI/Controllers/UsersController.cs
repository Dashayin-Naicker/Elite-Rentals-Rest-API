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

        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _ctx.Users
                .Select(u => new UserDto
                {
                    UserId = u.UserId,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email,
                    Role = u.Role,
                    IsActive = u.IsActive,
                    TenantApproval = u.TenantApproval
                })
                .ToListAsync();

            return Ok(users);
        }


        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] User update)
        {
            var user = await _ctx.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.FirstName = update.FirstName;
            user.LastName = update.LastName;
            user.Email = update.Email;
            user.Role = update.Role;
            user.TenantApproval = update.TenantApproval;

            await _ctx.SaveChangesAsync();
            return Ok(user);
        }

        [Authorize]
        [HttpPatch("{id:int}/password")]
        public async Task<IActionResult> ChangePassword(int id, [FromBody] ChangePasswordDto dto)
        {
            var user = await _ctx.Users.FindAsync(id);
            if (user == null) return NotFound();

            if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
                return BadRequest(new { message = "Current password is incorrect" });

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            await _ctx.SaveChangesAsync();

            return Ok(new { message = "Password updated successfully" });
        }



        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpPatch("{id:int}/status")]
        public async Task<IActionResult> ChangeStatus(int id)
        {
            var user = await _ctx.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.IsActive = !user.IsActive;
            await _ctx.SaveChangesAsync();

            return Ok(new { message = $"User {(user.IsActive ? "enabled" : "disabled")}" });
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

        public class FcmTokenDto
        {
            public string Token { get; set; } = "";
        }

        [Authorize]
        [HttpPatch("{id:int}/fcmtoken")]
        public async Task<IActionResult> UpdateFcmToken(int id, [FromBody] FcmTokenDto dto)
        {
            var user = await _ctx.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.FcmToken = dto.Token;
            await _ctx.SaveChangesAsync();

            return Ok(new { message = "FCM token updated" });
        }


    }
}
