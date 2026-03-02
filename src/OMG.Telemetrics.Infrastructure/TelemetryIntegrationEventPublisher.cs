using MassTransit;
using OMG.Messaging.Contracts.Telemetry;
using OMG.Telemetrics.Domain.Hydration;

namespace OMG.Telemetrics.Infrastructure;

public interface ITelemetryIntegrationEventPublisher
{
    Task PublishAsync(IEnumerable<object> domainEvents, CancellationToken cancellationToken = default);
}

public sealed class TelemetryIntegrationEventPublisher(IPublishEndpoint publishEndpoint) : ITelemetryIntegrationEventPublisher
{
    public async Task PublishAsync(IEnumerable<object> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            switch (domainEvent)
            {
                case PlantNeedsWateringDomainEvent e:
                    await publishEndpoint.Publish(
                        new WateringNeeded(
                            e.MeterId,
                            e.CurrentHumidityLevel,
                            e.IdealHumidityLevel,
                            e.OccurredAt),
                        cancellationToken).ConfigureAwait(false);
                    break;
                case WateringStartedDomainEvent e:
                    await publishEndpoint.Publish(
                        new WateringStarted(
                            e.SessionId,
                            e.PlantId,
                            e.MeterId,
                            e.StartedAt,
                            e.OccurredAt),
                        cancellationToken).ConfigureAwait(false);
                    break;
                case WateringCompletedDomainEvent e:
                    await publishEndpoint.Publish(
                        new WateringCompleted(
                            e.SessionId,
                            e.PlantId,
                            e.MeterId,
                            e.NewHumidityLevel,
                            e.OccurredAt),
                        cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
    }
}

