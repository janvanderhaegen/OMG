using OMG.Management.Domain.Common;

namespace OMG.Management.Domain.Gardens;

public interface IGardenRepository
{
    Task<Garden?> GetByIdAsync(GardenId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Garden>> ListByUserAsync(UserId userId, CancellationToken cancellationToken = default);

    Task AddAsync(Garden garden, CancellationToken cancellationToken = default);

    void Remove(Garden garden);
}

