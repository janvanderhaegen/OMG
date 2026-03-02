using OMG.Management.Domain.Abstractions;

namespace OMG.Management.Domain.Gardens;

public sealed record GardenCreatedDomainEvent(Garden Garden, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record GardenRenamedDomainEvent(Garden Garden, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record GardenSurfaceAreaChangedDomainEvent(Garden Garden, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record GardenTargetHumidityChangedDomainEvent(Garden Garden, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record GardenDeletedDomainEvent(Garden Garden, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PlantAddedToGardenDomainEvent(Garden Garden, Plant Plant, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PlantRenamedDomainEvent(Garden Garden, Plant Plant, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PlantReclassifiedDomainEvent(Garden Garden, Plant Plant, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PlantSurfaceAreaRequirementChangedDomainEvent(Garden Garden, Plant Plant, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PlantIdealHumidityLevelChangedDomainEvent(Garden Garden, Plant Plant, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PlantPlantationDateChangedDomainEvent(Garden Garden, Plant Plant, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PlantRemovedFromGardenDomainEvent(Garden Garden, Plant Plant, DateTimeOffset OccurredAt) : IDomainEvent;

