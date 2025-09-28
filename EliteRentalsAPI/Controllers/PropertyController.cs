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
    public class PropertyController : ControllerBase
    {
        private readonly AppDbContext _ctx;
        public PropertyController(AppDbContext ctx) { _ctx = ctx; }

        // Create property
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpPost]
        public async Task<ActionResult<Property>> Create([FromForm] PropertyUploadDto dto, IFormFile? image)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.SelectMany(kvp => kvp.Value.Errors.Select(err => $"{kvp.Key}: {err.ErrorMessage}"));
                return BadRequest(new { Errors = errors });
            }

            var managerIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userId" || c.Type == "nameid");
            if (managerIdClaim == null || !int.TryParse(managerIdClaim.Value, out int managerId))
                return Unauthorized(new { Message = "Manager ID missing from token." });

            var property = new Property
            {
                Title = dto.Title,
                Description = dto.Description,
                Address = dto.Address,
                City = dto.City,
                Province = dto.Province,
                Country = dto.Country,
                RentAmount = dto.RentAmount,
                NumOfBedrooms = dto.NumOfBedrooms,
                NumOfBathrooms = dto.NumOfBathrooms,
                ParkingType = dto.ParkingType,
                NumOfParkingSpots = dto.NumOfParkingSpots,
                PetFriendly = dto.PetFriendly,
                Status = dto.Status,
                ManagerId = managerId,
                ListingDate = DateTime.UtcNow
            };

            if (image != null)
            {
                using var ms = new MemoryStream();
                await image.CopyToAsync(ms);
                property.ImageData = ms.ToArray();
                property.ImageType = image.ContentType ?? "image/jpeg";
            }

            _ctx.Properties.Add(property);
            await _ctx.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = property.PropertyId }, property);
        }

        // Get all properties (public)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PropertyReadDto>>> GetAll()
        {
            var props = await _ctx.Properties
                .Include(p => p.Manager)
                .Select(p => new PropertyReadDto
                {
                    PropertyId = p.PropertyId,
                    Title = p.Title,
                    Description = p.Description,
                    Address = p.Address,
                    City = p.City,
                    Province = p.Province,
                    Country = p.Country,
                    RentAmount = p.RentAmount,
                    NumOfBedrooms = p.NumOfBedrooms,
                    NumOfBathrooms = p.NumOfBathrooms,
                    ParkingType = p.ParkingType,
                    NumOfParkingSpots = p.NumOfParkingSpots,
                    PetFriendly = p.PetFriendly,
                    Status = p.Status,
                    Manager = p.Manager == null ? null : new ManagerReadDto
                    {
                        UserId = p.Manager.UserId,
                        FirstName = p.Manager.FirstName,
                        LastName = p.Manager.LastName,
                        Email = p.Manager.Email
                    }
                })
                .ToListAsync();

            return Ok(props);
        }


        // Get property by ID (public)
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Property>> Get(int id)
        {
            var prop = await _ctx.Properties.FindAsync(id);
            if (prop == null) return NotFound(new { Message = $"Property {id} not found" });
            return Ok(prop);
        }

        // Download property image
        [HttpGet("{id:int}/image")]
        public async Task<IActionResult> GetImage(int id)
        {
            var prop = await _ctx.Properties.FindAsync(id);
            if (prop == null || prop.ImageData == null)
                return NotFound(new { Message = "Image not found" });

            return File(prop.ImageData, prop.ImageType ?? "image/jpeg", $"property_{id}_image");
        }

        // Update property
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromForm] Property updated, IFormFile? image)
        {
            var prop = await _ctx.Properties.FindAsync(id);
            if (prop == null) return NotFound(new { Message = $"Property {id} not found" });

            prop.Title = updated.Title;
            prop.Description = updated.Description;
            prop.Address = updated.Address;
            prop.City = updated.City;
            prop.Province = updated.Province;
            prop.Country = updated.Country;
            prop.RentAmount = updated.RentAmount;
            prop.NumOfBedrooms = updated.NumOfBedrooms;
            prop.NumOfBathrooms = updated.NumOfBathrooms;
            prop.ParkingType = updated.ParkingType;
            prop.NumOfParkingSpots = updated.NumOfParkingSpots;
            prop.PetFriendly = updated.PetFriendly;
            prop.Status = updated.Status;

            if (image != null)
            {
                using var ms = new MemoryStream();
                await image.CopyToAsync(ms);
                prop.ImageData = ms.ToArray();
                prop.ImageType = image.ContentType ?? "image/jpeg";
            }

            await _ctx.SaveChangesAsync();
            return Ok(new { Message = $"Property {id} updated successfully" });
        }

        // Update property status
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] PropertyStatusDto dto)
        {
            var prop = await _ctx.Properties.FindAsync(id);
            if (prop == null) return NotFound(new { Message = $"Property {id} not found" });

            prop.Status = dto.Status;
            await _ctx.SaveChangesAsync();
            return Ok(new { Message = $"Property {id} status updated to {dto.Status}" });
        }

        // Delete property
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var prop = await _ctx.Properties.FindAsync(id);
            if (prop == null) return NotFound(new { Message = $"Property {id} not found" });

            _ctx.Properties.Remove(prop);
            await _ctx.SaveChangesAsync();
            return Ok(new { Message = $"Property {id} deleted successfully" });
        }
    }
}
