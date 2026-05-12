using Microsoft.EntityFrameworkCore;
using SalaFinder.Data;
using SalaFinder.DTOs.Space;
using SalaFinder.Interfaces;
using SalaFinder.Models;

namespace SalaFinder.Services
{
    public class SpaceService : ISpaceService
    {
        private readonly ApplicationDbContext _context;

        public SpaceService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<SpaceResponseDto>> GetAllAsync(SpaceFilterDto? filter = null)
        {
            var query = _context.Spaces.Where(s => s.IsActive).AsQueryable();

            if (filter != null)
            {
                if (!string.IsNullOrEmpty(filter.Type))
                    query = query.Where(s => s.Type == filter.Type);

                if (filter.MinCapacity.HasValue)
                    query = query.Where(s => s.Capacity >= filter.MinCapacity.Value);

                if (!string.IsNullOrEmpty(filter.Building))
                    query = query.Where(s => s.Building.Contains(filter.Building));

                if (!string.IsNullOrEmpty(filter.Resource))
                    query = query.Where(s => s.Resources.Contains(filter.Resource));
            }

            var spaces = await query.ToListAsync();
            return spaces.Select(MapToDto).ToList();
        }

        public async Task<SpaceResponseDto?> GetByIdAsync(Guid id)
        {
            var space = await _context.Spaces.FindAsync(id);
            return space == null ? null : MapToDto(space);
        }

        public async Task<SpaceResponseDto> CreateAsync(CreateSpaceDto dto)
        {
            var existing = await _context.Spaces
                .AnyAsync(s => s.Name == dto.Name && s.Building == dto.Building && s.IsActive);

            if (existing)
                throw new InvalidOperationException($"Ya existe un espacio llamado '{dto.Name}' en {dto.Building}.");

            var space = new Space
            {
                Name = dto.Name,
                Type = dto.Type,
                Capacity = dto.Capacity,
                Building = dto.Building,
                Resources = dto.Resources,
                AllowedPrograms = dto.AllowedPrograms,
                RequiresApproval = dto.RequiresApproval,
                IsActive = true
            };

            _context.Spaces.Add(space);
            await _context.SaveChangesAsync();
            return MapToDto(space);
        }

        public async Task<SpaceResponseDto?> UpdateAsync(Guid id, UpdateSpaceDto dto)
        {
            var space = await _context.Spaces.FindAsync(id);
            if (space == null) return null;

            if (dto.Name != null) space.Name = dto.Name;
            if (dto.Type != null) space.Type = dto.Type;
            if (dto.Capacity.HasValue) space.Capacity = dto.Capacity.Value;
            if (dto.Building != null) space.Building = dto.Building;
            if (dto.Resources != null) space.Resources = dto.Resources;
            if (dto.AllowedPrograms != null) space.AllowedPrograms = dto.AllowedPrograms;
            if (dto.RequiresApproval.HasValue) space.RequiresApproval = dto.RequiresApproval.Value;
            if (dto.IsActive.HasValue) space.IsActive = dto.IsActive.Value;

            await _context.SaveChangesAsync();
            return MapToDto(space);
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var space = await _context.Spaces.FindAsync(id);
            if (space == null) return false;

            space.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<SpaceAvailabilityDto?> GetWeekAvailabilityAsync(Guid spaceId, DateTime weekStart)
        {
            var space = await _context.Spaces.FindAsync(spaceId);
            if (space == null) return null;

            var weekEnd = weekStart.AddDays(7);

            var reservations = await _context.Reservations
                .Where(r => r.SpaceId == spaceId
                    && r.Date >= weekStart
                    && r.Date < weekEnd
                    && (r.Status == ReservationStatus.Approved || r.Status == ReservationStatus.Pending))
                .ToListAsync();

            var slots = new List<AvailabilitySlotDto>();
            var openTime = new TimeSpan(7, 0, 0);
            var closeTime = new TimeSpan(22, 0, 0);
            var slotDuration = TimeSpan.FromHours(1);

            for (var current = openTime; current < closeTime; current = current.Add(slotDuration))
            {
                var slotEnd = current.Add(slotDuration);
                var isOccupied = reservations.Any(r =>
                    r.StartTime < slotEnd && r.EndTime > current);

                slots.Add(new AvailabilitySlotDto
                {
                    StartTime = current,
                    EndTime = slotEnd,
                    IsAvailable = !isOccupied
                });
            }

            return new SpaceAvailabilityDto
            {
                Space = MapToDto(space),
                Slots = slots
            };
        }

        public async Task<List<SpaceResponseDto>> GetAvailableSpacesAsync(SpaceFilterDto filter)
        {
            if (!filter.Date.HasValue || !filter.StartTime.HasValue || !filter.EndTime.HasValue)
                return await GetAllAsync(filter);

            var allSpaces = await GetAllAsync(filter);

            var reservedSpaceIds = await _context.Reservations
                .Where(r => r.Date.Date == filter.Date.Value.Date
                    && r.StartTime < filter.EndTime.Value
                    && r.EndTime > filter.StartTime.Value
                    && (r.Status == ReservationStatus.Approved || r.Status == ReservationStatus.Pending))
                .Select(r => r.SpaceId)
                .ToListAsync();

            return allSpaces.Where(s => !reservedSpaceIds.Contains(s.Id)).ToList();
        }

        private static SpaceResponseDto MapToDto(Space space) => new()
        {
            Id = space.Id,
            Name = space.Name,
            Type = space.Type,
            Capacity = space.Capacity,
            Building = space.Building,
            Resources = string.IsNullOrEmpty(space.Resources)
                ? new List<string>()
                : space.Resources.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
            AllowedPrograms = string.IsNullOrEmpty(space.AllowedPrograms)
                ? new List<string>()
                : space.AllowedPrograms.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
            RequiresApproval = space.RequiresApproval,
            IsActive = space.IsActive
        };
    }
}