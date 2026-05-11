using Microsoft.EntityFrameworkCore;
using SalaFinder.Data;
using SalaFinder.DTOs.Reservation;
using SalaFinder.Interfaces;
using SalaFinder.Models;

namespace SalaFinder.Services
{
    public class ReservationService : IReservationService
    {
        private readonly ApplicationDbContext _context;

        public ReservationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<ReservationResponseDto>> GetAllAsync(string? userId = null, ReservationStatus? status = null)
        {
            var query = _context.Reservations
                .Include(r => r.Space)
                .Include(r => r.User)
                .Include(r => r.ApprovedBy)
                .AsQueryable();

            if (!string.IsNullOrEmpty(userId))
                query = query.Where(r => r.UserId == userId);

            if (status.HasValue)
                query = query.Where(r => r.Status == status.Value);

            var reservations = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
            return reservations.Select(MapToDto).ToList();
        }

        public async Task<ReservationResponseDto?> GetByIdAsync(int id)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Space)
                .Include(r => r.User)
                .Include(r => r.ApprovedBy)
                .FirstOrDefaultAsync(r => r.Id == id);

            return reservation == null ? null : MapToDto(reservation);
        }

        public async Task<ConflictInfoDto> CheckConflictAsync(int spaceId, DateTime date, TimeSpan start, TimeSpan end, int? excludeId = null)
        {
            var query = _context.Reservations
                .Include(r => r.Space)
                .Include(r => r.User)
                .Where(r => r.SpaceId == spaceId
                    && r.Date.Date == date.Date
                    && r.StartTime < end
                    && r.EndTime > start
                    && (r.Status == ReservationStatus.Approved || r.Status == ReservationStatus.Pending));

            if (excludeId.HasValue)
                query = query.Where(r => r.Id != excludeId.Value);

            var conflicts = await query.ToListAsync();

            if (conflicts.Count == 0)
                return new ConflictInfoDto { HasConflict = false };

            var alternatives = await FindAlternativeSlotsAsync(spaceId, date, end - start);

            return new ConflictInfoDto
            {
                HasConflict = true,
                ConflictingReservations = conflicts.Select(MapToDto).ToList(),
                AlternativeSlots = alternatives
            };
        }

        public async Task<ReservationResponseDto> CreateAsync(CreateReservationDto dto, string userId)
        {
            var user = await _context.Users.FindAsync(userId)
                ?? throw new KeyNotFoundException("Usuario no encontrado.");

            if (user.IsBlocked && user.BlockedUntil.HasValue && user.BlockedUntil.Value > DateTime.UtcNow)
                throw new InvalidOperationException(
                    $"Estás bloqueado para hacer reservas hasta {user.BlockedUntil.Value:dd/MM/yyyy HH:mm} por cometer 2 no-shows.");

            if (dto.StartTime >= dto.EndTime)
                throw new ArgumentException("La hora de inicio debe ser anterior a la hora de fin.");

            if (dto.Date.Date < DateTime.Today)
                throw new ArgumentException("No se pueden crear reservas en fechas pasadas.");

            var space = await _context.Spaces.FindAsync(dto.SpaceId)
                ?? throw new KeyNotFoundException("Espacio no encontrado.");

            if (!space.IsActive)
                throw new InvalidOperationException("Este espacio no está disponible.");

            if (dto.AttendeeCount > space.Capacity)
                throw new InvalidOperationException(
                    $"El número de asistentes ({dto.AttendeeCount}) excede la capacidad del espacio ({space.Capacity}).");

            var conflictInfo = await CheckConflictAsync(dto.SpaceId, dto.Date, dto.StartTime, dto.EndTime);
            if (conflictInfo.HasConflict)
                throw new InvalidOperationException(
                    "El espacio no está disponible en ese horario.");

            var status = space.RequiresApproval
                ? ReservationStatus.Pending
                : ReservationStatus.Approved;

            var reservation = new Reservation
            {
                SpaceId = dto.SpaceId,
                UserId = userId,
                Date = dto.Date.Date,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                Purpose = dto.Purpose,
                AttendeeCount = dto.AttendeeCount,
                Status = status,
                CreatedAt = DateTime.UtcNow
            };

            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            await LogAuditAsync(
                reservationId: reservation.Id,
                userId: userId,
                action: "CREATED",
                details: $"Reserva creada para {space.Name} el {dto.Date:dd/MM/yyyy} de {dto.StartTime} a {dto.EndTime}",
                previousStatus: "",
                newStatus: status.ToString()
            );

            return MapToDto(await _context.Reservations
                .Include(r => r.Space)
                .Include(r => r.User)
                .FirstAsync(r => r.Id == reservation.Id));
        }

        public async Task<ReservationResponseDto?> UpdateStatusAsync(int id, UpdateReservationStatusDto dto, string adminUserId)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Space)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null) return null;

            var previousStatus = reservation.Status.ToString();

            if (dto.NewStatus == ReservationStatus.Approved)
            {
                var conflict = await CheckConflictAsync(
                    reservation.SpaceId, reservation.Date,
                    reservation.StartTime, reservation.EndTime, excludeId: id);

                if (conflict.HasConflict)
                    throw new InvalidOperationException(
                        "Existe un conflicto de horario con otra reserva aprobada.");

                reservation.ApprovedByUserId = adminUserId;
                reservation.ApprovedAt = DateTime.UtcNow;
            }

            if (dto.NewStatus == ReservationStatus.Rejected)
                reservation.RejectionReason = dto.Reason;

            reservation.Status = dto.NewStatus;
            await _context.SaveChangesAsync();

            await LogAuditAsync(
                reservationId: reservation.Id,
                userId: adminUserId,
                action: "STATUS_CHANGED",
                details: dto.Reason ?? $"Estado cambiado a {dto.NewStatus}",
                previousStatus: previousStatus,
                newStatus: dto.NewStatus.ToString()
            );

            return MapToDto(reservation);
        }

        public async Task<bool> CancelAsync(int id, string userId)
        {
            var reservation = await _context.Reservations
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null) return false;

            if (reservation.UserId != userId)
                throw new UnauthorizedAccessException("Solo puedes cancelar tus propias reservas.");

            if (reservation.Status == ReservationStatus.Cancelled)
                throw new InvalidOperationException("Esta reserva ya está cancelada.");

            var previousStatus = reservation.Status.ToString();
            reservation.Status = ReservationStatus.Cancelled;
            await _context.SaveChangesAsync();

            await LogAuditAsync(
                reservationId: reservation.Id,
                userId: userId,
                action: "CANCELLED",
                details: "Reserva cancelada por el usuario",
                previousStatus: previousStatus,
                newStatus: "Cancelled"
            );

            return true;
        }

        public async Task<bool> MarkNoShowAsync(int id, string adminUserId)
        {
            var reservation = await _context.Reservations
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null) return false;
            if (reservation.Status != ReservationStatus.Approved)
                throw new InvalidOperationException("Solo se puede marcar no-show en reservas aprobadas.");

            var previousStatus = reservation.Status.ToString();
            reservation.Status = ReservationStatus.NoShow;

            var user = reservation.User;
            user.NoShowCount++;

            if (user.NoShowCount >= 2)
            {
                user.IsBlocked = true;
                user.BlockedUntil = DateTime.UtcNow.AddDays(7);
            }

            await _context.SaveChangesAsync();

            await LogAuditAsync(
                reservationId: reservation.Id,
                userId: adminUserId,
                action: "NO_SHOW",
                details: $"No-show registrado. Total no-shows del usuario: {user.NoShowCount}. " +
                         (user.IsBlocked ? $"Usuario bloqueado hasta {user.BlockedUntil:dd/MM/yyyy}" : ""),
                previousStatus: previousStatus,
                newStatus: "NoShow"
            );

            return true;
        }

        public async Task<List<AuditLogResponseDto>> GetAuditLogsAsync(int? reservationId = null)
        {
            var query = _context.AuditLogs
                .Include(a => a.User)
                .AsQueryable();

            if (reservationId.HasValue)
                query = query.Where(a => a.ReservationId == reservationId.Value);

            var logs = await query.OrderByDescending(a => a.Timestamp).ToListAsync();

            return logs.Select(a => new AuditLogResponseDto
            {
                Id = a.Id,
                ReservationId = a.ReservationId,
                UserFullName = a.User?.FullName ?? "Sistema",
                Action = a.Action,
                Details = a.Details,
                Timestamp = a.Timestamp,
                PreviousStatus = a.PreviousStatus,
                NewStatus = a.NewStatus
            }).ToList();
        }

        public async Task<bool> CheckAndUnblockUsersAsync()
        {
            var blockedUsers = await _context.Users
                .Where(u => u.IsBlocked && u.BlockedUntil.HasValue && u.BlockedUntil.Value <= DateTime.UtcNow)
                .ToListAsync();

            foreach (var user in blockedUsers)
            {
                user.IsBlocked = false;
                user.BlockedUntil = null;

                await LogAuditAsync(
                    reservationId: null,
                    userId: user.Id,
                    action: "USER_UNBLOCKED",
                    details: "Usuario desbloqueado.",
                    previousStatus: "Blocked",
                    newStatus: "Active"
                );
            }

            if (blockedUsers.Count > 0)
                await _context.SaveChangesAsync();

            return blockedUsers.Count > 0;
        }

        private async Task<List<AlternativeSlotDto>> FindAlternativeSlotsAsync(int spaceId, DateTime date, TimeSpan duration)
        {
            var alternatives = new List<AlternativeSlotDto>();
            var space = await _context.Spaces.FindAsync(spaceId);
            if (space == null) return alternatives;

            var reservations = await _context.Reservations
                .Where(r => r.SpaceId == spaceId
                    && r.Date.Date == date.Date
                    && (r.Status == ReservationStatus.Approved || r.Status == ReservationStatus.Pending))
                .OrderBy(r => r.StartTime)
                .ToListAsync();

            var openTime = new TimeSpan(7, 0, 0);
            var closeTime = new TimeSpan(22, 0, 0);
            var current = openTime;

            while (current + duration <= closeTime && alternatives.Count < 3)
            {
                var slotEnd = current + duration;
                var hasConflict = reservations.Any(r => r.StartTime < slotEnd && r.EndTime > current);

                if (!hasConflict)
                {
                    alternatives.Add(new AlternativeSlotDto
                    {
                        Date = date,
                        StartTime = current,
                        EndTime = slotEnd,
                        SpaceName = space.Name,
                        SpaceId = spaceId
                    });
                }

                current = current.Add(TimeSpan.FromMinutes(30));
            }

            return alternatives;
        }

        private async Task LogAuditAsync(int? reservationId, string userId, string action, string details, string previousStatus, string newStatus)
        {
            var log = new AuditLog
            {
                ReservationId = reservationId,
                UserId = userId,
                Action = action,
                Details = details,
                PreviousStatus = previousStatus,
                NewStatus = newStatus,
                Timestamp = DateTime.UtcNow
            };

            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        private static ReservationResponseDto MapToDto(Reservation r) => new()
        {
            Id = r.Id,
            SpaceId = r.SpaceId,
            SpaceName = r.Space?.Name ?? "",
            SpaceBuilding = r.Space?.Building ?? "",
            UserId = r.UserId,
            UserFullName = r.User?.FullName ?? "",
            Date = r.Date,
            StartTime = r.StartTime,
            EndTime = r.EndTime,
            Purpose = r.Purpose,
            AttendeeCount = r.AttendeeCount,
            Status = r.Status.ToString(),
            CreatedAt = r.CreatedAt,
            ApprovedByName = r.ApprovedBy?.FullName,
            ApprovedAt = r.ApprovedAt,
            RejectionReason = r.RejectionReason
        };
    }
}
