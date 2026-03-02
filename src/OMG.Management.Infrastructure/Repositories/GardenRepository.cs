using Microsoft.EntityFrameworkCore;
using OMG.Management.Domain.Gardens;
using OMG.Management.Infrastructure.Entities;

namespace OMG.Management.Infrastructure.Repositories;

public sealed class GardenRepository(ManagementDbContext dbContext) : IGardenRepository
{
    public async Task<Garden?> GetByIdAsync(GardenId id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Gardens
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<Garden?> GetByIdWithPlantsAsync(GardenId id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Gardens
            .Include(g => g.Plants)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return null;
        }

        var plants = entity.Plants.Select(MapToDomain).ToList();
        return MapToDomain(entity, plants);
    }

    public async Task<IReadOnlyList<Garden>> ListByUserAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        var entities = await dbContext.Gardens
            .AsNoTracking()
            .Where(x => x.UserId == userId.Value)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(entity => MapToDomain(entity)).ToList();
    }

    public async Task AddAsync(Garden garden, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(garden);
        await dbContext.Gardens.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsync(Garden garden, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Gardens
            .Include(g => g.Plants)
            .FirstOrDefaultAsync(x => x.Id == garden.Id.Value, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            entity = MapToEntity(garden);
            await dbContext.Gardens.AddAsync(entity, cancellationToken).ConfigureAwait(false);
            return;
        }

        entity.UserId = garden.UserId.Value;
        entity.Name = garden.Name;
        entity.TotalSurfaceArea = garden.TotalSurfaceArea.Value;
        entity.TargetHumidityLevel = garden.TargetHumidityLevel.Value;
        entity.CreatedAt = garden.CreatedAt;
        entity.UpdatedAt = garden.UpdatedAt;
        entity.Deleted = garden.IsDeleted;
        entity.DeletedAt = garden.DeletedAt;

        entity.Plants.Clear();

        foreach (var plant in garden.Plants)
        {
            entity.Plants.Add(MapToEntity(plant, garden.Id.Value));
        }
    }

    public void Remove(Garden garden)
    {
        var entity = new GardenEntity
        {
            Id = garden.Id.Value,
            Deleted = garden.IsDeleted,
            DeletedAt = garden.DeletedAt
        };

        dbContext.Attach(entity);
        dbContext.Entry(entity).Property(x => x.Deleted).IsModified = true;
        dbContext.Entry(entity).Property(x => x.DeletedAt).IsModified = true;
    }

    private static Garden MapToDomain(GardenEntity entity, IEnumerable<Plant>? plants = null) =>
        Garden.FromPersistence(
            new GardenId(entity.Id),
            new UserId(entity.UserId),
            entity.Name,
            entity.TotalSurfaceArea,
            entity.TargetHumidityLevel,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.Deleted,
            entity.DeletedAt,
            plants);

    private static Plant MapToDomain(PlantEntity entity)
    {
        var type = Enum.TryParse<PlantType>(entity.Type, ignoreCase: true, out var parsed)
            ? parsed
            : PlantType.Vegetable;

        return new Plant(
            new PlantId(entity.Id),
            entity.Name,
            entity.Species,
            type,
            entity.PlantationDate,
            new SurfaceArea(entity.SurfaceAreaRequired),
            new HumidityLevel(entity.IdealHumidityLevel));
    }

    private static GardenEntity MapToEntity(Garden garden)
    {
        var entity = new GardenEntity
        {
            Id = garden.Id.Value,
            UserId = garden.UserId.Value,
            Name = garden.Name,
            TotalSurfaceArea = garden.TotalSurfaceArea.Value,
            TargetHumidityLevel = garden.TargetHumidityLevel.Value,
            CreatedAt = garden.CreatedAt,
            UpdatedAt = garden.UpdatedAt,
            Deleted = garden.IsDeleted,
            DeletedAt = garden.DeletedAt
        };

        foreach (var plant in garden.Plants)
        {
            entity.Plants.Add(MapToEntity(plant, garden.Id.Value));
        }

        return entity;
    }

    private static PlantEntity MapToEntity(Plant plant, Guid gardenId)
    {
        return new PlantEntity
        {
            Id = plant.Id.Value,
            GardenId = gardenId,
            Name = plant.Name,
            Species = plant.Species,
            Type = plant.Type.ToString(),
            PlantationDate = plant.PlantationDate,
            SurfaceAreaRequired = plant.SurfaceAreaRequired.Value,
            IdealHumidityLevel = plant.IdealHumidityLevel.Value
        };
    }
}

