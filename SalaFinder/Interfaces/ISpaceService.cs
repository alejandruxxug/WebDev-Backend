using SalaFinder.DTOs.Space;
using SalaFinder.Models;

namespace SalaFinder.Interfaces
{
    public interface ISpaceService
    {
        Task<List<SpaceResponseDto>> GetAllAsync(SpaceFilterDto? filter = null);
        Task<SpaceResponseDto?> GetByIdAsync(int id);
        Task<SpaceResponseDto> CreateAsync(CreateSpaceDto dto);
        Task<SpaceResponseDto?> UpdateAsync(int id, UpdateSpaceDto dto);
        Task<bool> DeleteAsync(int id);
        Task<SpaceAvailabilityDto?> GetWeekAvailabilityAsync(int spaceId, DateTime weekStart);
        Task<List<SpaceResponseDto>> GetAvailableSpacesAsync(SpaceFilterDto filter);
    }
}
