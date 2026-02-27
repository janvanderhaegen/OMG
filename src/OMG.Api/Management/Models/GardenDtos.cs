namespace OMG.Api.Management.Models;

public sealed record CreateGardenRequest(
    Guid UserId,
    string Name,
    decimal TotalSurfaceArea,
    int TargetHumidityLevel);

public sealed record UpdateGardenRequest(
    string Name,
    decimal TotalSurfaceArea,
    int TargetHumidityLevel);

public sealed record GardenResponse(
    Guid Id,
    Guid UserId,
    string Name,
    decimal TotalSurfaceArea,
    int TargetHumidityLevel,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

