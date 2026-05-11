using System.ComponentModel.DataAnnotations;

namespace SalaFinder.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        public int? ReservationId { get; set; }
        public Reservation? Reservation { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string Action { get; set; } = string.Empty;

        [StringLength(500)]
        public string Details { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public string PreviousStatus { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
    }
}
