using Microsoft.EntityFrameworkCore;
using OMG.Telemetrics.Infrastructure;
using OMG.Telemetrics.Infrastructure.Entities;

namespace OMG.Api.Telemetrics;

public sealed class MockIrrigationSystemAdapter(TelemetryDbContext telemetryDbContext) : IIrrigationSystemAdapter
{
    public async Task OpenWaterValveAsync(string meterId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(meterId))
        {
            return;
        }

        var plant = await telemetryDbContext.Plants
            .FirstOrDefaultAsync(p => p.MeterId == meterId, cancellationToken)
            .ConfigureAwait(false);

        if (plant is null)
        {
            return;
        }

        plant.IsWatering = true;
        await telemetryDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task CloseWaterValveAsync(string meterId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(meterId))
        {
            return;
        }

        var plant = await telemetryDbContext.Plants
            .FirstOrDefaultAsync(p => p.MeterId == meterId, cancellationToken)
            .ConfigureAwait(false);

        if (plant is null)
        {
            return;
        }

        plant.IsWatering = false;
        await telemetryDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

