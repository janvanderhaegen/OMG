namespace OMG.Telemetrics.Infrastructure.Entities;

public class WateringSessionEntity
{
    public Guid SessionId { get; set; }

    public Guid PlantId { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public bool IsActive { get; set; }
}

