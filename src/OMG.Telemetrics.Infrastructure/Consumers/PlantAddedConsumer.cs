using MassTransit;
using OMG.Messaging.Contracts.Management;
using OMG.Telemetrics.Domain.Hydration;
using OMG.Telemetrics.Infrastructure.Entities;
using OMG.Telemetrics.Infrastructure.Mapping;

namespace OMG.Telemetrics.Infrastructure.Consumers;

public class PlantAddedConsumer : IConsumer<PlantAdded>
{
    private readonly TelemetricsDbContext _db;

    public PlantAddedConsumer(TelemetricsDbContext db)
    {
        _db = db;
    }

    public async Task Consume(ConsumeContext<PlantAdded> context)
    {
        var msg = context.Message;
        var plantType = HydrationStateMapping.ToPlantType(msg.Type);

        var result = PlantHydrationState.InitializeFromPlantAdded(
            msg.PlantId,
            msg.GardenId,
            plantType,
            msg.IdealHumidityLevel);

        if (result.IsFailure)
            return;

        var state = result.Value!;
        var entity = HydrationStateMapping.ToEntity(state);
        _db.PlantHydrationStates.Add(entity);
        await _db.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
    }
}
