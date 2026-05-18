using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalaFinder.DTOs.Reservation;
using SalaFinder.Interfaces;
using SalaFinder.Models;
using System.Security.Claims;

namespace SalaFinder.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReservationsController : ControllerBase
    {
        private readonly IReservationService _reservationService;

        public ReservationsController(IReservationService reservationService)
        {
            _reservationService = reservationService;
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? userId = null,
            [FromQuery] ReservationStatus? status = null)
        {
            var reservations = await _reservationService.GetAllAsync(userId, status);
            return Ok(reservations);
        }

        [HttpGet("my")]
        public async Task<IActionResult> GetMy([FromQuery] ReservationStatus? status = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var reservations = await _reservationService.GetAllAsync(userId, status);
            return Ok(reservations);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var reservation = await _reservationService.GetByIdAsync(id);
            return reservation == null
                ? NotFound(new { message = $"La reserva con ID {id} no existe" })
                : Ok(reservation);
        }

        [HttpGet("check-conflict")]
        public async Task<IActionResult> CheckConflict(
            [FromQuery] Guid spaceId,
            [FromQuery] DateTime date,
            [FromQuery] TimeSpan startTime,
            [FromQuery] TimeSpan endTime)
        {
            var result = await _reservationService.CheckConflictAsync(spaceId, date, startTime, endTime);
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateReservationDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            try
            {
                var created = await _reservationService.CreateAsync(dto, userId);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpPatch("{id}/status")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateReservationStatusDto dto)
        {
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            try
            {
                var updated = await _reservationService.UpdateStatusAsync(id, dto, adminId);
                return updated == null ? NotFound(new { message = $"La reserva con ID {id} no existe" }) : Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Cancel(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            try
            {
                var cancelled = await _reservationService.CancelAsync(id, userId);
                return cancelled ? NoContent() : NotFound(new { message = $"La reserva con ID {id} no existe" });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("{id}/no-show")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> MarkNoShow(Guid id)
        {
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            try
            {
                var marked = await _reservationService.MarkNoShowAsync(id, adminId);
                return marked
                    ? Ok(new { message = "No-show registrado. Si el usuario acumula 2 no-shows, será bloqueado." })
                    : NotFound(new { message = $"La reserva con ID {id} no existe" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("audit")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAuditLogs([FromQuery] Guid? reservationId = null)
        {
            var logs = await _reservationService.GetAuditLogsAsync(reservationId);
            return Ok(logs);
        }

        [HttpPost("unblock-users")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UnblockExpiredUsers()
        {
            var anyUnblocked = await _reservationService.CheckAndUnblockUsersAsync();
            return Ok(new { message = anyUnblocked ? "Usuarios desbloqueados correctamente." : "No hay usuarios bloquiados." });
        }
    }
}
