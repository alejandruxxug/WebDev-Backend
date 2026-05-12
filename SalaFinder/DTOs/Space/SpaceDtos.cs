using System.ComponentModel.DataAnnotations;

namespace SalaFinder.DTOs.Space
{
    public class CreateSpaceDto
    {
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Type { get; set; } = string.Empty;

        [Required]
        [Range(1, 1000)]
        public int Capacity { get; set; }

        [Required]
        [StringLength(100)]
        public string Building { get; set; } = string.Empty;

        public string Resources { get; set; } = string.Empty;

        public string AllowedPrograms { get; set; } = string.Empty;

        public bool RequiresApproval { get; set; } = false;
    }

    public class UpdateSpaceDto
    {
        [StringLength(100, MinimumLength = 2)]
        public string? Name { get; set; }

        [StringLength(50)]
        public string? Type { get; set; }

        [Range(1, 1000)]
        public int? Capacity { get; set; }

        [StringLength(100)]
        public string? Building { get; set; }

        public string? Resources { get; set; }
        public string? AllowedPrograms { get; set; }
        public bool? RequiresApproval { get; set; }
        public bool? IsActive { get; set; }
    }

    public class SpaceResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int Capacity { get; set; }
        public string Building { get; set; } = string.Empty;
        public List<string> Resources { get; set; } = new();
        public List<string> AllowedPrograms { get; set; } = new();
        public bool RequiresApproval { get; set; }
        public bool IsActive { get; set; }
    }

    public class SpaceFilterDto
    {
        public string? Type { get; set; }
        public int? MinCapacity { get; set; }
        public string? Building { get; set; }
        public string? Resource { get; set; }
        public DateTime? Date { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
    }

    public class AvailabilitySlotDto
    {
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public bool IsAvailable { get; set; }
    }

    public class SpaceAvailabilityDto
    {
        public SpaceResponseDto Space { get; set; } = null!;
        public List<AvailabilitySlotDto> Slots { get; set; } = new();
    }
}
