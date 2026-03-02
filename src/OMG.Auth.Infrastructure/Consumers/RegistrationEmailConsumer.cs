using MassTransit;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<RegistrationEmailConsumer> _logger;

    public RegistrationEmailConsumer(ILogger<RegistrationEmailConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<SendRegistrationEmail> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Mock sending registration email to {Email} with verification code {Code}. " +
            "User must verify via /api/v1/auth/verify-email using this code.",
            message.Email,
            message.VerificationCode);
    }
}

