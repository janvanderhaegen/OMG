using MassTransit;
using Microsoft.EntityFrameworkCore;
using OMG.Messaging.Contracts.Management;

namespace OMG.Telemetrics.Infrastructure.Consumers;

public class PlantRemovedConsumer : IConsumer<PlantRemoved>
{
    private readonly TelemetricsDbContext _db;

    public PlantRemovedConsumer(TelemetricsDbContext db)
    {
        _db = db;
    }

    public async Task Consume(ConsumeContext<PlantRemoved> context)
    {
        var msg = context.Message;
        var state = await _db.PlantHydrationStates
            .FirstOrDefaultAsync(s => s.PlantId == msg.PlantId, context.CancellationToken)
            .ConfigureAwait(false);

        if (state is null)
            return;

        // Remove any active watering sessions for this plant
        var sessions = await _db.WateringSessions
            .Where(w => w.PlantId == msg.PlantId && w.Status != "Completed" && w.Status != "Cancelled")
            .ToListAsync(context.CancellationToken)
            .ConfigureAwait(false);

        _db.WateringSessions.RemoveRange(sessions);
        _db.PlantHydrationStates.Remove(state);
        await _db.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
    }
}
