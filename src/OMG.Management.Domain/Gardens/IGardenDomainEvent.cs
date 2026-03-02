using OMG.Management.Domain.Abstractions;

namespace OMG.Management.Domain.Gardens;

public interface IGardenDomainEvent : IDomainEvent
{
    Garden Garden { get; }
}
