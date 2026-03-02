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

public sealed record PlantAdded(
    Guid GardenId,
    Guid PlantId,
    string Name,
    string Species,
    string Type,
    decimal SurfaceAreaRequired,
    int IdealHumidityLevel,
    DateTimeOffset PlantationDate,
    DateTimeOffset OccurredAt,
    string? CorrelationId,
    string? CausationId);

public sealed record PlantRemoved(
    Guid GardenId,
    Guid PlantId,
    DateTimeOffset OccurredAt,
    string? CorrelationId,
    string? CausationId);

public sealed record PlantRenamed(
    Guid GardenId,
    Guid PlantId,
    string Name,
    DateTimeOffset OccurredAt,
    string? CorrelationId,
    string? CausationId);

public sealed record PlantReclassified(
    Guid GardenId,
    Guid PlantId,
    string Species,
    string Type,
    DateTimeOffset OccurredAt,
    string? CorrelationId,
    string? CausationId);

public sealed record PlantSurfaceAreaRequirementChanged(
    Guid GardenId,
    Guid PlantId,
    decimal SurfaceAreaRequired,
    DateTimeOffset OccurredAt,
    string? CorrelationId,
    string? CausationId);

public sealed record PlantIdealHumidityLevelChanged(
    Guid GardenId,
    Guid PlantId,
    int IdealHumidityLevel,
    DateTimeOffset OccurredAt,
    string? CorrelationId,
    string? CausationId);

public sealed record PlantPlantationDateChanged(
    Guid GardenId,
    Guid PlantId,
    DateTimeOffset PlantationDate,
    DateTimeOffset OccurredAt,
    string? CorrelationId,
    string? CausationId);

