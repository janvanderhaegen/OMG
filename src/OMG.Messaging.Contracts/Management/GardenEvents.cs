namespace OMG.Messaging.Contracts.Management;

public sealed record GardenCreated(
    Guid GardenId,
    Guid UserId,
    string Name,
    decimal TotalSurfaceArea,
    int TargetHumidityLevel,
    DateTimeOffset OccurredAt,
    string? CorrelationId,
    string? CausationId);

public sealed record GardenRenamed(
    Guid GardenId,
    Guid UserId,
    string Name,
    DateTimeOffset OccurredAt,
    string? CorrelationId,
    string? CausationId);

public sealed record GardenSurfaceAreaChanged(
    Guid GardenId,
    Guid UserId,
    decimal TotalSurfaceArea,
    DateTimeOffset OccurredAt,
    string? CorrelationId,
    string? CausationId);

public sealed record GardenTargetHumidityChanged(
    Guid GardenId,
    Guid UserId,
    int TargetHumidityLevel,
    DateTimeOffset OccurredAt,
    string? CorrelationId,
    string? CausationId);

public sealed record GardenDeleted(
    Guid GardenId,
    Guid UserId,
    DateTimeOffset OccurredAt,
    string? CorrelationId,
    string? CausationId);

