namespace OMG.Telemetrics.Domain.Hydration;

public sealed class WateringSession
{
    public WateringSession(Guid sessionId, Guid plantId, DateTimeOffset startedAt, DateTimeOffset endsAt)
    {
        SessionId = sessionId;
        PlantId = plantId;
        StartedAt = startedAt;
        EndsAt = endsAt;
    }

    public Guid SessionId { get; }

    public Guid PlantId { get; }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset EndsAt { get; }
}

