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

        // ✅ Create lease
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpPost]
        public async Task<ActionResult<Lease>> Create([FromForm] Lease lease, IFormFile? document)
        {
            if (document != null)
            {
                using var ms = new MemoryStream();
                await document.CopyToAsync(ms);
                lease.DocumentData = ms.ToArray();
                lease.DocumentType = document.ContentType;
            }
            _ctx.Leases.Add(lease);
            await _ctx.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = lease.LeaseId }, lease);
        }

        // ✅ Get all leases
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Lease>>> GetAll() =>
            await _ctx.Leases.ToListAsync();

        // ✅ Get lease by ID
        [Authorize]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Lease>> Get(int id)
        {
            var lease = await _ctx.Leases.FindAsync(id);
            if (lease == null) return NotFound();
            return lease;
        }

        // ✅ Download lease document
        [Authorize]
        [HttpGet("{id:int}/document")]
        public async Task<IActionResult> GetDocument(int id)
        {
            var lease = await _ctx.Leases.FindAsync(id);
            if (lease == null || lease.DocumentData == null) return NotFound();
            return File(lease.DocumentData, lease.DocumentType ?? "application/pdf", $"lease_{id}.pdf");
        }

        // ✅ Update lease
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromForm] Lease updated, IFormFile? document)
        {
            var lease = await _ctx.Leases.FindAsync(id);
            if (lease == null) return NotFound();

            lease.StartDate = updated.StartDate;
            lease.EndDate = updated.EndDate;
            lease.Deposit = updated.Deposit;
            lease.Status = updated.Status;

            if (document != null)
            {
                using var ms = new MemoryStream();
                await document.CopyToAsync(ms);
                lease.DocumentData = ms.ToArray();
                lease.DocumentType = document.ContentType;
            }

            await _ctx.SaveChangesAsync();
            return NoContent();
        }

        // ✅ Delete lease
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
    }
}
