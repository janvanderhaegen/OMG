namespace OMG.Messaging.Contracts.Auth;

/// <summary>
/// Integration message representing a registration email that should be sent to a user.
/// In this demo implementation, the consumer will automatically mark the user as verified
/// instead of actually sending an email.
/// </summary>
public sealed record SendRegistrationEmail(
    Guid UserId,
    string Email,
    string VerificationCode);

