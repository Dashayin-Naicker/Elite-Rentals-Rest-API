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
    public class PaymentController : ControllerBase
    {
        private readonly AppDbContext _ctx;
        private readonly FcmService _fcm;

        public PaymentController(AppDbContext ctx, FcmService fcm)
        {
            _ctx = ctx;
            _fcm = fcm;
        }

        // Submit payment (tenant)
        [HttpPost]
        [Authorize(Roles = "Tenant")]
        public async Task<ActionResult<Payment>> Create([FromForm] PaymentCreateDto dto, IFormFile? proof)
        {
            var payment = new Payment
            {
                TenantId = dto.TenantId,
                Amount = dto.Amount,
                Date = dto.Date,
                Status = "Pending"
            };

            if (proof != null)
            {
                using var ms = new MemoryStream();
                await proof.CopyToAsync(ms);
                payment.ProofData = ms.ToArray();
                payment.ProofType = proof.ContentType;
            }

            _ctx.Payments.Add(payment);
            await _ctx.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = payment.PaymentId }, payment);
        }


        // Get all payments (Admin/Manager only)
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Payment>>> GetAll() =>
            await _ctx.Payments.ToListAsync();

        // Get tenant payments
        [Authorize(Roles = "Tenant")]
        [HttpGet("tenant/{tenantId:int}")]
        public async Task<ActionResult<IEnumerable<Payment>>> GetTenantPayments(int tenantId) =>
            await _ctx.Payments.Where(p => p.TenantId == tenantId).ToListAsync();

        // Get single payment
        [Authorize]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Payment>> Get(int id)
        {
            var p = await _ctx.Payments.FindAsync(id);
            if (p == null) return NotFound();
            return p;
        }

        // Download proof of payment
        [Authorize]
        [HttpGet("{id:int}/proof")]
        public async Task<IActionResult> GetProof(int id)
        {
            var p = await _ctx.Payments.FindAsync(id);
            if (p == null || p.ProofData == null) return NotFound();
            return File(p.ProofData, p.ProofType ?? "application/octet-stream", $"payment_{id}_proof");
        }

        // Approve/Reject payment & notify tenant
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] PaymentStatusDto dto)
        {
            var p = await _ctx.Payments
                .Include(x => x.Tenant)
                .FirstOrDefaultAsync(x => x.PaymentId == id);

            if (p == null) return NotFound();

            // Only notify if status actually changes
            if (p.Status != dto.Status)
            {
                p.Status = dto.Status; // Paid, Overdue, Rejected
                await _ctx.SaveChangesAsync();

                if (!string.IsNullOrEmpty(p.Tenant?.FcmToken))
                {
                    try
                    {
                        await _fcm.SendAsync(
                            p.Tenant.FcmToken,
                            "💰 Payment Status Updated",
                            $"Hi {p.Tenant.FirstName}, your payment #{p.PaymentId} status has been changed to '{p.Status}'.",
                            new Dictionary<string, string>
                            {
                                { "type", "payment_status_update" },
                                { "paymentId", p.PaymentId.ToString() },
                                { "tenantId", p.TenantId.ToString() },
                                { "status", p.Status }
                            }
                        );
                    }
                    catch (Exception ex)
                    {
                        // log and continue
                        Console.WriteLine($"⚠️ Failed to send FCM: {ex.Message}");
                    }
                }
            }

            return NoContent();
        }
    }
}
