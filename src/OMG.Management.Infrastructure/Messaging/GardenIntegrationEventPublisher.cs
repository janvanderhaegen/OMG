using OMG.Management.Domain.Abstractions;
using OMG.Management.Domain.Gardens;
using OMG.Messaging.Contracts.Management;
using MassTransit;

namespace OMG.Management.Infrastructure.Messaging;

public interface IGardenIntegrationEventPublisher
{
    Task PublishIntegrationEventsAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}

public sealed class GardenIntegrationEventPublisher(IPublishEndpoint publishEndpoint) : IGardenIntegrationEventPublisher
{
    public async Task PublishIntegrationEventsAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        var events = domainEvents.ToList();
        var aggregatesToClear = new HashSet<AggregateRoot>();

        foreach (var domainEvent in events)
        {
            if (domainEvent is not IGardenDomainEvent hasGarden)
                continue;

            var message = MapToIntegrationMessage(domainEvent);
            if (message == null)
                continue;

            await publishEndpoint.Publish(message, cancellationToken).ConfigureAwait(false);
            aggregatesToClear.Add(hasGarden.Garden);
        }

        foreach (var aggregate in aggregatesToClear)
        {
            aggregate.ClearDomainEvents();
        }
    }

    private static object? MapToIntegrationMessage(IDomainEvent domainEvent) => domainEvent switch
    {
        GardenCreatedDomainEvent e => new GardenCreated(
            e.Garden.Id.Value,
            e.Garden.UserId.Value,
            e.Garden.Name,
            e.Garden.TotalSurfaceArea.Value,
            e.Garden.TargetHumidityLevel.Value,
            e.OccurredAt,
            null,
            null),
        GardenRenamedDomainEvent e => new GardenRenamed(
            e.Garden.Id.Value,
            e.Garden.UserId.Value,
            e.Garden.Name,
            e.OccurredAt,
            null,
            null),
        GardenSurfaceAreaChangedDomainEvent e => new GardenSurfaceAreaChanged(
            e.Garden.Id.Value,
            e.Garden.UserId.Value,
            e.Garden.TotalSurfaceArea.Value,
            e.OccurredAt,
            null,
            null),
        GardenTargetHumidityChangedDomainEvent e => new GardenTargetHumidityChanged(
            e.Garden.Id.Value,
            e.Garden.UserId.Value,
            e.Garden.TargetHumidityLevel.Value,
            e.OccurredAt,
            null,
            null),
        GardenDeletedDomainEvent e => new GardenDeleted(
            e.Garden.Id.Value,
            e.Garden.UserId.Value,
            e.OccurredAt,
            null,
            null),
        PlantAddedToGardenDomainEvent e => new PlantAdded(
            e.Garden.Id.Value,
            e.Plant.Id.Value,
            e.Plant.Name,
            e.Plant.Species,
            e.Plant.Type.ToString(),
            e.Plant.SurfaceAreaRequired.Value,
            e.Plant.IdealHumidityLevel.Value,
            e.Plant.PlantationDate,
            e.OccurredAt,
            null,
            null),
        PlantRemovedFromGardenDomainEvent e => new PlantRemoved(
            e.Garden.Id.Value,
            e.Plant.Id.Value,
            e.OccurredAt,
            null,
            null),
        PlantRenamedDomainEvent e => new PlantRenamed(
            e.Garden.Id.Value,
            e.Plant.Id.Value,
            e.Plant.Name,
            e.OccurredAt,
            null,
            null),
        PlantReclassifiedDomainEvent e => new PlantReclassified(
            e.Garden.Id.Value,
            e.Plant.Id.Value,
            e.Plant.Species,
            e.Plant.Type.ToString(),
            e.OccurredAt,
            null,
            null),
        PlantSurfaceAreaRequirementChangedDomainEvent e => new PlantSurfaceAreaRequirementChanged(
            e.Garden.Id.Value,
            e.Plant.Id.Value,
            e.Plant.SurfaceAreaRequired.Value,
            e.OccurredAt,
            null,
            null),
        PlantIdealHumidityLevelChangedDomainEvent e => new PlantIdealHumidityLevelChanged(
            e.Garden.Id.Value,
            e.Plant.Id.Value,
            e.Plant.IdealHumidityLevel.Value,
            e.OccurredAt,
            null,
            null),
        PlantPlantationDateChangedDomainEvent e => new PlantPlantationDateChanged(
            e.Garden.Id.Value,
            e.Plant.Id.Value,
            e.Plant.PlantationDate,
            e.OccurredAt,
            null,
            null),
        _ => null
    };
}
