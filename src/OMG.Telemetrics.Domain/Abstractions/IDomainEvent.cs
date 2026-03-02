namespace OMG.Telemetrics.Domain.Abstractions;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
