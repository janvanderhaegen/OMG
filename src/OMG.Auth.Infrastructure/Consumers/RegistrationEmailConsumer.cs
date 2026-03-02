using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OMG.Auth.Infrastructure.Entities;
using OMG.Messaging.Contracts.Auth;

namespace OMG.Auth.Infrastructure.Consumers;

/// <summary>
/// Mock consumer that simulates sending a registration email and automatically
/// marks the account as verified. This behavior is for demo purposes only and
/// should be replaced with a real email delivery and user-driven verification
/// flow in a production system.
/// </summary>
public class RegistrationEmailConsumer : IConsumer<SendRegistrationEmail>
{
    private readonly AuthDbContext _dbContext;
    private readonly ILogger<RegistrationEmailConsumer> _logger;

    public RegistrationEmailConsumer(AuthDbContext dbContext, ILogger<RegistrationEmailConsumer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<SendRegistrationEmail> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Mock sending registration email to {Email} with verification code {Code}",
            message.Email,
            message.VerificationCode);

        var user = await _dbContext.Set<ApplicationUser>()
            .FirstOrDefaultAsync(u => u.Id == message.UserId, context.CancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            _logger.LogWarning(
                "RegistrationEmailConsumer could not find user with id {UserId} to verify.",
                message.UserId);
            return;
        }

        user.IsEmailVerified = true;
        user.EmailConfirmed = true;
        user.VerificationCode = null;
        user.VerificationCodeExpiresAt = null;

        await _dbContext.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "User {UserId} has been automatically marked as verified (demo behavior).",
            message.UserId);
    }
}

