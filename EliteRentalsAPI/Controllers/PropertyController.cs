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

        // ✅ Create property with multiple images
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpPost]
        public async Task<ActionResult<Property>> Create([FromForm] PropertyUploadDto dto)
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
                ListingDate = DateTime.UtcNow,
                Images = new List<PropertyImage>()
            };

            // ✅ Handle multiple images
            if (dto.Images != null && dto.Images.Count > 0)
            {
                foreach (var img in dto.Images)
                {
                    using var ms = new MemoryStream();
                    await img.CopyToAsync(ms);
                    property.Images.Add(new PropertyImage
                    {
                        ImageData = ms.ToArray(),
                        ImageType = img.ContentType ?? "image/jpeg"
                    });
                }
            }

            _ctx.Properties.Add(property);
            await _ctx.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = property.PropertyId }, property);
        }

        // ✅ Get all properties
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PropertyReadDto>>> GetAll()
        {
            var props = await _ctx.Properties
                .Include(p => p.Manager)
                .Include(p => p.Images)
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
                    ImageUrls = p.Images.Select(i =>
    $"{Request.Scheme}://{Request.Host}/api/property/image/{i.PropertyImageId}"
).ToList(),

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

        // ✅ Get property by ID
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Property>> Get(int id)
        {
            var prop = await _ctx.Properties
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.PropertyId == id);

            if (prop == null)
                return NotFound(new { Message = $"Property {id} not found" });

            return Ok(prop);
        }

        // ✅ Download specific property image
        [HttpGet("image/{imageId:int}")]
        public async Task<IActionResult> GetImage(int imageId)
        {
            var img = await _ctx.PropertyImages.FindAsync(imageId);
            if (img == null)
                return NotFound(new { Message = "Image not found" });

            return File(img.ImageData, img.ImageType, $"property_image_{imageId}");
        }

        // ✅ Update property & optionally add new images
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromForm] PropertyUploadDto dto)
        {
            var prop = await _ctx.Properties.Include(p => p.Images).FirstOrDefaultAsync(p => p.PropertyId == id);
            if (prop == null) return NotFound(new { Message = $"Property {id} not found" });

            prop.Title = dto.Title;
            prop.Description = dto.Description;
            prop.Address = dto.Address;
            prop.City = dto.City;
            prop.Province = dto.Province;
            prop.Country = dto.Country;
            prop.RentAmount = dto.RentAmount;
            prop.NumOfBedrooms = dto.NumOfBedrooms;
            prop.NumOfBathrooms = dto.NumOfBathrooms;
            prop.ParkingType = dto.ParkingType;
            prop.NumOfParkingSpots = dto.NumOfParkingSpots;
            prop.PetFriendly = dto.PetFriendly;
            prop.Status = dto.Status;

            if (dto.Images != null && dto.Images.Count > 0)
            {
                foreach (var img in dto.Images)
                {
                    using var ms = new MemoryStream();
                    await img.CopyToAsync(ms);
                    prop.Images.Add(new PropertyImage
                    {
                        ImageData = ms.ToArray(),
                        ImageType = img.ContentType ?? "image/jpeg"
                    });
                }
            }

            await _ctx.SaveChangesAsync();
            return Ok(new { Message = $"Property {id} updated successfully" });
        }

        // ✅ Delete property and its images
        [Authorize(Roles = "Admin,PropertyManager")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var prop = await _ctx.Properties.Include(p => p.Images).FirstOrDefaultAsync(p => p.PropertyId == id);
            if (prop == null) return NotFound(new { Message = $"Property {id} not found" });

            if (prop.Images != null)
                _ctx.PropertyImages.RemoveRange(prop.Images);

            _ctx.Properties.Remove(prop);
            await _ctx.SaveChangesAsync();

            return Ok(new { Message = $"Property {id} deleted successfully" });
        }
    }
}
