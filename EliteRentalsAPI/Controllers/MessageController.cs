using EliteRentalsAPI.Data;
using EliteRentalsAPI.Models;
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
        public MessageController(AppDbContext ctx) { _ctx = ctx; }

        // Send message
        [Authorize]
        [HttpPost]
        public async Task<ActionResult<Message>> Send(Message msg)
        {
            _ctx.Messages.Add(msg);
            await _ctx.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = msg.MessageId }, msg);
        }

        // Get message by ID
        [Authorize]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Message>> GetById(int id)
        {
            var m = await _ctx.Messages.FindAsync(id);
            if (m == null) return NotFound();
            return m;
        }

        // Get conversation between two users
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

        // Get all messages for a user (inbox)
        [Authorize]
        [HttpGet("inbox/{userId:int}")]
        public async Task<ActionResult<IEnumerable<Message>>> GetInbox(int userId)
        {
            return await _ctx.Messages
                .Where(m => m.ReceiverId == userId)
                .OrderByDescending(m => m.Timestamp)
                .ToListAsync();
        }

        // Get all messages sent by a user
        [Authorize]
        [HttpGet("sent/{userId:int}")]
        public async Task<ActionResult<IEnumerable<Message>>> GetSent(int userId)
        {
            return await _ctx.Messages
                .Where(m => m.SenderId == userId)
                .OrderByDescending(m => m.Timestamp)
                .ToListAsync();
        }

        [Authorize]
        [HttpPost("broadcast")]
        public async Task<ActionResult<Message>> SendBroadcast([FromBody] Message msg)
        {
            msg.IsBroadcast = true;
            msg.ReceiverId = null;
            msg.Timestamp = DateTime.UtcNow;

            _ctx.Messages.Add(msg);
            await _ctx.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = msg.MessageId }, msg);
        }

        [Authorize]
        [HttpGet("announcements/{userId:int}")]
        public async Task<ActionResult<IEnumerable<Message>>> GetAnnouncements(int userId)
        {
            var user = await _ctx.Users.FindAsync(userId);
            if (user == null) return NotFound();

            var role = user.Role;

            var announcements = await _ctx.Messages
                .Where(m => m.IsBroadcast &&
                            (m.TargetRole == null || m.TargetRole == role))
                .OrderByDescending(m => m.Timestamp)
                .ToListAsync();

            return announcements;
        }


    }
}
