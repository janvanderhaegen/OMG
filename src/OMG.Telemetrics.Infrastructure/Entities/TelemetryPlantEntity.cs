namespace OMG.Telemetrics.Infrastructure.Entities;

public class TelemetryPlantEntity
{
    public Guid PlantId { get; set; }

    public Guid GardenId { get; set; }

    public string? MeterId { get; set; }

    public int IdealHumidityLevel { get; set; }

    public int CurrentHumidityLevel { get; set; }

    public bool IsWatering { get; set; }

    public bool HasIrrigationLine { get; set; }

    public DateTimeOffset? LastTelemetryAt { get; set; }
}

