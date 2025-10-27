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
    public class MessageController : ControllerBase
    {
        private readonly AppDbContext _ctx;
        private readonly FcmService _fcm;
        private readonly ILogger<MessageController> _logger;

        public MessageController(AppDbContext ctx, FcmService fcm, ILogger<MessageController> logger)
        {
            _ctx = ctx;
            _fcm = fcm;
            _logger = logger;
        }

        // 🔹 Send message + push notification
        [Authorize]
        [HttpPost]
        public async Task<ActionResult<Message>> Send([FromBody] Message msg)
        {
            try
            {
                if (msg == null || msg.SenderId == 0 || msg.ReceiverId == 0 || string.IsNullOrWhiteSpace(msg.MessageText))
                    return BadRequest("Invalid message payload.");

                msg.Timestamp = DateTime.UtcNow;

                _ctx.Messages.Add(msg);
                await _ctx.SaveChangesAsync();

                var receiver = await _ctx.Users.FirstOrDefaultAsync(u => u.UserId == msg.ReceiverId);
                var sender = await _ctx.Users.FirstOrDefaultAsync(u => u.UserId == msg.SenderId);

                if (receiver == null || sender == null)
                    return NotFound("Sender or receiver not found.");

                if (!string.IsNullOrWhiteSpace(receiver.FcmToken))
                {
                    var preview = msg.MessageText.Length > 60
                        ? msg.MessageText.Substring(0, 60) + "..."
                        : msg.MessageText;

                    try
                    {
                        // Ensure all data payload values are strings
                        var dataPayload = new Dictionary<string, string>
        {
            { "type", "message" },
            { "senderId", msg.SenderId.ToString() },
            { "receiverId", msg.ReceiverId.ToString() },
            { "messageId", msg.MessageId.ToString() },
            { "preview", preview } // optional: for showing in notification click
        };

                        await _fcm.SendAsync(
                            receiver.FcmToken,
                            "📩 New Message",
                            $"From {sender.FirstName}: {preview}",
                            dataPayload
                        );
                    }
                    catch (Exception pushEx)
                    {
                        _logger.LogWarning(pushEx, "⚠️ Failed to send FCM push notification.");
                    }
                }


                return CreatedAtAction(nameof(GetById), new { id = msg.MessageId }, msg);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in Send()");
                return StatusCode(500, "Internal Server Error");
            }
        }

        // 🔹 Get message by ID
        [Authorize]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Message>> GetById(int id)
        {
            var message = await _ctx.Messages.FindAsync(id);
            if (message == null) return NotFound();
            return message;
        }

        // 🔹 Get conversation between two users
        [Authorize]
        [HttpGet("conversation/{user1:int}/{user2:int}")]
        public async Task<ActionResult<IEnumerable<Message>>> GetConversation(int user1, int user2)
        {
            return await _ctx.Messages
                .Where(m => (m.SenderId == user1 && m.ReceiverId == user2) ||
                            (m.SenderId == user2 && m.ReceiverId == user1))
                .OrderBy(m => m.Timestamp)
                .ToListAsync();
        }

        // 🔹 Inbox
        [Authorize]
        [HttpGet("inbox/{userId:int}")]
        public async Task<ActionResult<IEnumerable<Message>>> GetInbox(int userId)
        {
            return await _ctx.Messages
                .Where(m => m.ReceiverId == userId)
                .OrderByDescending(m => m.Timestamp)
                .ToListAsync();
        }

        // 🔹 Sent items
        [Authorize]
        [HttpGet("sent/{userId:int}")]
        public async Task<ActionResult<IEnumerable<Message>>> GetSent(int userId)
        {
            return await _ctx.Messages
                .Where(m => m.SenderId == userId)
                .OrderByDescending(m => m.Timestamp)
                .ToListAsync();
        }

        // 🔹 Send broadcast + push
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpPost("broadcast")]
        public async Task<ActionResult<Message>> SendBroadcast([FromBody] Message msg)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(msg.MessageText))
                    return BadRequest("Broadcast message cannot be empty.");

                msg.IsBroadcast = true;
                msg.ReceiverId = null;
                msg.Timestamp = DateTime.UtcNow;

                _ctx.Messages.Add(msg);
                await _ctx.SaveChangesAsync();

                var recipients = await _ctx.Users
                    .Where(u => (msg.TargetRole == null || u.Role == msg.TargetRole) && !string.IsNullOrEmpty(u.FcmToken))
                    .ToListAsync();

                _logger.LogInformation("📣 Sending broadcast to {Count} users", recipients.Count);

                foreach (var user in recipients)
                {
                    try
                    {
                        // 🔹 Ensure all payload values are strings
                        var dataPayload = new Dictionary<string, string>
                {
                    { "type", "announcement" },
                    { "messageId", msg.MessageId.ToString() },
                    { "targetRole", msg.TargetRole ?? "" },
                    { "title", "📢 Announcement" },
                    { "body", msg.MessageText }
                };

                        await _fcm.SendAsync(
                            user.FcmToken,
                            "📢 Announcement",
                            msg.MessageText,
                            dataPayload
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ Failed to push to {Email}", user.Email);
                    }
                }

                return CreatedAtAction(nameof(GetById), new { id = msg.MessageId }, msg);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in SendBroadcast()");
                return StatusCode(500, "Internal Server Error");
            }
        }


        // 🔹 Get announcements visible to a specific user
        [Authorize]
        [HttpGet("announcements/{userId:int}")]
        public async Task<ActionResult<IEnumerable<Message>>> GetAnnouncements(int userId)
        {
            var user = await _ctx.Users.FindAsync(userId);
            if (user == null) return NotFound("User not found.");

            return await _ctx.Messages
                .Where(m => m.IsBroadcast &&
                            (m.TargetRole == null || m.TargetRole == user.Role))
                .OrderByDescending(m => m.Timestamp)
                .ToListAsync();
        }

        [HttpPost("testpush/{userId:int}")]
        public async Task<IActionResult> TestPush(int userId)
        {
            try
            {
                var user = await _ctx.Users.FindAsync(userId);
                if (user == null) return NotFound($"User {userId} not found");

                if (string.IsNullOrWhiteSpace(user.FcmToken))
                    return BadRequest($"User {userId} does not have a FCM token");

                await _fcm.SendAsync(user.FcmToken, "Test Push", "This is a test message", new { type = "message" });
                return Ok("Push sent");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ TestPush error: {ex}");
                return StatusCode(500, ex.Message);
            }
        }

        // 🔹 Send rent due reminder to a tenant
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpPost("remind-rent/{tenantId:int}")]
        public async Task<IActionResult> SendRentReminder(int tenantId, [FromQuery] DateTime? dueDate)
        {
            var tenant = await _ctx.Users.FindAsync(tenantId);
            if (tenant == null || string.IsNullOrWhiteSpace(tenant.FcmToken))
                return NotFound("Tenant not found or FCM token missing");

            var rentDueDate = dueDate ?? DateTime.UtcNow.AddDays(3); // default: 3 days from now

            var dataPayload = new Dictionary<string, string>
    {
        { "type", "rent_due" },
        { "tenantId", tenant.UserId.ToString() },
        { "dueDate", rentDueDate.ToString("yyyy-MM-dd") }
    };

            await _fcm.SendAsync(
                tenant.FcmToken,
                "💰 Rent Due Reminder",
                $"Hi {tenant.FirstName}, your rent is due on {rentDueDate:dd MMM}.",
                dataPayload
            );

            return Ok("Rent reminder sent.");
        }

        // 🔹 Notify maintenance progress (Tenants & Caretakers)
        // 🔹 Notify maintenance progress (Tenants & Caretakers)
        [Authorize(Roles = "Admin,PropertyManager,Caretaker")]
        [HttpPost("maintenance-update/{maintenanceId:int}")]
        public async Task<IActionResult> NotifyMaintenanceUpdate(int maintenanceId, [FromQuery] string status)
        {
            var maintenance = await _ctx.Maintenance.FindAsync(maintenanceId);
            if (maintenance == null) return NotFound("Maintenance task not found");

            // Tenant notification
            var tenant = await _ctx.Users.FindAsync(maintenance.TenantId);
            if (tenant?.FcmToken != null)
            {
                await _fcm.SendAsync(
                    tenant.FcmToken,
                    "🛠 Maintenance Update",
                    $"Your maintenance request #{maintenanceId} is now '{status}'.",
                    new Dictionary<string, string>
                    {
                { "type", "maintenance_update" },
                { "maintenanceId", maintenanceId.ToString() },
                { "status", status }
                    }
                );
            }

            // Caretaker notification
            if (maintenance.AssignedCaretakerId.HasValue)
            {
                var caretaker = await _ctx.Users.FindAsync(maintenance.AssignedCaretakerId.Value);
                if (caretaker?.FcmToken != null)
                {
                    await _fcm.SendAsync(
                        caretaker.FcmToken,
                        "🛠 Task Update",
                        $"Maintenance task #{maintenanceId} status updated to '{status}'.",
                        new Dictionary<string, string>
                        {
                    { "type", "task_update" },
                    { "maintenanceId", maintenanceId.ToString() },
                    { "status", status }
                        }
                    );
                }
            }

            return Ok("Maintenance notifications sent.");
        }


        // 🔹 Notify escalations or overdue items (Property Managers & Admins)
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpPost("notify-escalation/{escalationId:int}")]
        public async Task<IActionResult> NotifyEscalation(int escalationId, [FromQuery] string description)
        {
            var recipients = await _ctx.Users
                .Where(u => (u.Role == "PropertyManager" || u.Role == "Admin") && !string.IsNullOrEmpty(u.FcmToken))
                .ToListAsync();

            foreach (var user in recipients)
            {
                await _fcm.SendAsync(
                    user.FcmToken,
                    "⚠️ Escalation Alert",
                    description,
                    new Dictionary<string, string>
                    {
                { "type", "escalation" },
                { "escalationId", escalationId.ToString() }
                    }
                );
            }

            return Ok("Escalation notifications sent.");
        }

        // 🔹 Archive a message
        [Authorize]
        [HttpPut("archive/{id:int}")]
        public async Task<IActionResult> ArchiveMessage(int id)
        {
            var msg = await _ctx.Messages.FindAsync(id);
            if (msg == null)
                return NotFound();

            msg.IsArchived = true;
            msg.ArchivedDate = DateTime.UtcNow;
            await _ctx.SaveChangesAsync();

            return Ok(new { message = "Message archived successfully." });
        }

        // 🔹 Restore a message
        [Authorize]
        [HttpPut("restore/{id:int}")]
        public async Task<IActionResult> RestoreMessage(int id)
        {
            var msg = await _ctx.Messages.FindAsync(id);
            if (msg == null)
                return NotFound();

            msg.IsArchived = false;
            msg.ArchivedDate = null;
            await _ctx.SaveChangesAsync();

            return Ok(new { message = "Message restored successfully." });
        }

        // 🔹 Get archived messages for a user
        [Authorize]
        [HttpGet("archived/{userId:int}")]
        public async Task<ActionResult<IEnumerable<Message>>> GetArchivedMessages(int userId)
        {
            var archived = await _ctx.Messages
                .Where(m => (m.SenderId == userId || m.ReceiverId == userId) && m.IsArchived)
                .OrderByDescending(m => m.ArchivedDate)
                .ToListAsync();

            return Ok(archived);
        }

    }
}
