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
            switch (domainEvent)
            {
                case GardenCreatedDomainEvent created:
                    await PublishGardenCreatedAsync(created.Garden, created.OccurredAt, cancellationToken).ConfigureAwait(false);
                    aggregatesToClear.Add(created.Garden);
                    break;
                case GardenRenamedDomainEvent renamed:
                    await PublishGardenRenamedAsync(renamed.Garden, renamed.OccurredAt, cancellationToken).ConfigureAwait(false);
                    aggregatesToClear.Add(renamed.Garden);
                    break;
                case GardenSurfaceAreaChangedDomainEvent surfaceChanged:
                    await PublishGardenSurfaceAreaChangedAsync(surfaceChanged.Garden, surfaceChanged.OccurredAt, cancellationToken).ConfigureAwait(false);
                    aggregatesToClear.Add(surfaceChanged.Garden);
                    break;
                case GardenTargetHumidityChangedDomainEvent humidityChanged:
                    await PublishGardenTargetHumidityChangedAsync(humidityChanged.Garden, humidityChanged.OccurredAt, cancellationToken).ConfigureAwait(false);
                    aggregatesToClear.Add(humidityChanged.Garden);
                    break;
                case PlantAddedToGardenDomainEvent plantAdded:
                    await PublishPlantAddedAsync(plantAdded.Garden, plantAdded.Plant, plantAdded.OccurredAt, cancellationToken).ConfigureAwait(false);
                    aggregatesToClear.Add(plantAdded.Garden);
                    break;
                case PlantRemovedFromGardenDomainEvent plantRemoved:
                    await PublishPlantRemovedAsync(plantRemoved.Garden, plantRemoved.Plant, plantRemoved.OccurredAt, cancellationToken).ConfigureAwait(false);
                    aggregatesToClear.Add(plantRemoved.Garden);
                    break;
                case PlantRenamedDomainEvent e:
                    await PublishPlantRenamedAsync(e.Garden, e.Plant, e.OccurredAt, cancellationToken).ConfigureAwait(false);
                    aggregatesToClear.Add(e.Garden);
                    break;
                case PlantReclassifiedDomainEvent e:
                    await PublishPlantReclassifiedAsync(e.Garden, e.Plant, e.OccurredAt, cancellationToken).ConfigureAwait(false);
                    aggregatesToClear.Add(e.Garden);
                    break;
                case PlantSurfaceAreaRequirementChangedDomainEvent e:
                    await PublishPlantSurfaceAreaRequirementChangedAsync(e.Garden, e.Plant, e.OccurredAt, cancellationToken).ConfigureAwait(false);
                    aggregatesToClear.Add(e.Garden);
                    break;
                case PlantIdealHumidityLevelChangedDomainEvent e:
                    await PublishPlantIdealHumidityLevelChangedAsync(e.Garden, e.Plant, e.OccurredAt, cancellationToken).ConfigureAwait(false);
                    aggregatesToClear.Add(e.Garden);
                    break;
                case PlantPlantationDateChangedDomainEvent e:
                    await PublishPlantPlantationDateChangedAsync(e.Garden, e.Plant, e.OccurredAt, cancellationToken).ConfigureAwait(false);
                    aggregatesToClear.Add(e.Garden);
                    break;
                case GardenDeletedDomainEvent deleted:
                    var deletedMessage = new GardenDeleted(
                        deleted.Garden.Id.Value,
                        deleted.Garden.UserId.Value,
                        deleted.OccurredAt,
                        null,
                        null);

                    await publishEndpoint.Publish(deletedMessage, cancellationToken).ConfigureAwait(false);
                    aggregatesToClear.Add(deleted.Garden);
                    break;
            }
        }

        foreach (var aggregate in aggregatesToClear)
        {
            aggregate.ClearDomainEvents();
        }
    }

    private Task PublishGardenCreatedAsync(Garden garden, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        var message = new GardenCreated(
            garden.Id.Value,
            garden.UserId.Value,
            garden.Name,
            garden.TotalSurfaceArea.Value,
            garden.TargetHumidityLevel.Value,
            occurredAt,
            null,
            null);

        return publishEndpoint.Publish(message, cancellationToken);
    }

    private Task PublishGardenRenamedAsync(Garden garden, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        var message = new GardenRenamed(
            garden.Id.Value,
            garden.UserId.Value,
            garden.Name,
            occurredAt,
            null,
            null);

        return publishEndpoint.Publish(message, cancellationToken);
    }

    private Task PublishGardenSurfaceAreaChangedAsync(Garden garden, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        var message = new GardenSurfaceAreaChanged(
            garden.Id.Value,
            garden.UserId.Value,
            garden.TotalSurfaceArea.Value,
            occurredAt,
            null,
            null);

        return publishEndpoint.Publish(message, cancellationToken);
    }

    private Task PublishGardenTargetHumidityChangedAsync(Garden garden, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        var message = new GardenTargetHumidityChanged(
            garden.Id.Value,
            garden.UserId.Value,
            garden.TargetHumidityLevel.Value,
            occurredAt,
            null,
            null);

        return publishEndpoint.Publish(message, cancellationToken);
    }

    private Task PublishPlantAddedAsync(Garden garden, Plant plant, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        var message = new PlantAdded(
            garden.Id.Value,
            plant.Id.Value,
            plant.Name,
            plant.Species,
            plant.Type.ToString(),
            plant.SurfaceAreaRequired.Value,
            plant.IdealHumidityLevel.Value,
            plant.PlantationDate,
            occurredAt,
            null,
            null);

        return publishEndpoint.Publish(message, cancellationToken);
    }

    private Task PublishPlantRemovedAsync(Garden garden, Plant plant, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        var message = new PlantRemoved(
            garden.Id.Value,
            plant.Id.Value,
            occurredAt,
            CorrelationId: null,
            null);

        return publishEndpoint.Publish(message, cancellationToken);
    }

    private Task PublishPlantRenamedAsync(Garden garden, Plant plant, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        var message = new PlantRenamed(garden.Id.Value, plant.Id.Value, plant.Name, occurredAt, null, null);
        return publishEndpoint.Publish(message, cancellationToken);
    }

    private Task PublishPlantReclassifiedAsync(Garden garden, Plant plant, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        var message = new PlantReclassified(garden.Id.Value, plant.Id.Value, plant.Species, plant.Type.ToString(), occurredAt, null, null);
        return publishEndpoint.Publish(message, cancellationToken);
    }

    private Task PublishPlantSurfaceAreaRequirementChangedAsync(Garden garden, Plant plant, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        var message = new PlantSurfaceAreaRequirementChanged(garden.Id.Value, plant.Id.Value, plant.SurfaceAreaRequired.Value, occurredAt, null, null);
        return publishEndpoint.Publish(message, cancellationToken);
    }

    private Task PublishPlantIdealHumidityLevelChangedAsync(Garden garden, Plant plant, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        var message = new PlantIdealHumidityLevelChanged(garden.Id.Value, plant.Id.Value, plant.IdealHumidityLevel.Value, occurredAt, null, null);
        return publishEndpoint.Publish(message, cancellationToken);
    }

    private Task PublishPlantPlantationDateChangedAsync(Garden garden, Plant plant, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        var message = new PlantPlantationDateChanged(garden.Id.Value, plant.Id.Value, plant.PlantationDate, occurredAt, null, null);
        return publishEndpoint.Publish(message, cancellationToken);
    }
}

