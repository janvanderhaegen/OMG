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
}

