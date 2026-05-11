using SalaFinder.DTOs.Reservation;
using SalaFinder.Models;

namespace SalaFinder.Interfaces
{
    public interface IReservationService
    {
        Task<List<ReservationResponseDto>> GetAllAsync(string? userId = null, ReservationStatus? status = null);
        Task<ReservationResponseDto?> GetByIdAsync(int id);
        Task<ConflictInfoDto> CheckConflictAsync(int spaceId, DateTime date, TimeSpan start, TimeSpan end, int? excludeId = null);
        Task<ReservationResponseDto> CreateAsync(CreateReservationDto dto, string userId);
        Task<ReservationResponseDto?> UpdateStatusAsync(int id, UpdateReservationStatusDto dto, string adminUserId);
        Task<bool> CancelAsync(int id, string userId);
        Task<bool> MarkNoShowAsync(int id, string adminUserId);
        Task<List<AuditLogResponseDto>> GetAuditLogsAsync(int? reservationId = null);
        Task<bool> CheckAndUnblockUsersAsync();
    }
}
