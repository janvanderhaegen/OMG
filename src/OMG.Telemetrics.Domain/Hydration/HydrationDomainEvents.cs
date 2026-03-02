namespace OMG.Telemetrics.Domain.Hydration;

public sealed record PlantNeedsWateringDomainEvent(
    Guid PlantId,
    Guid GardenId,
    int CurrentHumidityLevel,
    int IdealHumidityLevel,
    DateTimeOffset OccurredAt) : Abstractions.IDomainEvent;

public sealed record WateringStartedDomainEvent(
    Guid SessionId,
    Guid PlantId,
    Guid GardenId,
    DateTimeOffset StartedAt,
    DateTimeOffset OccurredAt) : Abstractions.IDomainEvent;

public sealed record WateringCompletedDomainEvent(
    Guid SessionId,
    Guid PlantId,
    Guid GardenId,
    int NewHumidityLevel,
    DateTimeOffset OccurredAt) : Abstractions.IDomainEvent;
