using SalaFinder.DTOs.Space;
using SalaFinder.Models;

namespace SalaFinder.Interfaces
{
    public interface ISpaceService
    {
        Task<List<SpaceResponseDto>> GetAllAsync(SpaceFilterDto? filter = null);
        Task<SpaceResponseDto?> GetByIdAsync(Guid id);
        Task<SpaceResponseDto> CreateAsync(CreateSpaceDto dto);
        Task<SpaceResponseDto?> UpdateAsync(Guid id, UpdateSpaceDto dto);
        Task<bool> DeleteAsync(Guid id);
        Task<SpaceAvailabilityDto?> GetWeekAvailabilityAsync(Guid spaceId, DateTime weekStart);
        Task<List<SpaceResponseDto>> GetAvailableSpacesAsync(SpaceFilterDto filter);
    }
}
