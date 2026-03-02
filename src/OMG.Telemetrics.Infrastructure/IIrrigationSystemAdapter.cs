namespace OMG.Telemetrics.Infrastructure;

public interface IIrrigationSystemAdapter
{
    Task OpenWaterValveAsync(string meterId, CancellationToken cancellationToken = default);

    Task CloseWaterValveAsync(string meterId, CancellationToken cancellationToken = default);
}

