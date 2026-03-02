using MassTransit;
using Microsoft.EntityFrameworkCore;
using OMG.Management.Infrastructure;
using OMG.Messaging.Contracts.Management;
using OMG.Telemetrics.Infrastructure;
using OMG.Telemetrics.Infrastructure.Entities;

namespace OMG.Api.Telemetrics.Consumers;

/// <summary>
/// Creates or updates the telemetry plant when a meter is attached to a management plant.
/// </summary>
public sealed class PlantMeterAttachedConsumer(
    ManagementDbContext managementDb,
    TelemetryDbContext telemetryDb) : IConsumer<PlantMeterAttached>
{
    public async Task Consume(ConsumeContext<PlantMeterAttached> context)
    {
        var msg = context.Message;

        var managementPlant = await managementDb.Plants
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.GardenId == msg.GardenId && p.Id == msg.PlantId,
                context.CancellationToken)
            .ConfigureAwait(false);

        var idealHumidity = managementPlant?.IdealHumidityLevel ?? 50;

        var existing = await telemetryDb.Plants
            .FirstOrDefaultAsync(p => p.PlantId == msg.PlantId, context.CancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            existing.MeterId = msg.MeterId;
            existing.HasIrrigationLine = true;
            existing.IdealHumidityLevel = idealHumidity;
        }
        else
        {
            var utcNow = new DateTimeOffset(DateTime.UtcNow, TimeSpan.Zero);
            var entity = new TelemetryPlantEntity
            {
                PlantId = msg.PlantId,
                GardenId = msg.GardenId,
                MeterId = msg.MeterId,
                IdealHumidityLevel = idealHumidity,
                CurrentHumidityLevel = idealHumidity,
                IsWatering = false,
                HasIrrigationLine = true,
                LastTelemetryAt = utcNow
            };
            telemetryDb.Plants.Add(entity);
        }

        await telemetryDb.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
    }
}
