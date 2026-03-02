namespace OMG.Api.Management.Models;

public sealed record CreatePlantRequest(
    string Name,
    string Species,
    string Type,
    DateTimeOffset PlantationDate,
    decimal SurfaceAreaRequired,
    int IdealHumidityLevel);

public sealed record UpdatePlantRequest(
    string Name,
    string Species,
    string Type,
    DateTimeOffset PlantationDate,
    decimal SurfaceAreaRequired,
    int IdealHumidityLevel);

public sealed record PlantResponse(
    Guid Id,
    Guid GardenId,
    string Name,
    string Species,
    string Type,
    DateTimeOffset PlantationDate,
    decimal SurfaceAreaRequired,
    int IdealHumidityLevel);

