namespace OMG.Telemetrics.Infrastructure;

/// <summary>
/// Adapter for the mocked irrigation hardware. For the prototype, implementations log or record start/stop watering commands.
/// </summary>
public interface IMockIrrigationSystemAdapter
{
    void StartWatering(Guid plantId, Guid gardenId);
    void StopWatering(Guid plantId, Guid gardenId);
}
