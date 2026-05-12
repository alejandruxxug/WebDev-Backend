using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalaFinder.Models
{
    public class Space
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Type { get; set; } = string.Empty;

        [Range(1, 1000)]
        public int Capacity { get; set; }

        [Required]
        [StringLength(100)]
        public string Building { get; set; } = string.Empty;

        public string Resources { get; set; } = string.Empty;

        public string AllowedPrograms { get; set; } = string.Empty;

        public bool RequiresApproval { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    }
}
