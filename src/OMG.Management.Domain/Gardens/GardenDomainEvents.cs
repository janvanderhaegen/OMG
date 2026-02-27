using OMG.Management.Domain.Abstractions;

namespace OMG.Management.Domain.Gardens;

public sealed record GardenCreatedDomainEvent(Garden Garden, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record GardenRenamedDomainEvent(Garden Garden, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record GardenSurfaceAreaChangedDomainEvent(Garden Garden, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record GardenTargetHumidityChangedDomainEvent(Garden Garden, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record GardenDeletedDomainEvent(Garden Garden, DateTimeOffset OccurredAt) : IDomainEvent;

