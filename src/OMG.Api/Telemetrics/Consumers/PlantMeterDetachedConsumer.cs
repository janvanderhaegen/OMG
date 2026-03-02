using MassTransit;
using Microsoft.EntityFrameworkCore;
using OMG.Messaging.Contracts.Management;
using OMG.Telemetrics.Infrastructure;

namespace OMG.Api.Telemetrics.Consumers;

/// <summary>
/// Marks the telemetry plant as detached when a meter is removed from a management plant.
/// </summary>
public sealed class PlantMeterDetachedConsumer(TelemetryDbContext telemetryDb) : IConsumer<PlantMeterDetached>
{
    public async Task Consume(ConsumeContext<PlantMeterDetached> context)
    {
        var msg = context.Message;

        var telemetryPlant = await telemetryDb.Plants
            .FirstOrDefaultAsync(p => p.PlantId == msg.PlantId, context.CancellationToken)
            .ConfigureAwait(false);

        if (telemetryPlant is null)
            return;

        telemetryPlant.MeterId = null;
        telemetryPlant.HasIrrigationLine = false;

        await telemetryDb.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
    }
}
