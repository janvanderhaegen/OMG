using MassTransit;
using OMG.Messaging.Contracts.Auth;

namespace OMG.Auth.Infrastructure.Messaging;

public interface IAuthIntegrationEventPublisher
{
    Task PublishRegistrationEmailAsync(
        Guid userId,
        string email,
        string verificationCode,
        CancellationToken cancellationToken = default);

    Task PublishUserRemovedAsync(
        Guid userId,
        DateTimeOffset occurredAt,
        string? reason,
        CancellationToken cancellationToken = default);
}

public sealed class AuthIntegrationEventPublisher(IPublishEndpoint publishEndpoint)
    : IAuthIntegrationEventPublisher
{
    public Task PublishRegistrationEmailAsync(
        Guid userId,
        string email,
        string verificationCode,
        CancellationToken cancellationToken = default)
    {
        var message = new SendRegistrationEmail(userId, email, verificationCode);
        return publishEndpoint.Publish(message, cancellationToken);
    }

    public Task PublishUserRemovedAsync(
        Guid userId,
        DateTimeOffset occurredAt,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var message = new UserRemoved(userId, occurredAt, reason);
        return publishEndpoint.Publish(message, cancellationToken);
    }
}

