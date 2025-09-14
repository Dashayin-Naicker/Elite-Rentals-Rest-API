using EliteRentalsAPI.Data;
using EliteRentalsAPI.Models;
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
        public NotificationController(AppDbContext ctx) { _ctx = ctx; }

        // Create notification (system use)
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpPost]
        public async Task<ActionResult<Notification>> Create(Notification n)
        {
            _ctx.Notifications.Add(n);
            await _ctx.SaveChangesAsync();
            return CreatedAtAction(nameof(GetByUser), new { userId = n.UserId }, n);
        }

        // Get notifications for a user
        [Authorize]
        [HttpGet("user/{userId:int}")]
        public async Task<ActionResult<IEnumerable<Notification>>> GetByUser(int userId) =>
            await _ctx.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.Date)
                .ToListAsync();

        // Mark as read
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
    }
}
