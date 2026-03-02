using Microsoft.EntityFrameworkCore;
using OMG.Telemetrics.Domain.Hydration;
using OMG.Telemetrics.Infrastructure.Entities;

namespace OMG.Telemetrics.Infrastructure;

public sealed class PlantTelemetryRepository(TelemetryDbContext dbContext) : IPlantHydrationStateRepository
{
    public async Task<PlantHydrationState?> GetByPlantIdAsync(Guid plantId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Plants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PlantId == plantId, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null) return null;

        var activeSession = await GetActiveSessionForPlantAsync(entity.PlantId, cancellationToken).ConfigureAwait(false);
        return MapToDomain(entity, activeSession);
    }

    public async Task<PlantHydrationState?> GetByMeterIdAsync(Guid gardenId, string meterId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Plants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.GardenId == gardenId && x.MeterId == meterId, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null) return null;

        var activeSession = await GetActiveSessionForPlantAsync(entity.PlantId, cancellationToken).ConfigureAwait(false);
        return MapToDomain(entity, activeSession);
    }

    public async Task<IReadOnlyList<(PlantHydrationState State, string? MeterId, Guid GardenId)>> ListWithIrrigationLineAsync(CancellationToken cancellationToken = default)
    {
        var entities = await dbContext.Plants
            .AsNoTracking()
            .Where(x => x.HasIrrigationLine && x.MeterId != null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = new List<(PlantHydrationState State, string? MeterId, Guid GardenId)>();

        foreach (var e in entities)
        {
            var activeSession = await GetActiveSessionForPlantAsync(e.PlantId, cancellationToken).ConfigureAwait(false);
            result.Add((MapToDomain(e, activeSession), e.MeterId, e.GardenId));
        }

        return result;
    }

    public async Task AddAsync(PlantHydrationState state, string? meterId, Guid gardenId, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(state, meterId, gardenId);
        await dbContext.Plants.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await SyncWateringSessionAsync(state, cancellationToken).ConfigureAwait(false);
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
            entity.LastTelemetryAt = state.LastIrrigationEnd ?? state.LastIrrigationStart ?? DateTimeOffset.UtcNow;
        }

        await SyncWateringSessionAsync(state, cancellationToken).ConfigureAwait(false);
    }

    private async Task<WateringSession?> GetActiveSessionForPlantAsync(Guid plantId, CancellationToken cancellationToken)
    {
        var sessionEntity = await dbContext.WateringSessions
            .AsNoTracking()
            .Where(s => s.PlantId == plantId && s.IsActive)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (sessionEntity is null) return null;

        var endsAt = sessionEntity.StartedAt.AddMinutes(HydrationConstants.WateringDurationMinutes);
        return new WateringSession(
            sessionEntity.SessionId,
            sessionEntity.PlantId,
            sessionEntity.StartedAt,
            endsAt);
    }

    private async Task SyncWateringSessionAsync(PlantHydrationState state, CancellationToken cancellationToken)
    {
        if (state.ActiveSession is not null)
        {
            var existing = await dbContext.WateringSessions
                .FirstOrDefaultAsync(s => s.SessionId == state.ActiveSession.SessionId, cancellationToken)
                .ConfigureAwait(false);

            if (existing is null)
            {
                dbContext.WateringSessions.Add(new WateringSessionEntity
                {
                    SessionId = state.ActiveSession.SessionId,
                    PlantId = state.ActiveSession.PlantId,
                    StartedAt = state.ActiveSession.StartedAt,
                    EndedAt = null,
                    IsActive = true
                });
            }
        }
        else
        {
            var activeEntities = await dbContext.WateringSessions
                .Where(s => s.PlantId == state.PlantId && s.IsActive)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var se in activeEntities)
            {
                se.IsActive = false;
                se.EndedAt = state.LastIrrigationEnd ?? DateTimeOffset.UtcNow;
            }
        }
    }

    private static PlantHydrationState MapToDomain(TelemetryPlantEntity entity, WateringSession? activeSession)
    {
        var isWatering = activeSession is not null;
        return PlantHydrationState.FromPersistence(
            entity.PlantId,
            entity.MeterId!,
            PlantType.Vegetable,
            entity.IdealHumidityLevel,
            entity.CurrentHumidityLevel,
            lastIrrigationStart: null,
            lastIrrigationEnd: null,
            isWatering,
            entity.HasIrrigationLine,
            activeSession);
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

