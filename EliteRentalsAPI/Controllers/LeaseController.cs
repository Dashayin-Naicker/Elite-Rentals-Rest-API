using EliteRentalsAPI.Data;
using EliteRentalsAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EliteRentalsAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LeaseController : ControllerBase
    {
        private readonly AppDbContext _ctx;
        public LeaseController(AppDbContext ctx) { _ctx = ctx; }

        // Create lease
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpPost]
        public async Task<ActionResult<Lease>> Create([FromBody] Lease lease)
        {

            lease.StartDate = DateTime.SpecifyKind(lease.StartDate, DateTimeKind.Utc);
            lease.EndDate = DateTime.SpecifyKind(lease.EndDate, DateTimeKind.Utc);

            _ctx.Leases.Add(lease);
            await _ctx.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = lease.LeaseId }, lease);
        }

        // Get all leases
        [Authorize(Roles = "Admin,PropertyManager,Tenant")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetAll()
        {
            var leases = await _ctx.Leases
                .Include(l => l.Property)
                .Include(l => l.Tenant)
                .Select(l => new
                {
                    l.LeaseId,
                    l.PropertyId,
                    Property = l.Property == null ? null : new
                    {
                        l.Property.PropertyId,
                        l.Property.Title,
                        l.Property.Description,
                        l.Property.RentAmount
                    },
                    l.TenantId,
                    Tenant = l.Tenant == null ? null : new
                    {
                        l.Tenant.UserId,
                        l.Tenant.FirstName,
                        l.Tenant.LastName,
                        l.Tenant.Email
                    },
                    l.StartDate,
                    l.EndDate,
                    l.Deposit,
                    l.Status,
                    l.DocumentData,
                    l.DocumentType
                }).ToListAsync();

            return Ok(leases);
        }


        // Get lease by ID
        [Authorize]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Lease>> Get(int id)
        {
            var lease = await _ctx.Leases.FindAsync(id);
            if (lease == null) return NotFound();
            return lease;
        }

        // Download lease document
        [Authorize]
        [HttpGet("{id:int}/document")]
        public async Task<IActionResult> GetDocument(int id)
        {
            var lease = await _ctx.Leases.FindAsync(id);
            if (lease == null || lease.DocumentData == null) return NotFound();
            return File(lease.DocumentData, lease.DocumentType ?? "application/pdf", $"lease_{id}.pdf");
        }

        // Update lease from JSON
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] Lease updated)
        {
            if (updated == null)
                return BadRequest("Invalid request body — lease data missing.");

            try
            {
                var lease = await _ctx.Leases.FindAsync(id);
                if (lease == null)
                    return NotFound($"Lease with ID {id} not found.");

                // ✅ Update only scalar fields (not navigation properties)
                lease.StartDate = DateTime.SpecifyKind(updated.StartDate, DateTimeKind.Utc);
                lease.EndDate = DateTime.SpecifyKind(updated.EndDate, DateTimeKind.Utc);
                lease.Deposit = updated.Deposit;
                lease.Status = updated.Status;


                // Prevent EF from thinking related entities changed
                _ctx.Entry(lease).Reference(l => l.Property).IsModified = false;
                _ctx.Entry(lease).Reference(l => l.Tenant).IsModified = false;

                await _ctx.SaveChangesAsync();

                return NoContent();
            }
            catch (DbUpdateException dbEx)
            {
                // Handles SQL constraint errors (e.g., FK violations)
                return StatusCode(500, $"Database update failed: {dbEx.InnerException?.Message ?? dbEx.Message}");
            }
            catch (Exception ex)
            {
                // General fallback for unexpected issues
                return StatusCode(500, $"Server error: {ex.Message}");
            }
        }


        // Delete lease
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var lease = await _ctx.Leases.FindAsync(id);
            if (lease == null) return NotFound();
            _ctx.Leases.Remove(lease);
            await _ctx.SaveChangesAsync();
            return NoContent();
        }

        // Example: Server-side PDF generation (optional)
        // private void GenerateLeasePdf(Lease lease)
        // {
        //     // Use iTextSharp, PdfSharp, or any library to generate PDF
        //     // Save to database or filesystem if needed
        // }
    }
}
