using MassTransit;
using Microsoft.EntityFrameworkCore;
using OMG.Messaging.Contracts.Management;
using OMG.Telemetrics.Infrastructure.Mapping;

namespace OMG.Telemetrics.Infrastructure.Consumers;

public class PlantIdealHumidityLevelChangedConsumer : IConsumer<PlantIdealHumidityLevelChanged>
{
    private readonly TelemetricsDbContext _db;

    public PlantIdealHumidityLevelChangedConsumer(TelemetricsDbContext db)
    {
        _db = db;
    }

    public async Task Consume(ConsumeContext<PlantIdealHumidityLevelChanged> context)
    {
        var msg = context.Message;
        var entity = await _db.PlantHydrationStates
            .FirstOrDefaultAsync(s => s.PlantId == msg.PlantId, context.CancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
            return;

        var state = HydrationStateMapping.ToDomain(entity, activeSession: null);
        state.UpdateIdealHumidityLevel(msg.IdealHumidityLevel);
        HydrationStateMapping.ApplyToEntity(state, entity);
        await _db.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
    }
}
