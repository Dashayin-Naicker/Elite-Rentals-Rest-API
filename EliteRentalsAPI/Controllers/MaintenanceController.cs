using EliteRentalsAPI.Data;
using EliteRentalsAPI.Models;
using EliteRentalsAPI.Models.DTOs;
using EliteRentalsAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EliteRentalsAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MaintenanceController : ControllerBase
    {
        private readonly AppDbContext _ctx;
        private readonly FcmService _fcm;
        private readonly ILogger<MaintenanceController> _logger;

        public MaintenanceController(AppDbContext ctx, FcmService fcm, ILogger<MaintenanceController> logger)
        {
            _ctx = ctx;
            _fcm = fcm;
            _logger = logger;
        }

        // Tenant creates request
        [Authorize(Roles = "Tenant")]
        [HttpPost]
        public async Task<ActionResult<Maintenance>> Create([FromForm] Maintenance request, IFormFile? proof)
        {
            if (proof != null)
            {
                using var ms = new MemoryStream();
                await proof.CopyToAsync(ms);
                request.ProofData = ms.ToArray();
                request.ProofType = proof.ContentType;
            }
            _ctx.Maintenance.Add(request);
            await _ctx.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = request.MaintenanceId }, request);
        }

        // Get all requests (Manager/Caretaker)
        [Authorize(Roles = "Admin,PropertyManager,Caretaker")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Maintenance>>> GetAll()
        {
            return await _ctx.Maintenance
                .Include(m => m.Property)
                .Include(m => m.Tenant)
                .Include(m => m.Caretaker)
                .ToListAsync();
        }

        // Get by ID
        [Authorize]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Maintenance>> Get(int id)
        {
            var m = await _ctx.Maintenance
                .Include(m => m.Property)
                .Include(m => m.Tenant)
                .Include(m => m.Caretaker)
                .FirstOrDefaultAsync(x => x.MaintenanceId == id);

            if (m == null) return NotFound();
            return m;
        }

        // Download proof image
        [Authorize]
        [HttpGet("{id:int}/proof")]
        public async Task<IActionResult> GetProof(int id)
        {
            var m = await _ctx.Maintenance.FindAsync(id);
            if (m == null || m.ProofData == null) return NotFound();
            return File(m.ProofData, m.ProofType ?? "image/jpeg", $"maintenance_{id}_proof");
        }

        // Update status (Caretaker/Manager)
        [Authorize(Roles = "Admin,PropertyManager,Caretaker")]
        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] MaintenanceStatusDto dto)
        {
            var m = await _ctx.Maintenance
                .Include(x => x.Tenant)
                .Include(x => x.Caretaker)
                .FirstOrDefaultAsync(x => x.MaintenanceId == id);

            if (m == null) return NotFound("Maintenance request not found.");

            // Update status and timestamp
            m.Status = dto.Status;
            m.UpdatedAt = DateTime.UtcNow;
            await _ctx.SaveChangesAsync();

            // 🔹 Notify tenant
            if (m.Tenant?.FcmToken != null)
            {
                try
                {
                    await _fcm.SendAsync(
                        m.Tenant.FcmToken,
                        "🛠 Maintenance Update",
                        $"Your maintenance request #{m.MaintenanceId} is now '{m.Status}'.",
                        new Dictionary<string, string>
                        {
                            { "type", "maintenance_update" },
                            { "maintenanceId", m.MaintenanceId.ToString() },
                            { "status", m.Status }
                        }
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Failed to send maintenance update to tenant {TenantId}", m.TenantId);
                }
            }

            // 🔹 Notify caretaker (if assigned)
            if (m.AssignedCaretakerId.HasValue && m.Caretaker?.FcmToken != null)
            {
                try
                {
                    await _fcm.SendAsync(
                        m.Caretaker.FcmToken,
                        "🔧 Maintenance Task Update",
                        $"Task #{m.MaintenanceId} status changed to '{m.Status}'.",
                        new Dictionary<string, string>
                        {
                            { "type", "task_update" },
                            { "maintenanceId", m.MaintenanceId.ToString() },
                            { "status", m.Status }
                        }
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Failed to send maintenance update to caretaker {CaretakerId}", m.AssignedCaretakerId);
                }
            }

            return Ok(new { message = "Status updated and notifications sent." });
        }

        // Get all requests for the current tenant
        [Authorize(Roles = "Tenant")]
        [HttpGet("my-requests")]
        public async Task<ActionResult<IEnumerable<Maintenance>>> GetTenantRequests()
        {
            var tenantIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value;
            if (tenantIdClaim == null || !int.TryParse(tenantIdClaim, out int tenantId))
                return Unauthorized();


            var tenantRequests = await _ctx.Maintenance
                .Where(m => m.TenantId == tenantId)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();

            return Ok(tenantRequests);

        }

        // Update caretaker assignment (Manager/Admin)
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpPut("{id:int}/assign-caretaker")]
        public async Task<IActionResult> AssignCaretaker(int id, [FromBody] AssignCaretakerDto dto)
        {
            var m = await _ctx.Maintenance
                .Include(x => x.Property)
                .Include(x => x.Tenant)
                .FirstOrDefaultAsync(x => x.MaintenanceId == id);

            if (m == null)
                return NotFound(new { message = "Maintenance request not found." });

            var caretaker = await _ctx.Users.FindAsync(dto.AssignedCaretakerId);
            if (caretaker == null || caretaker.Role != "Caretaker")
                return BadRequest(new { message = "Invalid caretaker ID." });

            m.AssignedCaretakerId = dto.AssignedCaretakerId;
            m.UpdatedAt = DateTime.UtcNow;
            await _ctx.SaveChangesAsync();

            // 🔹 Notify caretaker
            if (!string.IsNullOrWhiteSpace(caretaker.FcmToken))
            {
                try
                {
                    await _fcm.SendAsync(
                        caretaker.FcmToken,
                        "🛠 New Maintenance Task Assigned",
                        $"You have been assigned to maintenance request #{m.MaintenanceId} at {m.Property?.Address ?? "property"}",
                        new Dictionary<string, string>
                        {
                    { "type", "task_assignment" },
                    { "maintenanceId", m.MaintenanceId.ToString() },
                    { "tenantId", m.TenantId.ToString() },
                    { "propertyId", m.PropertyId.ToString() }
                        }
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Failed to send task assignment notification to caretaker {CaretakerId}", caretaker.UserId);
                }
            }

            return Ok(new { message = "Caretaker assigned and notified successfully." });
        }

        // Get all requests assigned to the current caretaker
        [Authorize(Roles = "Caretaker")]
        [HttpGet("caretaker-requests")]
        public async Task<ActionResult<IEnumerable<Maintenance>>> GetCaretakerRequests()
        {
            var caretakerIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value;
            if (caretakerIdClaim == null || !int.TryParse(caretakerIdClaim, out int caretakerId))
                return Unauthorized();

            var caretakerRequests = await _ctx.Maintenance
                .Include(m => m.Property)
                .Include(m => m.Tenant)
                .Include(m => m.Caretaker)
                .Where(m => m.AssignedCaretakerId == caretakerId)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();

            return Ok(caretakerRequests);
        }

        [Authorize(Roles = "Tenant,Caretaker,Admin,PropertyManager")]
        [HttpPost("{id:int}/proof")]
        public async Task<IActionResult> UpdateProof(int id, IFormFile proof)
        {
            var m = await _ctx.Maintenance.FindAsync(id);
            if (m == null) return NotFound();

            if (proof != null)
            {
                using var ms = new MemoryStream();
                await proof.CopyToAsync(ms);
                m.ProofData = ms.ToArray();
                m.ProofType = proof.ContentType;
                m.UpdatedAt = DateTime.UtcNow;
                await _ctx.SaveChangesAsync();
            }

            return Ok(new { message = "Proof updated successfully." });
        }





    }
}
