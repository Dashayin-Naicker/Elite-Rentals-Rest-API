using EliteRentalsAPI.Data;
using EliteRentalsAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EliteRentalsAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReportController : ControllerBase
    {
        private readonly AppDbContext _ctx;
        public ReportController(AppDbContext ctx) { _ctx = ctx; }

        // Generate report (upload generated file)
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpPost]
        public async Task<ActionResult<Report>> Create([FromForm] Report report, IFormFile? file)
        {
            if (file != null)
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                report.ReportData = ms.ToArray();
                report.FileType = file.ContentType;
            }
            _ctx.Reports.Add(report);
            await _ctx.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = report.ReportId }, report);
        }

        // Get all reports
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Report>>> GetAll() =>
            await _ctx.Reports.ToListAsync();

        // Get report by ID
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Report>> Get(int id)
        {
            var r = await _ctx.Reports.FindAsync(id);
            if (r == null) return NotFound();
            return r;
        }

        // Download report file
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpGet("{id:int}/file")]
        public async Task<IActionResult> GetFile(int id)
        {
            var r = await _ctx.Reports.FindAsync(id);
            if (r == null || r.ReportData == null) return NotFound();
            return File(r.ReportData, r.FileType ?? "application/pdf", $"report_{id}.pdf");
        }
    }
}
