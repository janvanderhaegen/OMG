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

