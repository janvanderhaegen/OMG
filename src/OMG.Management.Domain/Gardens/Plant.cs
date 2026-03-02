using OMG.Management.Domain.Common;

namespace OMG.Management.Domain.Gardens;

public readonly record struct PlantId(Guid Value)
{
    public static PlantId New() => new(Guid.NewGuid());

    public static PlantId From(Guid value) => new(value);
}

public enum PlantType
{
    Vegetable = 0,
    Fruit = 1,
    Flower = 2
}

public sealed class Plant
{
    public Plant(
        PlantId id,
        string name,
        string species,
        PlantType type,
        DateTimeOffset plantationDate,
        SurfaceArea surfaceAreaRequired,
        HumidityLevel idealHumidityLevel)
    {
        Id = id;
        Name = name;
        Species = species;
        Type = type;
        PlantationDate = plantationDate;
        SurfaceAreaRequired = surfaceAreaRequired;
        IdealHumidityLevel = idealHumidityLevel;
    }

    public PlantId Id { get; }

    public string Name { get; internal set; }

    public string Species { get; internal set; }

    public PlantType Type { get; internal set; }

    public DateTimeOffset PlantationDate { get; internal set; }

    public SurfaceArea SurfaceAreaRequired { get; internal set; }

    public HumidityLevel IdealHumidityLevel { get; internal set; }

    internal static Result<Plant> Create(
        string name,
        string species,
        PlantType type,
        DateTimeOffset plantationDate,
        decimal surfaceAreaRequired,
        int idealHumidityLevel)
    {
        var validationErrors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(name))
        {
            validationErrors["name"] = ["Name is required."];
        }

        if (string.IsNullOrWhiteSpace(species))
        {
            validationErrors["species"] = ["Species is required."];
        }

        if (surfaceAreaRequired <= 0)
        {
            validationErrors["surfaceAreaRequired"] = ["Surface area required must be greater than zero."];
        }

        if (idealHumidityLevel is < 0 or > 100)
        {
            validationErrors["idealHumidityLevel"] = ["Ideal humidity level must be between 0 and 100."];
        }

        if (validationErrors.Count > 0)
        {
            return Result<Plant>.Failure(
                ErrorCodes.PlantValidationFailed,
                "One or more validation errors occurred while creating a plant.",
                validationErrors);
        }

        var plant = new Plant(
            PlantId.New(),
            name.Trim(),
            species.Trim(),
            type,
            plantationDate,
            new SurfaceArea(surfaceAreaRequired),
            new HumidityLevel(idealHumidityLevel));

        return Result<Plant>.Success(plant);
    }
}

