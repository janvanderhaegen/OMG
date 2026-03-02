using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OMG.Api.Management;
using OMG.Management.Domain.Gardens;
using OMG.Management.Infrastructure;
using OMG.Telemetrics.Infrastructure;
using OMG.Telemetrics.Infrastructure.Entities;

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

    private async Task SimulateAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();

        var telemetryDb = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
        var managementDb = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var plants = await telemetryDb.Plants
            .AsNoTracking()
            .Where(p => p.HasIrrigationLine && p.MeterId != null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (plants.Count == 0)
        {
            return;
        }
         

        var readingsByGarden = new Dictionary<Guid, List<TelemetryReadingRequest>>();

        foreach (var plant in plants)
        {
            var typed = managementDb.Plants
                .Where(p => p.Id == plant.PlantId)  
                .Select(c => c.Type)
                .Single();
            var type = Enum.TryParse<PlantType>(typed, ignoreCase: true, out var parsed) ? parsed : PlantType.Vegetable;


            var meterId = plant.MeterId!;

            var current = plant.CurrentHumidityLevel;
            var ideal = plant.IdealHumidityLevel;
            var isWatering = plant.IsWatering;

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

            plant.CurrentHumidityLevel = next;

            if (!readingsByGarden.TryGetValue(plant.GardenId, out var list))
            {
                list = [];
                readingsByGarden[plant.GardenId] = list;
            }

            list.Add(new TelemetryReadingRequest(meterId, plant.CurrentHumidityLevel, plant.IsWatering));
        }

        await telemetryDb.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

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

