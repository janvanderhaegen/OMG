namespace OMG.Telemetrics.Domain.Hydration;

public interface IPlantHydrationStateRepository
{
    Task<PlantHydrationState?> GetByPlantIdAsync(Guid plantId, CancellationToken cancellationToken = default);

    Task<PlantHydrationState?> GetByMeterIdAsync(Guid gardenId, string meterId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(PlantHydrationState State, string? MeterId, Guid GardenId)>> ListWithIrrigationLineAsync(CancellationToken cancellationToken = default);

    Task AddAsync(PlantHydrationState state, string? meterId, Guid gardenId, CancellationToken cancellationToken = default);

    Task SaveAsync(PlantHydrationState state, string? meterId, Guid gardenId, CancellationToken cancellationToken = default);
}
