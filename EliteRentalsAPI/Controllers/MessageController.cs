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
                        await _fcm.SendAsync(
                            receiver.FcmToken,
                            "📩 New Message",
                            $"From {sender.FirstName}: {preview}",
                            new
                            {
                                type = "message",
                                senderId = msg.SenderId,
                                receiverId = msg.ReceiverId,
                                messageId = msg.MessageId
                            }
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
                        await _fcm.SendAsync(
                            user.FcmToken,
                            "📢 Announcement",
                            msg.MessageText,
                            new { type = "announcement" }
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
    }
}
