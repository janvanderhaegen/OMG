using OMG.Management.Domain.Abstractions;
using OMG.Management.Domain.Common;

namespace OMG.Management.Domain.Gardens;

public sealed class Garden : AggregateRoot
{
    private readonly List<Plant> _plants;

    private Garden(
        GardenId id,
        UserId userId,
        string name,
        SurfaceArea totalSurfaceArea,
        HumidityLevel targetHumidityLevel,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        bool isDeleted,
        DateTimeOffset? deletedAt,
        IEnumerable<Plant> plants)
    {
        Id = id;
        UserId = userId;
        Name = name;
        TotalSurfaceArea = totalSurfaceArea;
        TargetHumidityLevel = targetHumidityLevel;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        IsDeleted = isDeleted;
        DeletedAt = deletedAt;
        _plants = new List<Plant>(plants ?? Array.Empty<Plant>());
    }

    public GardenId Id { get; }

    public UserId UserId { get; }

    public string Name { get; private set; }

    public SurfaceArea TotalSurfaceArea { get; private set; }

    public HumidityLevel TargetHumidityLevel { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public IReadOnlyCollection<Plant> Plants => _plants.AsReadOnly();

    public static Garden FromPersistence(
        GardenId id,
        UserId userId,
        string name,
        decimal totalSurfaceArea,
        int targetHumidityLevel,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        bool deleted,
        DateTimeOffset? deletedAt,
        IEnumerable<Plant>? plants = null)
    {
        return new Garden(
            id,
            userId,
            name,
            new SurfaceArea(totalSurfaceArea),
            new HumidityLevel(targetHumidityLevel),
            createdAt,
            updatedAt,
            deleted,
            deletedAt,
            plants ?? Array.Empty<Plant>());
    }

    public static Result<Garden> Create(
        UserId userId,
        string name,
        decimal totalSurfaceArea,
        int targetHumidityLevel,
        DateTimeOffset utcNow)
    {
        var validationErrors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(name))
        {
            validationErrors["name"] = ["Name is required."];
        }

        if (totalSurfaceArea <= 0)
        {
            validationErrors["totalSurfaceArea"] = ["Total surface area must be greater than zero."];
        }

        if (targetHumidityLevel is < 0 or > 100)
        {
            validationErrors["targetHumidityLevel"] = ["Target humidity level must be between 0 and 100."];
        }

        if (validationErrors.Count > 0)
        {
            return Result<Garden>.Failure(
                ErrorCodes.GardenValidationFailed,
                "One or more validation errors occurred while creating a garden.",
                validationErrors);
        }

        var garden = new Garden(
            GardenId.New(),
            userId,
            name.Trim(),
            new SurfaceArea(totalSurfaceArea),
            new HumidityLevel(targetHumidityLevel),
            utcNow,
            utcNow,
            isDeleted: false,
            deletedAt: null,
            plants: Array.Empty<Plant>());

        garden.RaiseDomainEvent(new GardenCreatedDomainEvent(garden, utcNow));

        return Result<Garden>.Success(garden);
    }

    public Result Rename(string name, DateTimeOffset utcNow)
    {
        var validationErrors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(name))
        {
            validationErrors["name"] = ["Name is required."];
        }

        if (validationErrors.Count > 0)
        {
            return Result.Failure(
                ErrorCodes.GardenValidationFailed,
                "One or more validation errors occurred while renaming a garden.",
                validationErrors);
        }

        var trimmedName = name.Trim();

        if (string.Equals(trimmedName, Name, StringComparison.Ordinal))
        {
            return Result.Success();
        }

        Name = trimmedName;
        UpdatedAt = utcNow;

        RaiseDomainEvent(new GardenRenamedDomainEvent(this, utcNow));

        return Result.Success();
    }

    public Result ChangeSurfaceArea(decimal totalSurfaceArea, DateTimeOffset utcNow)
    {
        if (totalSurfaceArea <= 0)
        {
            var validationErrors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["totalSurfaceArea"] = ["Total surface area must be greater than zero."]
            };

            return Result.Failure(
                ErrorCodes.GardenValidationFailed,
                "One or more validation errors occurred while changing a garden's surface area.",
                validationErrors);
        }

        if (totalSurfaceArea == TotalSurfaceArea.Value)
        {
            return Result.Success();
        }

        TotalSurfaceArea = new SurfaceArea(totalSurfaceArea);
        UpdatedAt = utcNow;

        RaiseDomainEvent(new GardenSurfaceAreaChangedDomainEvent(this, utcNow));

        return Result.Success();
    }

    public Result ChangeTargetHumidity(int targetHumidityLevel, DateTimeOffset utcNow)
    {
        if (targetHumidityLevel is < 0 or > 100)
        {
            var validationErrors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["targetHumidityLevel"] = ["Target humidity level must be between 0 and 100."]
            };

            return Result.Failure(
                ErrorCodes.GardenValidationFailed,
                "One or more validation errors occurred while changing a garden's target humidity.",
                validationErrors);
        }

        if (targetHumidityLevel == TargetHumidityLevel.Value)
        {
            return Result.Success();
        }

        TargetHumidityLevel = new HumidityLevel(targetHumidityLevel);
        UpdatedAt = utcNow;

        RaiseDomainEvent(new GardenTargetHumidityChangedDomainEvent(this, utcNow));

        return Result.Success();
    }

    public Result<Plant> AddPlant(
        string name,
        string species,
        PlantType type,
        DateTimeOffset plantationDate,
        decimal surfaceAreaRequired,
        int idealHumidityLevel,
        DateTimeOffset utcNow)
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
                ErrorCodes.GardenValidationFailed,
                "One or more validation errors occurred while adding a plant to a garden.",
                validationErrors);
        }

        var currentTotalSurfaceArea = _plants.Sum(p => p.SurfaceAreaRequired.Value);
        var newTotalSurfaceArea = currentTotalSurfaceArea + surfaceAreaRequired;

        if (newTotalSurfaceArea > TotalSurfaceArea.Value)
        {
            var surfaceAreaErrors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["surfaceAreaRequired"] = ["Adding this plant would exceed the garden's total surface area."]
            };

            return Result<Plant>.Failure(
                ErrorCodes.GardenValidationFailed,
                "Surface area constraint violated while adding a plant to a garden.",
                surfaceAreaErrors);
        }

        var plant = new Plant(
            PlantId.New(),
            name.Trim(),
            species.Trim(),
            type,
            plantationDate,
            new SurfaceArea(surfaceAreaRequired),
            new HumidityLevel(idealHumidityLevel));

        _plants.Add(plant);
        UpdatedAt = utcNow;

        RaiseDomainEvent(new PlantAddedToGardenDomainEvent(this, plant, utcNow));

        return Result<Plant>.Success(plant);
    }

    public Result RenamePlant(PlantId plantId, string name, DateTimeOffset utcNow)
    {
        var plant = _plants.FirstOrDefault(p => p.Id == plantId);
        if (plant is null)
            return Result.Success();

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(
                ErrorCodes.PlantValidationFailed,
                "One or more validation errors occurred while renaming a plant.",
                new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase) { ["name"] = ["Name is required."] });
        }

        var trimmedName = name.Trim();
        if (string.Equals(trimmedName, plant.Name, StringComparison.Ordinal))
            return Result.Success();

        plant.Name = trimmedName;
        UpdatedAt = utcNow;
        RaiseDomainEvent(new PlantRenamedDomainEvent(this, plant, utcNow));
        return Result.Success();
    }

    public Result ReclassifyPlant(PlantId plantId, string species, PlantType type, DateTimeOffset utcNow)
    {
        var plant = _plants.FirstOrDefault(p => p.Id == plantId);
        if (plant is null)
            return Result.Success();

        if (string.IsNullOrWhiteSpace(species))
        {
            return Result.Failure(
                ErrorCodes.PlantValidationFailed,
                "One or more validation errors occurred while reclassifying a plant.",
                new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase) { ["species"] = ["Species is required."] });
        }

        var trimmedSpecies = species.Trim();
        var speciesChanged = !string.Equals(trimmedSpecies, plant.Species, StringComparison.Ordinal);
        var typeChanged = plant.Type != type;
        if (!speciesChanged && !typeChanged)
            return Result.Success();

        plant.Species = trimmedSpecies;
        plant.Type = type;
        UpdatedAt = utcNow;
        RaiseDomainEvent(new PlantReclassifiedDomainEvent(this, plant, utcNow));
        return Result.Success();
    }

    public Result DefineSurfaceAreaRequirement(PlantId plantId, decimal surfaceAreaRequired, DateTimeOffset utcNow)
    {
        var plant = _plants.FirstOrDefault(p => p.Id == plantId);
        if (plant is null)
            return Result.Success();

        if (surfaceAreaRequired <= 0)
        {
            return Result.Failure(
                ErrorCodes.PlantValidationFailed,
                "Surface area required must be greater than zero.",
                new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase) { ["surfaceAreaRequired"] = ["Surface area required must be greater than zero."] });
        }

        if (plant.SurfaceAreaRequired.Value == surfaceAreaRequired)
            return Result.Success();

        var currentTotalSurfaceArea = _plants.Sum(p => p.SurfaceAreaRequired.Value);
        var adjustedTotalSurfaceArea = currentTotalSurfaceArea - plant.SurfaceAreaRequired.Value + surfaceAreaRequired;
        if (adjustedTotalSurfaceArea > TotalSurfaceArea.Value)
        {
            return Result.Failure(
                ErrorCodes.PlantValidationFailed,
                "Updating this plant would exceed the garden's total surface area.",
                new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase) { ["surfaceAreaRequired"] = ["Updating this plant would exceed the garden's total surface area."] });
        }

        plant.SurfaceAreaRequired = new SurfaceArea(surfaceAreaRequired);
        UpdatedAt = utcNow;
        RaiseDomainEvent(new PlantSurfaceAreaRequirementChangedDomainEvent(this, plant, utcNow));
        return Result.Success();
    }

    public Result AdjustIdealHumidity(PlantId plantId, int idealHumidityLevel, DateTimeOffset utcNow)
    {
        var plant = _plants.FirstOrDefault(p => p.Id == plantId);
        if (plant is null)
            return Result.Success();

        if (idealHumidityLevel is < 0 or > 100)
        {
            return Result.Failure(
                ErrorCodes.PlantValidationFailed,
                "Ideal humidity level must be between 0 and 100.",
                new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase) { ["idealHumidityLevel"] = ["Ideal humidity level must be between 0 and 100."] });
        }

        if (plant.IdealHumidityLevel.Value == idealHumidityLevel)
            return Result.Success();

        plant.IdealHumidityLevel = new HumidityLevel(idealHumidityLevel);
        UpdatedAt = utcNow;
        RaiseDomainEvent(new PlantIdealHumidityLevelChangedDomainEvent(this, plant, utcNow));
        return Result.Success();
    }

    public Result SetPlantationDate(PlantId plantId, DateTimeOffset plantationDate, DateTimeOffset utcNow)
    {
        var plant = _plants.FirstOrDefault(p => p.Id == plantId);
        if (plant is null)
            return Result.Success();

        if (plant.PlantationDate == plantationDate)
            return Result.Success();

        if(plantationDate >= utcNow)
            {
            return Result.Failure(
                ErrorCodes.PlantValidationFailed,
                "Plantation date must be in the past.",
                new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase) { ["plantationDate"] = ["Plantation date must be in the past."] });
        }

        plant.PlantationDate = plantationDate;
        UpdatedAt = utcNow;
        RaiseDomainEvent(new PlantPlantationDateChangedDomainEvent(this, plant, utcNow));
        return Result.Success();
    }

    public Result RemovePlant(PlantId plantId, DateTimeOffset utcNow)
    {
        var plant = _plants.FirstOrDefault(p => p.Id == plantId);

        if (plant is null)
        {
            return Result.Success();
        }

        _plants.Remove(plant);
        UpdatedAt = utcNow;

        RaiseDomainEvent(new PlantRemovedFromGardenDomainEvent(this, plant, utcNow));

        return Result.Success();
    }

    public Result MarkDeleted(DateTimeOffset utcNow)
    {
        if (IsDeleted)
        {
            return Result.Success();
        }

        IsDeleted = true;
        DeletedAt = utcNow;
        UpdatedAt = utcNow;

        RaiseDomainEvent(new GardenDeletedDomainEvent(this, utcNow));
        return Result.Success();
    }
}

