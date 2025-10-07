using EliteRentalsAPI.Data;
using EliteRentalsAPI.Models;
using EliteRentalsAPI.Models.DTOs;
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
        public MaintenanceController(AppDbContext ctx) { _ctx = ctx; }

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
        public async Task<ActionResult<IEnumerable<Maintenance>>> GetAll() =>
            await _ctx.Maintenance.ToListAsync();

        // Get by ID
        [Authorize]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Maintenance>> Get(int id)
        {
            var m = await _ctx.Maintenance.FindAsync(id);
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
            var m = await _ctx.Maintenance.FindAsync(id);
            if (m == null) return NotFound();

            m.Status = dto.Status;
            m.UpdatedAt = DateTime.UtcNow;
            await _ctx.SaveChangesAsync();
            return NoContent();
        }

        // Get all requests for the current tenant
        [Authorize(Roles = "Tenant")]
        [HttpGet("my-requests")]
        public async Task<ActionResult<IEnumerable<Maintenance>>> GetTenantRequests()
        {
            var tenantIdClaim = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
            if (tenantIdClaim == null || !int.TryParse(tenantIdClaim, out int tenantId))
                return Unauthorized();

            var tenantRequests = await _ctx.Maintenance
                .Where(m => m.TenantId == tenantId)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();

            return Ok(tenantRequests);

        }

    }
}
