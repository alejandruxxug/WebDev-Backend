using System.ComponentModel.DataAnnotations;
using SalaFinder.Models;

namespace SalaFinder.DTOs.Reservation
{
    public class CreateReservationDto
    {
        [Required]
        public int SpaceId { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        [Required]
        [StringLength(300, MinimumLength = 10)]
        public string Purpose { get; set; } = string.Empty;

        [Required]
        [Range(1, 1000)]
        public int AttendeeCount { get; set; }
    }

    public class UpdateReservationStatusDto
    {
        [Required]
        public ReservationStatus NewStatus { get; set; }

        [StringLength(300)]
        public string? Reason { get; set; }
    }

    public class ReservationResponseDto
    {
        public int Id { get; set; }
        public int SpaceId { get; set; }
        public string SpaceName { get; set; } = string.Empty;
        public string SpaceBuilding { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserFullName { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Purpose { get; set; } = string.Empty;
        public int AttendeeCount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? ApprovedByName { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? RejectionReason { get; set; }
    }

    public class ConflictInfoDto
    {
        public bool HasConflict { get; set; }
        public List<ReservationResponseDto> ConflictingReservations { get; set; } = new();
        public List<AlternativeSlotDto> AlternativeSlots { get; set; } = new();
    }

    public class AlternativeSlotDto
    {
        public DateTime Date { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string SpaceName { get; set; } = string.Empty;
        public int SpaceId { get; set; }
    }

    public class AuditLogResponseDto
    {
        public int Id { get; set; }
        public int? ReservationId { get; set; }
        public string UserFullName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string PreviousStatus { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
    }
}
