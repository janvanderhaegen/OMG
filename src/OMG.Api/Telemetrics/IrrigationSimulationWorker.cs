using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OMG.Api.Management;
using OMG.Management.Infrastructure;
using OMG.Telemetrics.Domain.Hydration;
using OMG.Telemetrics.Infrastructure;
using PlantType = OMG.Telemetrics.Domain.Hydration.PlantType;

namespace OMG.Api.Telemetrics;

public sealed class IrrigationSimulationWorker(
    IServiceProvider serviceProvider,
    ILogger<IrrigationSimulationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SimulateAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Error while running irrigation simulation.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
        }
    }

    private static bool IsWateringFromSession(PlantHydrationState state, DateTimeOffset utcNow)
    {
        if (state.ActiveSession is null)
            return false;
        var session = state.ActiveSession;
        var startedWithinTwoMinutes = (utcNow - session.StartedAt).TotalMinutes < 2;
        var notEnded = utcNow < session.EndsAt;
        return startedWithinTwoMinutes && notEnded;
    }

    private async Task SimulateAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();

        var plantRepository = scope.ServiceProvider.GetRequiredService<IPlantHydrationStateRepository>();
        var managementDb = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();
        var telemetryDb = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        var eventPublisher = scope.ServiceProvider.GetRequiredService<ITelemetryIntegrationEventPublisher>();

        var plantsWithIrrigation = await plantRepository.ListWithIrrigationLineAsync(cancellationToken)
            .ConfigureAwait(false);

        if (plantsWithIrrigation.Count == 0)
        {
            return;
        }

        var utcNow = DateTimeOffset.UtcNow;
         

        var readingsByGarden = new Dictionary<Guid, List<TelemetryReadingRequest>>();

        foreach (var (state, meterId, gardenId) in plantsWithIrrigation)
        {
            if (string.IsNullOrEmpty(meterId))
                continue;

            var typed = await managementDb.Plants
                .AsNoTracking()
                .Where(p => p.Id == state.PlantId)
                .Select(c => c.Type)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            var type = Enum.TryParse<PlantType>(typed ?? "Vegetable", ignoreCase: true, out var parsed)
                ? parsed
                : PlantType.Vegetable;

            var current = state.CurrentHumidityLevel;
            var isWatering = IsWateringFromSession(state, utcNow);

            int next;

            if (isWatering)  
            {
                //currentHumidityLevel with 16% for vegetables, 18% for fruits and 20%
                var increase = type switch
                {
                    PlantType.Vegetable => 16,
                    PlantType.Fruit => 18,
                    PlantType.Flower => 20,
                    _ => 15
                };  
                // Simple increase when watering.
                next = Math.Min(100, current + increase);
            }
            else
            {
                //- Every minute a plant’s currentHumidityLevel drops: 1% for vegetables, 3% for fruits and 4% for flowers
                var decay = type switch
                {
                    PlantType.Vegetable => 1,
                    PlantType.Fruit => 3,
                    PlantType.Flower => 4,
                    _ => 2
                };  
                // Simple decay when not watering.
                next = Math.Max(0, current - decay);
            }

            if (!readingsByGarden.TryGetValue(gardenId, out var list))
            {
                list = [];
                readingsByGarden[gardenId] = list;
            }

            list.Add(new TelemetryReadingRequest(meterId, next, isWatering));
        }

        foreach (var (gardenId, readings) in readingsByGarden)
        {
            var garden = await managementDb.Gardens
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == gardenId, cancellationToken)
                .ConfigureAwait(false);

            if (garden is null)
            {
                continue;
            }

            var validationErrors = await TelemetryEndpoints.ProcessReadingsForGardenAsync(
                    managementDb,
                    telemetryDb,
                    plantRepository,
                    eventPublisher,
                    new PublishingUnitOfWork(managementDb),
                    publishEndpoint,
                    garden,
                    readings,
                    cancellationToken)
                .ConfigureAwait(false);

            if (validationErrors is not null && validationErrors.Count > 0)
            {
                logger.LogWarning(
                    "Telemetry ingestion validation failed for garden {GardenId}: {Errors}",
                    gardenId,
                    string.Join("; ", validationErrors.Select(kvp => $"{kvp.Key}={string.Join(",", kvp.Value)}")));
            }
        }
    }
}

