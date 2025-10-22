using EliteRentalsAPI.Data;
using EliteRentalsAPI.Models;
using EliteRentalsAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EliteRentalsAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationController : ControllerBase
    {
        private readonly AppDbContext _ctx;
        private readonly FcmService _fcm;

        public NotificationController(AppDbContext ctx, FcmService fcm)
        {
            _ctx = ctx;
            _fcm = fcm;
        }

        // 🔔 Create notification and send push
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpPost]
        public async Task<ActionResult<Notification>> Create(Notification n)
        {
            _ctx.Notifications.Add(n);
            await _ctx.SaveChangesAsync();

            var user = await _ctx.Users.FindAsync(n.UserId);
            if (user?.FcmToken != null)
            {
                await _fcm.SendAsync(user.FcmToken, "EliteRentals", n.Message);
            }

            return CreatedAtAction(nameof(GetByUser), new { userId = n.UserId }, n);
        }

        // 📬 Get notifications for a user
        [Authorize]
        [HttpGet("user/{userId:int}")]
        public async Task<ActionResult<IEnumerable<Notification>>> GetByUser(int userId) =>
            await _ctx.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.Date)
                .ToListAsync();

        // ✅ Mark notification as read
        [Authorize]
        [HttpPut("{id:int}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var n = await _ctx.Notifications.FindAsync(id);
            if (n == null) return NotFound();

            n.IsRead = true;
            await _ctx.SaveChangesAsync();
            return NoContent();
        }

        // 📢 Optional: Broadcast to role group
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpPost("broadcast")]
        public async Task<IActionResult> Broadcast([FromBody] BroadcastDto dto)
        {
            var recipients = await _ctx.Users
                .Where(u => u.Role == dto.Role && u.FcmToken != null)
                .ToListAsync();

            foreach (var user in recipients)
            {
                var notification = new Notification
                {
                    UserId = user.UserId,
                    Message = dto.Message,
                    Date = DateTime.UtcNow
                };

                _ctx.Notifications.Add(notification);
                await _fcm.SendAsync(user.FcmToken, "EliteRentals", dto.Message);
            }

            await _ctx.SaveChangesAsync();
            return Ok(new { message = $"Broadcast sent to {recipients.Count} {dto.Role}s" });
        }

        public class BroadcastDto
        {
            public string Role { get; set; } = ""; // e.g. "Tenant", "Caretaker"
            public string Message { get; set; } = "";
        }
    }
}
