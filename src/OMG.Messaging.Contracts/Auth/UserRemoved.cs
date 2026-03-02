namespace OMG.Messaging.Contracts.Auth;

/// <summary>
/// Integration message indicating that a user has been removed from the system.
/// Downstream consumers can react by cleaning up user-owned data in their own bounded contexts.
/// </summary>
public sealed record UserRemoved(
    Guid UserId,
    DateTimeOffset OccurredAt,
    string? Reason);

