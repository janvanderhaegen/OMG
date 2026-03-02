namespace OMG.Telemetrics.Domain.Hydration;

public sealed record PlantNeedsWateringDomainEvent(
    Guid PlantId,
    string MeterId,
    int CurrentHumidityLevel,
    int IdealHumidityLevel,
    DateTimeOffset OccurredAt);

public sealed record WateringStartedDomainEvent(
    Guid SessionId,
    Guid PlantId,
    string MeterId,
    DateTimeOffset StartedAt,
    DateTimeOffset OccurredAt);

public sealed record WateringCompletedDomainEvent(
    Guid SessionId,
    Guid PlantId,
    string MeterId,
    int NewHumidityLevel,
    DateTimeOffset OccurredAt);

