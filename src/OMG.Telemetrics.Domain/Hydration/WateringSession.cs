namespace OMG.Telemetrics.Domain.Hydration;

public sealed class WateringSession
{
    public Guid SessionId { get; }
    public Guid PlantId { get; }
    public Guid GardenId { get; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset EndsAt { get; }
    public WateringSessionStatus Status { get; private set; }

    private WateringSession(Guid sessionId, Guid plantId, Guid gardenId, DateTimeOffset startedAt, DateTimeOffset endsAt, WateringSessionStatus status)
    {
        SessionId = sessionId;
        PlantId = plantId;
        GardenId = gardenId;
        StartedAt = startedAt;
        EndsAt = endsAt;
        Status = status;
    }

    public static WateringSession Create(Guid plantId, Guid gardenId, DateTimeOffset startedAt)
    {
        var endsAt = startedAt.AddMinutes(HydrationConstants.WateringDurationMinutes);
        return new WateringSession(
            Guid.NewGuid(),
            plantId,
            gardenId,
            startedAt,
            endsAt,
            WateringSessionStatus.Started);
    }

    public static WateringSession FromPersistence(Guid sessionId, Guid plantId, Guid gardenId, DateTimeOffset startedAt, DateTimeOffset endsAt, WateringSessionStatus status)
    {
        return new WateringSession(sessionId, plantId, gardenId, startedAt, endsAt, status);
    }

    public void MarkCompleted() => Status = WateringSessionStatus.Completed;

    public void MarkCancelled() => Status = WateringSessionStatus.Cancelled;

    public bool IsDueAt(DateTimeOffset now) => Status == WateringSessionStatus.Started && EndsAt <= now;
}
