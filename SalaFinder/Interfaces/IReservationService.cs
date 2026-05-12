using SalaFinder.DTOs.Reservation;
using SalaFinder.Models;

namespace SalaFinder.Interfaces
{
    public interface IReservationService
    {
        Task<List<ReservationResponseDto>> GetAllAsync(string? userId = null, ReservationStatus? status = null);
        Task<ReservationResponseDto?> GetByIdAsync(Guid id);
        Task<ConflictInfoDto> CheckConflictAsync(Guid spaceId, DateTime date, TimeSpan start, TimeSpan end, Guid? excludeId = null);
        Task<ReservationResponseDto> CreateAsync(CreateReservationDto dto, string userId);
        Task<ReservationResponseDto?> UpdateStatusAsync(Guid id, UpdateReservationStatusDto dto, string adminUserId);
        Task<bool> CancelAsync(Guid id, string userId);
        Task<bool> MarkNoShowAsync(Guid id, string adminUserId);
        Task<List<AuditLogResponseDto>> GetAuditLogsAsync(Guid? reservationId = null);
        Task<bool> CheckAndUnblockUsersAsync();
    }
}
