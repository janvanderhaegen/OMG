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

    public async Task<IReadOnlyList<Garden>> ListByUserAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        var entities = await dbContext.Gardens
            .AsNoTracking()
            .Where(x => x.UserId == userId.Value)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(Garden garden, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(garden);
        await dbContext.Gardens.AddAsync(entity, cancellationToken).ConfigureAwait(false);
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

    private static Garden MapToDomain(GardenEntity entity) =>
        Garden.FromPersistence(
            new GardenId(entity.Id),
            new UserId(entity.UserId),
            entity.Name,
            entity.TotalSurfaceArea,
            entity.TargetHumidityLevel,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.Deleted,
            entity.DeletedAt);

    private static GardenEntity MapToEntity(Garden garden)
    {
        return new GardenEntity
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
    }
}

