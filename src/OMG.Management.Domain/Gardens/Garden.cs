using OMG.Management.Domain.Abstractions;
using OMG.Management.Domain.Common;

namespace OMG.Management.Domain.Gardens;

public sealed class Garden : AggregateRoot
{
    private Garden(
        GardenId id,
        UserId userId,
        string name,
        SurfaceArea totalSurfaceArea,
        HumidityLevel targetHumidityLevel,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        UserId = userId;
        Name = name;
        TotalSurfaceArea = totalSurfaceArea;
        TargetHumidityLevel = targetHumidityLevel;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
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

    public static Garden FromPersistence(
        GardenId id,
        UserId userId,
        string name,
        decimal totalSurfaceArea,
        int targetHumidityLevel,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        bool deleted,
        DateTimeOffset? deletedAt)
    {
        return new Garden(
            id,
            userId,
            name,
            new SurfaceArea(totalSurfaceArea),
            new HumidityLevel(targetHumidityLevel),
            createdAt,
            updatedAt);
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
            utcNow);

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

