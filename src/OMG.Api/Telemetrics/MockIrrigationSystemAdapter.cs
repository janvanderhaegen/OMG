using OMG.Telemetrics.Infrastructure;

namespace OMG.Api.Telemetrics;

public sealed class MockIrrigationSystemAdapter : IMockIrrigationSystemAdapter
{
    private readonly ILogger<MockIrrigationSystemAdapter> _logger;

    public MockIrrigationSystemAdapter(ILogger<MockIrrigationSystemAdapter> logger)
    {
        _logger = logger;
    }

    public void StartWatering(Guid plantId, Guid gardenId)
    {
        _logger.LogInformation("Mock irrigation: Start watering Plant {PlantId} in Garden {GardenId}", plantId, gardenId);
    }

    public void StopWatering(Guid plantId, Guid gardenId)
    {
        _logger.LogInformation("Mock irrigation: Stop watering Plant {PlantId} in Garden {GardenId}", plantId, gardenId);
    }
}
