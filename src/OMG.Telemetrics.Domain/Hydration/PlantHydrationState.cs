using OMG.Telemetrics.Domain.Abstractions;
using OMG.Telemetrics.Domain.Common;

namespace OMG.Telemetrics.Domain.Hydration;

public sealed class PlantHydrationState
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public Guid PlantId { get; }
    public Guid GardenId { get; }
    public PlantType PlantType { get; private set; }
    public int IdealHumidityLevel { get; private set; }
    public int CurrentHumidityLevel { get; private set; }
    public DateTimeOffset? LastIrrigationStart { get; private set; }
    public DateTimeOffset? LastIrrigationEnd { get; private set; }
    public bool IsWatering { get; private set; }
    public bool HasIrrigationLine { get; private set; }

    /// <summary>Active watering session when IsWatering is true.</summary>
    public WateringSession? ActiveSession { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private PlantHydrationState(
        Guid plantId,
        Guid gardenId,
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
        GardenId = gardenId;
        PlantType = plantType;
        IdealHumidityLevel = idealHumidityLevel;
        CurrentHumidityLevel = Math.Clamp(currentHumidityLevel, 0, 100);
        LastIrrigationStart = lastIrrigationStart;
        LastIrrigationEnd = lastIrrigationEnd;
        IsWatering = isWatering;
        HasIrrigationLine = hasIrrigationLine;
        ActiveSession = activeSession;
    }

    /// <summary>Create initial hydration state when a plant is added (from Management event).</summary>
    public static Result<PlantHydrationState> InitializeFromPlantAdded(
        Guid plantId,
        Guid gardenId,
        PlantType plantType,
        int idealHumidityLevel)
    {
        if (idealHumidityLevel is < 0 or > 100)
            return Result<PlantHydrationState>.Failure(
                ErrorCodes.HydrationValidationFailed,
                "Ideal humidity level must be between 0 and 100.",
                new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase) { ["idealHumidityLevel"] = ["Must be between 0 and 100."] });

        var state = new PlantHydrationState(
            plantId,
            gardenId,
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

    /// <summary>Reconstruct from persistence.</summary>
    public static PlantHydrationState FromPersistence(
        Guid plantId,
        Guid gardenId,
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
            gardenId,
            plantType,
            idealHumidityLevel,
            currentHumidityLevel,
            lastIrrigationStart,
            lastIrrigationEnd,
            isWatering,
            hasIrrigationLine,
            activeSession);
    }

    public void UpdateIdealHumidityLevel(int idealHumidityLevel)
    {
        if (idealHumidityLevel is < 0 or > 100) return;
        IdealHumidityLevel = idealHumidityLevel;
    }

    public void UpdatePlantType(PlantType plantType) => PlantType = plantType;

    public void AttachIrrigationLine() => HasIrrigationLine = true;

    public void DetachIrrigationLine() => HasIrrigationLine = false;

    /// <summary>Apply one minute of decay. Raises PlantNeedsWateringDomainEvent when below ideal and not already watering.</summary>
    public void ApplyMinuteTick(DateTimeOffset now)
    {
        if (IsWatering)
            return;

        var decay = HydrationConstants.DecayPercentPerMinute(PlantType);
        CurrentHumidityLevel = Math.Max(0, CurrentHumidityLevel - decay);

        if (CurrentHumidityLevel < IdealHumidityLevel)
        {
            _domainEvents.Add(new PlantNeedsWateringDomainEvent(
                PlantId,
                GardenId,
                CurrentHumidityLevel,
                IdealHumidityLevel,
                now));
        }
    }

    /// <summary>Start a watering session. Returns error if already watering.</summary>
    public Result<WateringSession> StartWatering(DateTimeOffset now)
    {
        if (IsWatering)
            return Result<WateringSession>.Failure(
                ErrorCodes.WateringSessionValidationFailed,
                "Cannot start watering: a session is already active.",
                new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase) { ["isWatering"] = ["Already watering."] });

        var session = WateringSession.Create(PlantId, GardenId, now);
        ActiveSession = session;
        IsWatering = true;
        LastIrrigationStart = now;

        _domainEvents.Add(new WateringStartedDomainEvent(
            session.SessionId,
            PlantId,
            GardenId,
            now,
            now));

        return Result<WateringSession>.Success(session);
    }

    /// <summary>Complete the active watering session and apply humidity increase. Returns error if no active session or not yet due.</summary>
    public Result CompleteWatering(DateTimeOffset now)
    {
        if (!IsWatering || ActiveSession is null)
            return Result.Failure(
                ErrorCodes.WateringSessionValidationFailed,
                "No active watering session to complete.");

        if (ActiveSession.EndsAt > now)
            return Result.Failure(
                ErrorCodes.WateringSessionValidationFailed,
                "Watering session has not yet reached its end time.");

        var increase = HydrationConstants.WateringIncreasePercent(PlantType);
        CurrentHumidityLevel = Math.Min(100, CurrentHumidityLevel + increase);
        LastIrrigationEnd = now;
        IsWatering = false;

        _domainEvents.Add(new WateringCompletedDomainEvent(
            ActiveSession.SessionId,
            PlantId,
            GardenId,
            CurrentHumidityLevel,
            now));

        ActiveSession.MarkCompleted();
        ActiveSession = null;

        return Result.Success();
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}
