using EliteRentalsAPI.Data;
using EliteRentalsAPI.Helpers;
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
    public class RentalApplicationsController : ControllerBase
    {
        private readonly AppDbContext _ctx;
        private readonly EmailService _email;
        public RentalApplicationsController(AppDbContext ctx, EmailService email)
        {
            _ctx = ctx;
            _email = email;
        }

        // Submit rental application (public)
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

            // ✅ Send email confirmation to applicant
            if (!string.IsNullOrEmpty(app.Email))
            {
                string subject = "We've Received Your Rental Application!";
                string messageBody = $@"
<p>Hi {app.ApplicantName},</p>
<p>Thanks for submitting your rental application to Elite Rentals — we're excited to review it!</p>
<p>Your application ID is <b>{app.ApplicationId}</b>. Our team will carefully assess your details and get back to you shortly.</p>
<p>If you have any questions in the meantime, feel free to reach out.</p>
<p>Warm regards,<br><b>The Elite Rentals Team</b></p>";


                string htmlBody = EmailTemplateHelper.WrapEmail(subject, messageBody);
                _email.SendEmail(app.Email, subject, htmlBody);

            }

            return CreatedAtAction(nameof(Get), new { id = app.ApplicationId }, app);
        }

        // Get all applications (PropertyManagers/Admin only)
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<RentalApplication>>> GetAll() =>
            await _ctx.Applications.ToListAsync();

        // Get one application
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<RentalApplication>> Get(int id)
        {
            var app = await _ctx.Applications.FindAsync(id);
            if (app == null) return NotFound();
            return app;
        }

        // Download application document
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpGet("{id:int}/document")]
        public async Task<IActionResult> GetDocument(int id)
        {
            var app = await _ctx.Applications.FindAsync(id);
            if (app == null || app.DocumentData == null) return NotFound();
            return File(app.DocumentData, app.DocumentType ?? "application/octet-stream", $"application_{id}.pdf");
        }

        // Approve/Reject
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] RentalApplicationStatusDto dto)
        {
            var app = await _ctx.Applications.FindAsync(id);
            if (app == null) return NotFound();

            app.Status = dto.Status;
            await _ctx.SaveChangesAsync();

            // ✅ Send status update email
            string subject = $"Update on Your Rental Application: {dto.Status}";
            string messageBody = $@"
<p>Hi {app.ApplicantName},</p>
<p>We've reviewed your rental application and the status is now: <b>{dto.Status}</b>.</p>
{(dto.Status == "Approved"
                ? "<p>Congratulations! A team member will be in touch soon to finalize the next steps.</p>"
                : "<p>We appreciate your interest, and while this property wasn't the right fit, we encourage you to explore other listings with us.</p>")}
<p>Thank you for considering Elite Rentals.<br><b>The Elite Rentals Team</b></p>";


            string htmlBody = EmailTemplateHelper.WrapEmail(subject, messageBody);
            _email.SendEmail(app.Email, subject, htmlBody);


            return NoContent();
        }
    }
}
