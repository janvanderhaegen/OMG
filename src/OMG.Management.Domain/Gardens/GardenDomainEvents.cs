namespace OMG.Management.Domain.Gardens;

public sealed record GardenCreatedDomainEvent(Garden Garden, DateTimeOffset OccurredAt) : IGardenDomainEvent;

public sealed record GardenRenamedDomainEvent(Garden Garden, DateTimeOffset OccurredAt) : IGardenDomainEvent;

public sealed record GardenSurfaceAreaChangedDomainEvent(Garden Garden, DateTimeOffset OccurredAt) : IGardenDomainEvent;

public sealed record GardenTargetHumidityChangedDomainEvent(Garden Garden, DateTimeOffset OccurredAt) : IGardenDomainEvent;

public sealed record GardenDeletedDomainEvent(Garden Garden, DateTimeOffset OccurredAt) : IGardenDomainEvent;

public sealed record PlantAddedToGardenDomainEvent(Garden Garden, Plant Plant, DateTimeOffset OccurredAt) : IGardenDomainEvent;

public sealed record PlantRenamedDomainEvent(Garden Garden, Plant Plant, DateTimeOffset OccurredAt) : IGardenDomainEvent;

public sealed record PlantReclassifiedDomainEvent(Garden Garden, Plant Plant, DateTimeOffset OccurredAt) : IGardenDomainEvent;

public sealed record PlantSurfaceAreaRequirementChangedDomainEvent(Garden Garden, Plant Plant, DateTimeOffset OccurredAt) : IGardenDomainEvent;

public sealed record PlantIdealHumidityLevelChangedDomainEvent(Garden Garden, Plant Plant, DateTimeOffset OccurredAt) : IGardenDomainEvent;

public sealed record PlantPlantationDateChangedDomainEvent(Garden Garden, Plant Plant, DateTimeOffset OccurredAt) : IGardenDomainEvent;

public sealed record PlantRemovedFromGardenDomainEvent(Garden Garden, Plant Plant, DateTimeOffset OccurredAt) : IGardenDomainEvent;

