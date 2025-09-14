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
        public async Task<ActionResult<Property>> Create([FromForm] Property property, IFormFile? image)
        {
            if (image != null)
            {
                using var ms = new MemoryStream();
                await image.CopyToAsync(ms);
                property.ImageData = ms.ToArray();
                property.ImageType = image.ContentType;
            }
            _ctx.Properties.Add(property);
            await _ctx.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = property.PropertyId }, property);
        }

        // Get all properties (public)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Property>>> GetAll() =>
            await _ctx.Properties.ToListAsync();

        // Get property by ID (public)
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Property>> Get(int id)
        {
            var prop = await _ctx.Properties.FindAsync(id);
            if (prop == null) return NotFound();
            return prop;
        }

        // Download property image
        [HttpGet("{id:int}/image")]
        public async Task<IActionResult> GetImage(int id)
        {
            var prop = await _ctx.Properties.FindAsync(id);
            if (prop == null || prop.ImageData == null) return NotFound();
            return File(prop.ImageData, prop.ImageType ?? "image/jpeg", $"property_{id}_image");
        }

        // Update property
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromForm] Property updated, IFormFile? image)
        {
            var prop = await _ctx.Properties.FindAsync(id);
            if (prop == null) return NotFound();

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

            if (image != null)
            {
                using var ms = new MemoryStream();
                await image.CopyToAsync(ms);
                prop.ImageData = ms.ToArray();
                prop.ImageType = image.ContentType;
            }

            await _ctx.SaveChangesAsync();
            return NoContent();
        }

        // Update property status 
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] PropertyStatusDto dto)
        {
            var prop = await _ctx.Properties.FindAsync(id);
            if (prop == null) return NotFound();

            prop.Status = dto.Status;
            await _ctx.SaveChangesAsync();
            return NoContent();
        }

        // Delete property
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var prop = await _ctx.Properties.FindAsync(id);
            if (prop == null) return NotFound();
            _ctx.Properties.Remove(prop);
            await _ctx.SaveChangesAsync();
            return NoContent();
        }
    }
}
