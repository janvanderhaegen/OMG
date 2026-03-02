using Microsoft.EntityFrameworkCore;
using OMG.Telemetrics.Domain.Hydration;
using OMG.Telemetrics.Infrastructure;
using OMG.Telemetrics.Infrastructure.Entities;
using OMG.Telemetrics.Infrastructure.Mapping;

namespace OMG.Api.Telemetrics;

public sealed class IrrigationSimulationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IrrigationSimulationWorker> _logger;
    private readonly TimeSpan _tickInterval = TimeSpan.FromMinutes(1);

    public IrrigationSimulationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<IrrigationSimulationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Irrigation simulation worker started. Tick interval: {Interval}", _tickInterval);

        using var timer = new PeriodicTimer(_tickInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await RunTickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during irrigation simulation tick");
            }
        }
    }

    private async Task RunTickAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TelemetricsDbContext>();
        var mockAdapter = scope.ServiceProvider.GetRequiredService<IMockIrrigationSystemAdapter>();
        var now = DateTimeOffset.UtcNow;

        var stateEntities = await db.PlantHydrationStates
            .Where(s => s.HasIrrigationLine)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (stateEntities.Count == 0)
            return;

        var plantIds = stateEntities.Select(s => s.PlantId).ToList();
        var activeSessions = await db.WateringSessions
            .Where(w => plantIds.Contains(w.PlantId) && w.Status == nameof(WateringSessionStatus.Started))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var sessionByPlant = activeSessions.ToLookup(s => s.PlantId);

        foreach (var entity in stateEntities)
        {
            var activeSessionEntity = sessionByPlant[entity.PlantId].FirstOrDefault();
            var activeSession = activeSessionEntity != null ? HydrationStateMapping.ToDomain(activeSessionEntity) : null;
            var state = HydrationStateMapping.ToDomain(entity, activeSession);

            state.ApplyMinuteTick(now);

            var needsWatering = state.DomainEvents.OfType<PlantNeedsWateringDomainEvent>().Any();
            if (needsWatering)
            {
                var startResult = state.StartWatering(now);
                if (startResult.IsSuccess && startResult.Value is { } session)
                {
                    db.WateringSessions.Add(HydrationStateMapping.ToEntity(session));
                    mockAdapter.StartWatering(state.PlantId, state.GardenId);
                }
            }

            state.ClearDomainEvents();
            HydrationStateMapping.ApplyToEntity(state, entity);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dueSessions = await db.WateringSessions
            .Where(w => w.Status == nameof(WateringSessionStatus.Started) && w.EndsAt <= now)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var sessionEntity in dueSessions)
        {
            var stateEntity = await db.PlantHydrationStates
                .FirstOrDefaultAsync(s => s.PlantId == sessionEntity.PlantId, cancellationToken)
                .ConfigureAwait(false);

            if (stateEntity is null)
                continue;

            var session = HydrationStateMapping.ToDomain(sessionEntity);
            var state = HydrationStateMapping.ToDomain(stateEntity, session);
            var completeResult = state.CompleteWatering(now);

            if (completeResult.IsSuccess)
            {
                HydrationStateMapping.ApplyToEntity(state, stateEntity);
                sessionEntity.Status = nameof(WateringSessionStatus.Completed);
                mockAdapter.StopWatering(state.PlantId, state.GardenId);
            }
        }

        if (dueSessions.Count > 0)
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
