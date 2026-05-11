using Microsoft.AspNetCore.Identity;

namespace SalaFinder.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public string Program { get; set; } = string.Empty;
        public bool IsBlocked { get; set; } = false;
        public DateTime? BlockedUntil { get; set; }
        public int NoShowCount { get; set; } = 0;
        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
        public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    }
}
