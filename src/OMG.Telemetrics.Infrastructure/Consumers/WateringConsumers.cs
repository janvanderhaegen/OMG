using MassTransit;
using OMG.Messaging.Contracts.Telemetry;

namespace OMG.Telemetrics.Infrastructure.Consumers;

public sealed class WateringNeededConsumer(IIrrigationSystemAdapter irrigationSystemAdapter) : IConsumer<WateringNeeded>
{
    public async Task Consume(ConsumeContext<WateringNeeded> context)
    {
        await irrigationSystemAdapter.OpenWaterValveAsync(context.Message.MeterId, context.CancellationToken);
    }
}

public sealed class HydrationSatisfiedConsumer(IIrrigationSystemAdapter irrigationSystemAdapter) : IConsumer<HydrationSatisfied>
{
    public async Task Consume(ConsumeContext<HydrationSatisfied> context)
    {
        await irrigationSystemAdapter.CloseWaterValveAsync(context.Message.MeterId, context.CancellationToken);
    }
}

