namespace OMG.Telemetrics.Infrastructure.Entities;

public class WateringSessionEntity
{
    public Guid SessionId { get; set; }
    public Guid PlantId { get; set; }
    public Guid GardenId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string Status { get; set; } = string.Empty; // "Planned", "Started", "Completed", "Cancelled"
}
