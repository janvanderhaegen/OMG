using OMG.Telemetrics.Domain.Common;

namespace OMG.Telemetrics.Domain.Hydration;

public sealed class PlantHydrationState : AggregateRoot
{
    private PlantHydrationState(
        Guid plantId,
        string meterId,
        PlantType plantType,
        int idealHumidityLevel,
        int currentHumidityLevel,
        DateTimeOffset? lastIrrigationStart,
        DateTimeOffset? lastIrrigationEnd,
        bool isWatering,
        bool hasIrrigationLine,
        WateringSession? activeSession)
    {
        PlantId = plantId; 
        MeterId = meterId;
        PlantType = plantType;
        IdealHumidityLevel = idealHumidityLevel;
        CurrentHumidityLevel = currentHumidityLevel;
        LastIrrigationStart = lastIrrigationStart;
        LastIrrigationEnd = lastIrrigationEnd;
        IsWatering = isWatering;
        HasIrrigationLine = hasIrrigationLine;
        ActiveSession = activeSession;
    }

    public Guid PlantId { get; }

    public string MeterId { get; }

    public PlantType PlantType { get; private set; }

    public int IdealHumidityLevel { get; private set; }

    public int CurrentHumidityLevel { get; private set; }

    public DateTimeOffset? LastIrrigationStart { get; private set; }

    public DateTimeOffset? LastIrrigationEnd { get; private set; }

    public bool IsWatering { get; private set; }

    public bool HasIrrigationLine { get; private set; }

    public WateringSession? ActiveSession { get; private set; }

    public static Result<PlantHydrationState> InitializeFromPlantAdded(
        Guid plantId,
        string meterId,
        PlantType plantType,
        int idealHumidityLevel)
    {
        var validationErrors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (idealHumidityLevel is < 0 or > 100)
        {
            validationErrors["idealHumidityLevel"] = ["Ideal humidity level must be between 0 and 100."];
        }

        if (validationErrors.Count > 0)
        {
            return Result<PlantHydrationState>.Failure(
                ErrorCodes.HydrationValidationFailed,
                "One or more validation errors occurred while initializing plant hydration state.",
                validationErrors);
        }

        var state = new PlantHydrationState(
            plantId,
            meterId,
            plantType,
            idealHumidityLevel,
            HydrationConstants.InitialHumidityPercent,
            lastIrrigationStart: null,
            lastIrrigationEnd: null,
            isWatering: false,
            hasIrrigationLine: false,
            activeSession: null);

        return Result<PlantHydrationState>.Success(state);
    }

    public static PlantHydrationState FromPersistence(
        Guid plantId, 
        string meterId,
        PlantType plantType,
        int idealHumidityLevel,
        int currentHumidityLevel,
        DateTimeOffset? lastIrrigationStart,
        DateTimeOffset? lastIrrigationEnd,
        bool isWatering,
        bool hasIrrigationLine,
        WateringSession? activeSession)
    {
        return new PlantHydrationState(
            plantId,
            meterId,
            plantType,
            idealHumidityLevel,
            currentHumidityLevel,
            lastIrrigationStart,
            lastIrrigationEnd,
            isWatering,
            hasIrrigationLine,
            activeSession);
    }

    public Result RegisterCurrentHumidity(int currentHumidityLevel)
    {
        if (currentHumidityLevel is < 0 or > 100)
        {
            return Result.Failure(
                ErrorCodes.HydrationValidationFailed,
                "Current humidity level must be between 0 and 100.");
        }

        CurrentHumidityLevel = currentHumidityLevel;
        return Result.Success();
    }

    public Result StartWatering(DateTimeOffset utcNow)
    {
        if (IsWatering)
        {
            return Result.Failure(
                ErrorCodes.HydrationValidationFailed,
                "Plant is already being watered.");
        }

        IsWatering = true;
        LastIrrigationStart = utcNow;

        var sessionId = Guid.NewGuid();
        var endsAt = utcNow.AddMinutes(HydrationConstants.WateringDurationMinutes);
        ActiveSession = new WateringSession(sessionId, PlantId, utcNow, endsAt);

        RaiseDomainEvent(new WateringStartedDomainEvent(
            sessionId,
            PlantId,
            MeterId,
            utcNow,
            utcNow));

        return Result.Success();
    }

    public Result CompleteWatering(DateTimeOffset utcNow)
    {
        if (ActiveSession is null)
        {
            return Result.Failure(
                ErrorCodes.HydrationValidationFailed,
                "No active watering session to complete.");
        }

        if (utcNow < ActiveSession.EndsAt)
        {
            return Result.Failure(
                ErrorCodes.HydrationValidationFailed,
                "Watering session is not yet due to complete.");
        }

        var increase = PlantType switch
        {
            PlantType.Vegetable => HydrationConstants.VegetableWateringIncrease,
            PlantType.Fruit => HydrationConstants.FruitWateringIncrease,
            PlantType.Flower => HydrationConstants.FlowerWateringIncrease,
            _ => HydrationConstants.VegetableWateringIncrease
        };

        CurrentHumidityLevel = Math.Min(100, CurrentHumidityLevel + increase);

        IsWatering = false;
        LastIrrigationEnd = utcNow;

        var session = ActiveSession;
        ActiveSession = null;

        RaiseDomainEvent(new WateringCompletedDomainEvent(
            session.SessionId,
            PlantId,
            MeterId,
            CurrentHumidityLevel,
            utcNow));

        return Result.Success();
    }

    /// <summary>
    /// Completes the current watering session (e.g. when hardware reports watering stopped).
    /// Allows early completion even when utcNow &lt; ActiveSession.EndsAt.
    /// </summary>
    public Result StopWatering(DateTimeOffset utcNow)
    {
        if (ActiveSession is null)
        {
            return Result.Success();
        }

        var increase = PlantType switch
        {
            PlantType.Vegetable => HydrationConstants.VegetableWateringIncrease,
            PlantType.Fruit => HydrationConstants.FruitWateringIncrease,
            PlantType.Flower => HydrationConstants.FlowerWateringIncrease,
            _ => HydrationConstants.VegetableWateringIncrease
        };

        CurrentHumidityLevel = Math.Min(100, CurrentHumidityLevel + increase);

        IsWatering = false;
        LastIrrigationEnd = utcNow;

        var session = ActiveSession;
        ActiveSession = null;

        RaiseDomainEvent(new WateringCompletedDomainEvent(
            session.SessionId,
            PlantId,
            MeterId,
            CurrentHumidityLevel,
            utcNow));

        return Result.Success();
    }

    public void AttachIrrigationLine()
    {
        HasIrrigationLine = true;
    }

    public void DetachIrrigationLine()
    {
        HasIrrigationLine = false;
    }

    public void UpdateIdealHumidityLevel(int idealHumidityLevel)
    {
        IdealHumidityLevel = idealHumidityLevel;
    }

    public void UpdatePlantType(PlantType plantType)
    {
        PlantType = plantType;
    }
}

