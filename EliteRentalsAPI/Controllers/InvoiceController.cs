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
    public class InvoiceController : ControllerBase
    {
        private readonly AppDbContext _ctx;
        public InvoiceController(AppDbContext ctx) { _ctx = ctx; }

        // Create invoice (Admin/Manager only)
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpPost]
        public async Task<ActionResult<Invoice>> Create([FromForm] Invoice invoice, IFormFile? pdf)
        {
            if (pdf != null)
            {
                using var ms = new MemoryStream();
                await pdf.CopyToAsync(ms);
                invoice.PdfData = ms.ToArray();
                invoice.PdfType = pdf.ContentType;
            }
            _ctx.Invoices.Add(invoice);
            await _ctx.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = invoice.InvoiceId }, invoice);
        }

        // Get all invoices (Admin/Manager)
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Invoice>>> GetAll() =>
            await _ctx.Invoices.ToListAsync();

        // Get tenant’s invoices
        [Authorize(Roles = "Tenant")]
        [HttpGet("tenant/{tenantId:int}")]
        public async Task<ActionResult<IEnumerable<Invoice>>> GetTenantInvoices(int tenantId) =>
            await _ctx.Invoices.Where(i => i.TenantId == tenantId).ToListAsync();

        // Get single invoice
        [Authorize]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Invoice>> Get(int id)
        {
            var inv = await _ctx.Invoices.FindAsync(id);
            if (inv == null) return NotFound();
            return inv;
        }

        // Download invoice PDF
        [Authorize]
        [HttpGet("{id:int}/pdf")]
        public async Task<IActionResult> GetPdf(int id)
        {
            var inv = await _ctx.Invoices.FindAsync(id);
            if (inv == null || inv.PdfData == null) return NotFound();
            return File(inv.PdfData, inv.PdfType ?? "application/pdf", $"invoice_{id}.pdf");
        }

        // Update invoice status
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] InvoiceStatusDto dto)
        {
            var inv = await _ctx.Invoices.FindAsync(id);
            if (inv == null) return NotFound();

            inv.Status = dto.Status;
            await _ctx.SaveChangesAsync();
            return NoContent();
        }
    }
}
