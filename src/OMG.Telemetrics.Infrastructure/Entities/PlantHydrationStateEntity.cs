namespace OMG.Telemetrics.Infrastructure.Entities;

public class PlantHydrationStateEntity
{
    public Guid PlantId { get; set; }
    public Guid GardenId { get; set; }
    public string PlantType { get; set; } = string.Empty; // "Vegetable", "Fruit", "Flower"
    public int IdealHumidityLevel { get; set; }
    public int CurrentHumidity { get; set; }
    public DateTimeOffset? LastIrrigationStart { get; set; }
    public DateTimeOffset? LastIrrigationEnd { get; set; }
    public bool IsWatering { get; set; }
    public bool HasIrrigationLine { get; set; }
}
