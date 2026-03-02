using Microsoft.EntityFrameworkCore;
using OMG.Telemetrics.Domain.Hydration;
using OMG.Telemetrics.Infrastructure.Entities;

namespace OMG.Telemetrics.Infrastructure;

public interface IPlantTelemetryRepository
{
    Task<PlantHydrationState?> GetByPlantIdAsync(Guid plantId, CancellationToken cancellationToken = default);

    Task<PlantHydrationState?> GetByMeterIdAsync(Guid gardenId, string meterId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(PlantHydrationState State, string? MeterId)>> ListWithIrrigationLineAsync(CancellationToken cancellationToken = default);

    Task AddAsync(PlantHydrationState state, string? meterId, Guid gardenId, CancellationToken cancellationToken = default);

    Task SaveAsync(PlantHydrationState state, string? meterId, Guid gardenId, CancellationToken cancellationToken = default);
}

public sealed class PlantTelemetryRepository(TelemetryDbContext dbContext) : IPlantTelemetryRepository
{
    public async Task<PlantHydrationState?> GetByPlantIdAsync(Guid plantId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Plants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PlantId == plantId, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<PlantHydrationState?> GetByMeterIdAsync(Guid gardenId, string meterId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Plants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.GardenId == gardenId && x.MeterId == meterId, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<(PlantHydrationState State, string? MeterId)>> ListWithIrrigationLineAsync(CancellationToken cancellationToken = default)
    {
        var entities = await dbContext.Plants
            .AsNoTracking()
            .Where(x => x.HasIrrigationLine && x.MeterId != null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities
            .Select(e => (MapToDomain(e), e.MeterId))
            .ToList();
    }

    public async Task AddAsync(PlantHydrationState state, string? meterId, Guid gardenId, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(state, meterId, gardenId);
        await dbContext.Plants.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsync(PlantHydrationState state, string? meterId, Guid gardenId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Plants
            .FirstOrDefaultAsync(x => x.PlantId == state.PlantId, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            entity = MapToEntity(state, meterId, gardenId);
            dbContext.Plants.Add(entity);
        }
        else
        {
            entity.GardenId = gardenId;
            entity.MeterId = meterId;
            entity.IdealHumidityLevel = state.IdealHumidityLevel;
            entity.CurrentHumidityLevel = state.CurrentHumidityLevel;
            entity.IsWatering = state.IsWatering;
            entity.HasIrrigationLine = state.HasIrrigationLine;
            entity.LastTelemetryAt = state.LastIrrigationEnd ?? state.LastIrrigationStart;
        }
    }

    private static PlantHydrationState MapToDomain(TelemetryPlantEntity entity)
    {
        // For now, default to Vegetable when reconstructing type; detailed type can be projected later.
        return PlantHydrationState.FromPersistence(
            entity.PlantId,
            entity.MeterId!,
            PlantType.Vegetable,
            entity.IdealHumidityLevel,
            entity.CurrentHumidityLevel,
            lastIrrigationStart: null,
            lastIrrigationEnd: null,
            isWatering: entity.IsWatering,
            hasIrrigationLine: entity.HasIrrigationLine,
            activeSession: null);
    }

    private static TelemetryPlantEntity MapToEntity(PlantHydrationState state, string? meterId, Guid gardenId)
    {
        return new TelemetryPlantEntity
        {
            PlantId = state.PlantId,
            GardenId = gardenId,
            MeterId = meterId,
            IdealHumidityLevel = state.IdealHumidityLevel,
            CurrentHumidityLevel = state.CurrentHumidityLevel,
            IsWatering = state.IsWatering,
            HasIrrigationLine = state.HasIrrigationLine,
            LastTelemetryAt = state.LastIrrigationEnd ?? state.LastIrrigationStart
        };
    }
}

