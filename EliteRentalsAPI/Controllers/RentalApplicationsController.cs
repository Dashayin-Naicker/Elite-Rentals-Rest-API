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
    public class RentalApplicationsController : ControllerBase
    {
        private readonly AppDbContext _ctx;
        public RentalApplicationsController(AppDbContext ctx) { _ctx = ctx; }

        // ✅ Submit rental application (public)
        [HttpPost]
        public async Task<ActionResult<RentalApplication>> Create([FromForm] RentalApplication app, IFormFile? document)
        {
            if (document != null)
            {
                using var ms = new MemoryStream();
                await document.CopyToAsync(ms);
                app.DocumentData = ms.ToArray();
                app.DocumentType = document.ContentType;
            }
            _ctx.Applications.Add(app);
            await _ctx.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = app.ApplicationId }, app);
        }

        // ✅ Get all applications (PropertyManagers/Admin only)
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<RentalApplication>>> GetAll() =>
            await _ctx.Applications.ToListAsync();

        // ✅ Get one application
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<RentalApplication>> Get(int id)
        {
            var app = await _ctx.Applications.FindAsync(id);
            if (app == null) return NotFound();
            return app;
        }

        // ✅ Download application document
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpGet("{id:int}/document")]
        public async Task<IActionResult> GetDocument(int id)
        {
            var app = await _ctx.Applications.FindAsync(id);
            if (app == null || app.DocumentData == null) return NotFound();
            return File(app.DocumentData, app.DocumentType ?? "application/octet-stream", $"application_{id}.pdf");
        }

        // ✅ Approve/Reject
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] RentalApplicationStatusDto dto)
        {
            var app = await _ctx.Applications.FindAsync(id);
            if (app == null) return NotFound();

            app.Status = dto.Status;
            await _ctx.SaveChangesAsync();
            return NoContent();
        }
    }
}
