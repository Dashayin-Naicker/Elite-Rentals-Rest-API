using EliteRentalsAPI.Data;
using EliteRentalsAPI.Helpers;
using EliteRentalsAPI.Models;
using EliteRentalsAPI.Services;
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
        private readonly EmailService _email;
        public LeaseController(AppDbContext ctx, EmailService email)
        {
            _ctx = ctx;
            _email = email;
        }

        // Create lease
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpPost]
        public async Task<ActionResult<Lease>> Create([FromBody] Lease lease)
        {
            lease.StartDate = DateTime.SpecifyKind(lease.StartDate, DateTimeKind.Utc);
            lease.EndDate = DateTime.SpecifyKind(lease.EndDate, DateTimeKind.Utc);

            var property = await _ctx.Properties.FindAsync(lease.PropertyId);
            if (property == null)
                return BadRequest($"Property with ID {lease.PropertyId} not found.");

            if (property.Status == "Occupied")
                return BadRequest("This property is already occupied. Please choose another property.");

            lease.Status = "Active"; // optional default
            property.Status = "Occupied"; // mark property as occupied

            await using var transaction = await _ctx.Database.BeginTransactionAsync();
            try
            {
                _ctx.Leases.Add(lease);
                await _ctx.SaveChangesAsync(); // create lease and get LeaseId

                // Commit property update at the same time
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Failed to create lease: {ex.Message}");
            }

            // Send email after transaction succeeds
            var tenant = await _ctx.Users.FindAsync(lease.TenantId);
            if (tenant != null)
            {
                try
                {
                    string subject = "Welcome to Your New Lease!";
                    string messageBody = $@"
                <p>Hi {tenant.FirstName},</p>
                <p>Your lease for property <b>{lease.PropertyId}</b> has been successfully created.</p>
                <p>Start: {lease.StartDate:yyyy-MM-dd}, End: {lease.EndDate:yyyy-MM-dd}</p>";
                    string htmlBody = EmailTemplateHelper.WrapEmail(subject, messageBody);
                    _email.SendEmail(tenant.Email, subject, htmlBody);
                }
                catch
                {
                    // Email failure should NOT break API
                }
            }

            return CreatedAtAction(nameof(Get), new { id = lease.LeaseId }, new
            {
                lease.LeaseId,
                lease.PropertyId,
                lease.TenantId,
                lease.StartDate,
                lease.EndDate,
                lease.Deposit,
                lease.Status
            });
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
        public async Task<ActionResult<object>> Get(int id)
        {
            var lease = await _ctx.Leases
                .Include(l => l.Tenant)
                .Include(l => l.Property)
                .FirstOrDefaultAsync(l => l.LeaseId == id);

            if (lease == null) return NotFound();

            // Map to a minimal DTO for the client
            var dto = new
            {
                lease.LeaseId,
                lease.StartDate,
                lease.EndDate,
                lease.Deposit,
                lease.Status,
                TenantId = lease.TenantId,
                TenantName = lease.Tenant != null ? $"{lease.Tenant.FirstName} {lease.Tenant.LastName}" : "Unknown Tenant",
                PropertyId = lease.PropertyId,
                PropertyTitle = lease.Property?.Title ?? "Unknown Property",
                lease.DocumentData,
                lease.DocumentType
            };

            return Ok(dto);
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

            if (!lease.IsArchived)
                return BadRequest("Please archive the lease before deleting it permanently.");

            // ✅ Mark property as Available before deletion
            var property = await _ctx.Properties.FindAsync(lease.PropertyId);
            if (property != null)
            {
                property.Status = "Available";
            }

            _ctx.Leases.Remove(lease);
            await _ctx.SaveChangesAsync();
            return NoContent();

        }

        // 🔹 Archive a lease (soft delete)
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpPut("archive/{id:int}")]
        public async Task<IActionResult> Archive(int id)
        {
            var lease = await _ctx.Leases.FindAsync(id);
            if (lease == null)
                return NotFound();

            lease.IsArchived = true;
            lease.ArchivedDate = DateTime.UtcNow;
            lease.Status = "Archived";

            // ✅ Mark property as Available
            var property = await _ctx.Properties.FindAsync(lease.PropertyId);
            if (property != null)
            {
                property.Status = "Available";
            }

            await _ctx.SaveChangesAsync();

            return Ok(new { message = "Lease archived successfully." });
        }

        // 🔹 Restore archived lease
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpPut("restore/{id:int}")]
        public async Task<IActionResult> Restore(int id)
        {
            var lease = await _ctx.Leases.FindAsync(id);
            if (lease == null)
                return NotFound();

            lease.IsArchived = false;
            lease.ArchivedDate = null;
            lease.Status = "Active";

            // ✅ Mark property as Occupied
            var property = await _ctx.Properties.FindAsync(lease.PropertyId);
            if (property != null)
            {
                property.Status = "Occupied";
            }

            await _ctx.SaveChangesAsync();
            return Ok(new { message = "Lease restored successfully." });

        }

        // 🔹 Get all archived leases (like GetAll)
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpGet("archived")]
        public async Task<ActionResult<IEnumerable<object>>> GetArchived()
        {
            var archived = await _ctx.Leases
                .Include(l => l.Property)
                .Include(l => l.Tenant)
                .Where(l => l.IsArchived)
                .OrderByDescending(l => l.ArchivedDate)
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
                    l.DocumentType,
                    l.ArchivedDate
                })
                .ToListAsync();

            return Ok(archived);
        }



        // Example: Server-side PDF generation (optional)
        // private void GenerateLeasePdf(Lease lease)
        // {
        //     // Use iTextSharp, PdfSharp, or any library to generate PDF
        //     // Save to database or filesystem if needed
        // }
    }
}
