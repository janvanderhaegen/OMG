using MassTransit;
using Microsoft.Extensions.Logging;
using OMG.Management.Domain.Abstractions;
using OMG.Management.Domain.Common;
using OMG.Management.Domain.Gardens;
using OMG.Management.Infrastructure.Messaging;
using OMG.Messaging.Contracts.Auth;

namespace OMG.Management.Infrastructure.Consumers;

/// <summary>
/// Consumer that reacts to a removed user by deleting all gardens owned by that user.
/// This keeps the Garden Management context decoupled from the Auth HTTP surface.
/// </summary>
public sealed class UserRemovedConsumer : IConsumer<UserRemoved>
{
    private readonly IGardenRepository _gardenRepository;
    private readonly IGardenIntegrationEventPublisher _integrationEventPublisher;
    private readonly IManagementUnitOfWork _unitOfWork;
    private readonly ILogger<UserRemovedConsumer> _logger;

    public UserRemovedConsumer(
        IGardenRepository gardenRepository,
        IGardenIntegrationEventPublisher integrationEventPublisher,
        IManagementUnitOfWork unitOfWork,
        ILogger<UserRemovedConsumer> logger)
    {
        _gardenRepository = gardenRepository;
        _integrationEventPublisher = integrationEventPublisher;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UserRemoved> context)
    {
        var message = context.Message;
        var userId = new UserId(message.UserId);

        var gardens = await _gardenRepository
            .ListByUserAsync(userId, context.CancellationToken)
            .ConfigureAwait(false);

        if (gardens.Count == 0)
        {
            _logger.LogInformation(
                "UserRemovedConsumer received UserRemoved for {UserId}, but no gardens were found.",
                message.UserId);
            return;
        }

        var occurredAt = message.OccurredAt;

        foreach (var garden in gardens)
        {
            garden.MarkDeleted(occurredAt);
            _gardenRepository.Remove(garden);

            await _integrationEventPublisher
                .PublishIntegrationEventsAsync(garden.DomainEvents, context.CancellationToken)
                .ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "UserRemovedConsumer soft-deleted {GardenCount} gardens for user {UserId}.",
            gardens.Count,
            message.UserId);
    }
}

