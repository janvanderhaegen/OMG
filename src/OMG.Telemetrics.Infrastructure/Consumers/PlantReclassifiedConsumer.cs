using MassTransit;
using Microsoft.EntityFrameworkCore;
using OMG.Messaging.Contracts.Management;
using OMG.Telemetrics.Domain.Hydration;
using OMG.Telemetrics.Infrastructure.Mapping;

namespace OMG.Telemetrics.Infrastructure.Consumers;

public class PlantReclassifiedConsumer : IConsumer<PlantReclassified>
{
    private readonly TelemetricsDbContext _db;

    public PlantReclassifiedConsumer(TelemetricsDbContext db)
    {
        _db = db;
    }

    public async Task Consume(ConsumeContext<PlantReclassified> context)
    {
        var msg = context.Message;
        var entity = await _db.PlantHydrationStates
            .FirstOrDefaultAsync(s => s.PlantId == msg.PlantId, context.CancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
            return;

        var plantType = HydrationStateMapping.ToPlantType(msg.Type);
        var state = HydrationStateMapping.ToDomain(entity, activeSession: null);
        state.UpdatePlantType(plantType);
        HydrationStateMapping.ApplyToEntity(state, entity);
        await _db.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
    }
}
