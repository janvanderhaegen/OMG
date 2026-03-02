namespace OMG.Messaging.Contracts.Telemetry;

public sealed record WateringNeeded(
    string MeterId,
    int CurrentHumidityLevel,
    int IdealHumidityLevel,
    DateTimeOffset OccurredAt);

public sealed record HydrationSatisfied(
    string MeterId,
    int CurrentHumidityLevel,
    int IdealHumidityLevel,
    DateTimeOffset OccurredAt);

public sealed record WateringStarted(
    Guid SessionId,
    Guid PlantId,
    string MeterId,
    DateTimeOffset StartedAt,
    DateTimeOffset OccurredAt);

public sealed record WateringCompleted(
    Guid SessionId,
    Guid PlantId,
    string MeterId,
    int NewHumidityLevel,
    DateTimeOffset OccurredAt);

