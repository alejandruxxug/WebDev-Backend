using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalaFinder.DTOs.Space;
using SalaFinder.Interfaces;

namespace SalaFinder.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SpacesController : ControllerBase
    {
        private readonly ISpaceService _spaceService;

        public SpacesController(ISpaceService spaceService)
        {
            _spaceService = spaceService;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll([FromQuery] SpaceFilterDto filter)
        {
            var spaces = await _spaceService.GetAllAsync(filter);
            return Ok(spaces);
        }

        [HttpGet("available")]
        [Authorize]
        public async Task<IActionResult> GetAvailable([FromQuery] SpaceFilterDto filter)
        {
            var spaces = await _spaceService.GetAvailableSpacesAsync(filter);
            return Ok(spaces);
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(int id)
        {
            var space = await _spaceService.GetByIdAsync(id);
            return space == null ? NotFound(new { message = $"Espacio con ID {id} no encontrado." }) : Ok(space);
        }

        [HttpGet("{id}/availability")]
        [Authorize]
        public async Task<IActionResult> GetWeekAvailability(int id, [FromQuery] DateTime weekStart)
        {
            var availability = await _spaceService.GetWeekAvailabilityAsync(id, weekStart);
            return availability == null
                ? NotFound(new { message = "Espacio no encontrado." })
                : Ok(availability);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateSpaceDto dto)
        {
            try
            {
                var created = await _spaceService.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateSpaceDto dto)
        {
            var updated = await _spaceService.UpdateAsync(id, dto);
            return updated == null ? NotFound(new { message = $"Espacio con ID {id} no encontrado." }) : Ok(updated);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _spaceService.DeleteAsync(id);
            return deleted ? NoContent() : NotFound(new { message = $"Espacio con ID {id} no encontrado." });
        }
    }
}
