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
    public class PaymentController : ControllerBase
    {
        private readonly AppDbContext _ctx;
        public PaymentController(AppDbContext ctx) { _ctx = ctx; }

        // ✅ Submit payment (tenant)
        [Authorize(Roles = "Tenant")]
        [HttpPost]
        public async Task<ActionResult<Payment>> Create([FromForm] Payment payment, IFormFile? proof)
        {
            if (proof != null)
            {
                using var ms = new MemoryStream();
                await proof.CopyToAsync(ms);
                payment.ProofData = ms.ToArray();
                payment.ProofType = proof.ContentType;
            }
            payment.Status = "Pending"; // default
            _ctx.Payments.Add(payment);
            await _ctx.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = payment.PaymentId }, payment);
        }

        // ✅ Get all payments (Admin/Manager only)
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Payment>>> GetAll() =>
            await _ctx.Payments.ToListAsync();

        // ✅ Get tenant payments
        [Authorize(Roles = "Tenant")]
        [HttpGet("tenant/{tenantId:int}")]
        public async Task<ActionResult<IEnumerable<Payment>>> GetTenantPayments(int tenantId) =>
            await _ctx.Payments.Where(p => p.TenantId == tenantId).ToListAsync();

        // ✅ Get single payment
        [Authorize]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Payment>> Get(int id)
        {
            var p = await _ctx.Payments.FindAsync(id);
            if (p == null) return NotFound();
            return p;
        }

        // ✅ Download proof of payment
        [Authorize]
        [HttpGet("{id:int}/proof")]
        public async Task<IActionResult> GetProof(int id)
        {
            var p = await _ctx.Payments.FindAsync(id);
            if (p == null || p.ProofData == null) return NotFound();
            return File(p.ProofData, p.ProofType ?? "application/octet-stream", $"payment_{id}_proof");
        }

        // ✅ Approve/Reject payment
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] PaymentStatusDto dto)
        {
            var p = await _ctx.Payments.FindAsync(id);
            if (p == null) return NotFound();

            p.Status = dto.Status; // Paid, Overdue, Rejected
            await _ctx.SaveChangesAsync();
            return NoContent();
        }
    }
}
