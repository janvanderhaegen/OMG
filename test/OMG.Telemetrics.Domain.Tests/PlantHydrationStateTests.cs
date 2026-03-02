using OMG.Telemetrics.Domain.Hydration;

namespace OMG.Telemetrics.Domain.Tests;

public class PlantHydrationStateTests
{
    [Fact]
    public void InitializeFromPlantAdded_Succeeds_With_50_Percent_Humidity()
    {
        var plantId = Guid.NewGuid();
        var gardenId = Guid.NewGuid();

        var result = PlantHydrationState.InitializeFromPlantAdded(
            plantId,
            gardenId,
            PlantType.Vegetable,
            idealHumidityLevel: 60);

        Assert.True(result.IsSuccess);
        var state = result.Value!;
        Assert.Equal(plantId, state.PlantId);
        Assert.Equal(gardenId, state.GardenId);
        Assert.Equal(PlantType.Vegetable, state.PlantType);
        Assert.Equal(60, state.IdealHumidityLevel);
        Assert.Equal(HydrationConstants.InitialHumidityPercent, state.CurrentHumidityLevel);
        Assert.False(state.IsWatering);
        Assert.False(state.HasIrrigationLine);
        Assert.Null(state.LastIrrigationStart);
        Assert.Null(state.LastIrrigationEnd);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void InitializeFromPlantAdded_Fails_When_IdealHumidity_Out_Of_Range(int idealHumidity)
    {
        var result = PlantHydrationState.InitializeFromPlantAdded(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PlantType.Vegetable,
            idealHumidity);

        Assert.True(result.IsFailure);
        Assert.Contains("idealHumidityLevel", result.Error!.ValidationErrors!.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(PlantType.Vegetable, 1)]
    [InlineData(PlantType.Fruit, 3)]
    [InlineData(PlantType.Flower, 4)]
    public void ApplyMinuteTick_Reduces_Humidity_By_Type_Decay(PlantType plantType, int expectedDecay)
    {
        var state = PlantHydrationState.InitializeFromPlantAdded(
            Guid.NewGuid(),
            Guid.NewGuid(),
            plantType,
            idealHumidityLevel: 80).Value!;

        var before = state.CurrentHumidityLevel;
        state.ApplyMinuteTick(DateTimeOffset.UtcNow);
        Assert.Equal(before - expectedDecay, state.CurrentHumidityLevel);
    }

    [Fact]
    public void ApplyMinuteTick_Does_Not_Reduce_Below_Zero()
    {
        var state = PlantHydrationState.InitializeFromPlantAdded(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PlantType.Flower, // 4% per minute
            idealHumidityLevel: 5).Value!;

        for (var i = 0; i < 20; i++)
            state.ApplyMinuteTick(DateTimeOffset.UtcNow.AddMinutes(i));

        Assert.True(state.CurrentHumidityLevel >= 0);
    }

    [Fact]
    public void ApplyMinuteTick_Raises_PlantNeedsWatering_When_Below_Ideal_And_Not_Watering()
    {
        var state = PlantHydrationState.InitializeFromPlantAdded(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PlantType.Vegetable,
            idealHumidityLevel: 55).Value!;

        // 50% -> 49% after one tick (decay 1). Still above 55? No, 49 < 55. So we need to drop below 55.
        // Start at 50, ideal 55. After one tick: 49. 49 < 55 so we need watering.
        state.ApplyMinuteTick(DateTimeOffset.UtcNow);

        var evt = Assert.Single(state.DomainEvents);
        var needsWatering = Assert.IsType<PlantNeedsWateringDomainEvent>(evt);
        Assert.Equal(state.PlantId, needsWatering.PlantId);
        Assert.Equal(state.GardenId, needsWatering.GardenId);
        Assert.Equal(49, needsWatering.CurrentHumidityLevel);
        Assert.Equal(55, needsWatering.IdealHumidityLevel);
    }

    [Fact]
    public void ApplyMinuteTick_Does_Not_Raise_Event_When_Already_Watering()
    {
        var state = PlantHydrationState.InitializeFromPlantAdded(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PlantType.Vegetable,
            idealHumidityLevel: 60).Value!;

        state.StartWatering(DateTimeOffset.UtcNow);
        state.ClearDomainEvents();

        state.ApplyMinuteTick(DateTimeOffset.UtcNow);

        Assert.True(state.IsWatering);
        Assert.Empty(state.DomainEvents);
    }

    [Fact]
    public void StartWatering_Sets_IsWatering_And_Raises_WateringStarted()
    {
        var now = DateTimeOffset.UtcNow;
        var state = PlantHydrationState.InitializeFromPlantAdded(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PlantType.Vegetable,
            idealHumidityLevel: 60).Value!;

        var result = state.StartWatering(now);

        Assert.True(result.IsSuccess);
        Assert.True(state.IsWatering);
        Assert.NotNull(state.LastIrrigationStart);
        Assert.Equal(now, state.LastIrrigationStart);
        Assert.NotNull(state.ActiveSession);
        Assert.Equal(state.PlantId, state.ActiveSession!.PlantId);

        var evt = Assert.Single(state.DomainEvents);
        var started = Assert.IsType<WateringStartedDomainEvent>(evt);
        Assert.Equal(state.ActiveSession.SessionId, started.SessionId);
        Assert.Equal(state.PlantId, started.PlantId);
        Assert.Equal(now, started.StartedAt);
    }

    [Fact]
    public void StartWatering_Fails_When_Already_Watering()
    {
        var state = PlantHydrationState.InitializeFromPlantAdded(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PlantType.Vegetable,
            idealHumidityLevel: 60).Value!;

        state.StartWatering(DateTimeOffset.UtcNow);
        var second = state.StartWatering(DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.True(second.IsFailure);
        Assert.True(state.IsWatering);
    }

    [Fact]
    public void CompleteWatering_Increases_Humidity_By_Type_And_Clears_IsWatering()
    {
        var now = DateTimeOffset.UtcNow;
        var state = PlantHydrationState.InitializeFromPlantAdded(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PlantType.Vegetable, // +16%
            idealHumidityLevel: 60).Value!;

        state.StartWatering(now);
        state.ClearDomainEvents();
        var session = state.ActiveSession!;
        var endsAt = now.AddMinutes(HydrationConstants.WateringDurationMinutes);

        Assert.Equal(50, state.CurrentHumidityLevel);

        var completeResult = state.CompleteWatering(endsAt);

        Assert.True(completeResult.IsSuccess);
        Assert.False(state.IsWatering);
        Assert.Null(state.ActiveSession);
        Assert.Equal(50 + 16, state.CurrentHumidityLevel);
        Assert.NotNull(state.LastIrrigationEnd);
        Assert.Equal(endsAt, state.LastIrrigationEnd);

        var evt = Assert.Single(state.DomainEvents);
        var completed = Assert.IsType<WateringCompletedDomainEvent>(evt);
        Assert.Equal(session.SessionId, completed.SessionId);
        Assert.Equal(66, completed.NewHumidityLevel);
    }

    [Theory]
    [InlineData(PlantType.Vegetable, 16)]
    [InlineData(PlantType.Fruit, 18)]
    [InlineData(PlantType.Flower, 20)]
    public void CompleteWatering_Applies_Type_Specific_Increase(PlantType plantType, int expectedIncrease)
    {
        var now = DateTimeOffset.UtcNow;
        var state = PlantHydrationState.InitializeFromPlantAdded(
            Guid.NewGuid(),
            Guid.NewGuid(),
            plantType,
            idealHumidityLevel: 50).Value!;

        state.StartWatering(now);
        state.ClearDomainEvents();
        var endsAt = now.AddMinutes(HydrationConstants.WateringDurationMinutes);

        state.CompleteWatering(endsAt);

        Assert.Equal(HydrationConstants.InitialHumidityPercent + expectedIncrease, state.CurrentHumidityLevel);
    }

    [Fact]
    public void CompleteWatering_Caps_Humidity_At_100()
    {
        var now = DateTimeOffset.UtcNow;
        var state = PlantHydrationState.FromPersistence(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PlantType.Flower,
            idealHumidityLevel: 50,
            currentHumidityLevel: 95,
            lastIrrigationStart: null,
            lastIrrigationEnd: null,
            isWatering: false,
            hasIrrigationLine: true,
            activeSession: null);

        state.StartWatering(now);
        state.ClearDomainEvents();
        var endsAt = now.AddMinutes(HydrationConstants.WateringDurationMinutes);

        state.CompleteWatering(endsAt);

        Assert.Equal(100, state.CurrentHumidityLevel);
    }

    [Fact]
    public void CompleteWatering_Fails_When_No_Active_Session()
    {
        var state = PlantHydrationState.InitializeFromPlantAdded(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PlantType.Vegetable,
            idealHumidityLevel: 60).Value!;

        var result = state.CompleteWatering(DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void CompleteWatering_Fails_When_Session_Not_Yet_Due()
    {
        var now = DateTimeOffset.UtcNow;
        var state = PlantHydrationState.InitializeFromPlantAdded(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PlantType.Vegetable,
            idealHumidityLevel: 60).Value!;

        state.StartWatering(now);
        var result = state.CompleteWatering(now.AddMinutes(1)); // Only 1 minute passed, need 2

        Assert.True(result.IsFailure);
        Assert.True(state.IsWatering);
    }

    [Fact]
    public void AttachIrrigationLine_Sets_HasIrrigationLine()
    {
        var state = PlantHydrationState.InitializeFromPlantAdded(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PlantType.Vegetable,
            idealHumidityLevel: 60).Value!;

        Assert.False(state.HasIrrigationLine);
        state.AttachIrrigationLine();
        Assert.True(state.HasIrrigationLine);
        state.DetachIrrigationLine();
        Assert.False(state.HasIrrigationLine);
    }

    [Fact]
    public void UpdateIdealHumidityLevel_Updates_Value()
    {
        var state = PlantHydrationState.InitializeFromPlantAdded(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PlantType.Vegetable,
            idealHumidityLevel: 60).Value!;

        state.UpdateIdealHumidityLevel(70);
        Assert.Equal(70, state.IdealHumidityLevel);
    }

    [Fact]
    public void UpdatePlantType_Updates_Type()
    {
        var state = PlantHydrationState.InitializeFromPlantAdded(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PlantType.Vegetable,
            idealHumidityLevel: 60).Value!;

        state.UpdatePlantType(PlantType.Fruit);
        Assert.Equal(PlantType.Fruit, state.PlantType);
    }
}
