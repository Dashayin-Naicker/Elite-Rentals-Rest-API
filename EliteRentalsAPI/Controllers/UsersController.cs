using EliteRentalsAPI.Data;
using EliteRentalsAPI.Models;
using EliteRentalsAPI.Models.DTOs;
using EliteRentalsAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Crypto.Generators;

namespace EliteRentalsAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _ctx;
        private readonly TokenService _tokenService;

        public UsersController(AppDbContext ctx, TokenService tokenService)
        {
            _ctx = ctx;
            _tokenService = tokenService;
        }

        // ✅ Register new user
        [HttpPost("signup")]
        public async Task<IActionResult> Signup([FromBody] User user)
        {
            if (await _ctx.Users.AnyAsync(x => x.Email == user.Email))
                return Conflict(new { message = "Email already registered" });

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);
            _ctx.Users.Add(user);
            await _ctx.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = user.UserId }, user);
        }

        // ✅ Login with email + password → return JWT
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _ctx.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                return Unauthorized(new { message = "Invalid email or password" });

            var token = _tokenService.CreateToken(user);

            var response = new LoginResponseDto
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
            };

            return Ok(response);
        }

        // ✅ Get all users (Admins only)
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetAll() =>
            await _ctx.Users.ToListAsync();

        // ✅ Get specific user
        [Authorize]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<User>> GetById(int id)
        {
            var u = await _ctx.Users.FindAsync(id);
            if (u == null) return NotFound();
            return u;
        }

        // ✅ Update user (self or admin)
        [Authorize]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] User updated)
        {
            var existing = await _ctx.Users.FindAsync(id);
            if (existing == null) return NotFound();

            existing.FirstName = updated.FirstName;
            existing.LastName = updated.LastName;
            existing.Email = updated.Email;
            existing.Role = updated.Role;
            if (!string.IsNullOrEmpty(updated.PasswordHash))
            {
                existing.PasswordHash = BCrypt.Net.BCrypt.HashPassword(updated.PasswordHash);
            }

            await _ctx.SaveChangesAsync();
            return NoContent();
        }

        // ✅ Delete user
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var u = await _ctx.Users.FindAsync(id);
            if (u == null) return NotFound();

            _ctx.Users.Remove(u);
            await _ctx.SaveChangesAsync();
            return NoContent();
        }

        // DTO
        public class LoginDto
        {
            public string Email { get; set; } = "";
            public string Password { get; set; } = "";
        }

        // Inside UsersController.cs
        [HttpPost("sso")]
        public async Task<IActionResult> SsoLogin([FromBody] SsoLoginDto dto)
        {
            // 1. Verify external token (stubbed for now)
            bool valid = await VerifyExternalToken(dto.Provider, dto.Token);
            if (!valid) return Unauthorized(new { message = "Invalid SSO token" });

            // 2. Find or create local user
            var user = await _ctx.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null)
            {
                user = new User
                {
                    Email = dto.Email,
                    FirstName = dto.FirstName,
                    LastName = dto.LastName,
                    Role = dto.Role ?? "Tenant" // default role
                };
                _ctx.Users.Add(user);
                await _ctx.SaveChangesAsync();
            }

            // 3. Issue JWT
            var token = _tokenService.CreateToken(user);
            return Ok(new { token, user });
        }

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

        // Stub token verification
        private async Task<bool> VerifyExternalToken(string provider, string token)
        {
            // TODO: Call Google/Microsoft API to validate the token
            await Task.Delay(50); // simulate async
            return true; // stub: always valid for now
        }

    }
}
