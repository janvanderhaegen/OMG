using OMG.Management.Domain.Gardens;

namespace OMG.Management.Domain.Tests;

public class GardenTests
{
    [Fact]
    public void Create_Fails_When_Name_Is_Empty()
    {
        var result = Garden.Create(
            new UserId(Guid.NewGuid()),
            string.Empty,
            totalSurfaceArea: 10,
            targetHumidityLevel: 50,
            utcNow: DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal("Garden.ValidationFailed", result.Error!.Code);
        Assert.Contains("name", result.Error.ValidationErrors!.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_Fails_When_SurfaceArea_Is_Non_Positive()
    {
        var result = Garden.Create(
            new UserId(Guid.NewGuid()),
            "My Garden",
            totalSurfaceArea: 0,
            targetHumidityLevel: 50,
            utcNow: DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Contains("totalSurfaceArea", result.Error!.ValidationErrors!.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Create_Fails_When_TargetHumidity_Is_Out_Of_Range(int humidity)
    {
        var result = Garden.Create(
            new UserId(Guid.NewGuid()),
            "My Garden",
            totalSurfaceArea: 10,
            targetHumidityLevel: humidity,
            utcNow: DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Contains("targetHumidityLevel", result.Error!.ValidationErrors!.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_Succeeds_And_Raises_DomainEvent()
    {
        var now = DateTimeOffset.UtcNow;

        var result = Garden.Create(
            new UserId(Guid.NewGuid()),
            "My Garden",
            totalSurfaceArea: 10,
            targetHumidityLevel: 50,
            utcNow: now);

        Assert.True(result.IsSuccess);
        var garden = result.Value!;

        Assert.Equal("My Garden", garden.Name);
        Assert.Single(garden.DomainEvents);
        Assert.IsType<GardenCreatedDomainEvent>(garden.DomainEvents.Single());
    }

    [Fact]
    public void Rename_Succeeds_And_Raises_DomainEvent_When_Name_Changes()
    {
        var now = DateTimeOffset.UtcNow;

        var garden = Garden.Create(
                new UserId(Guid.NewGuid()),
                "My Garden",
                totalSurfaceArea: 10,
                targetHumidityLevel: 50,
                utcNow: now)
            .Value!;

        garden.ClearDomainEvents();

        var result = garden.Rename("Renamed Garden", now.AddMinutes(1));

        Assert.True(result.IsSuccess);
        Assert.Equal("Renamed Garden", garden.Name);
        var @event = Assert.Single(garden.DomainEvents);
        Assert.IsType<GardenRenamedDomainEvent>(@event);
    }

    [Fact]
    public void Rename_Does_Nothing_When_Name_Is_Identical()
    {
        var now = DateTimeOffset.UtcNow;

        var garden = Garden.Create(
                new UserId(Guid.NewGuid()),
                "My Garden",
                totalSurfaceArea: 10,
                targetHumidityLevel: 50,
                utcNow: now)
            .Value!;

        garden.ClearDomainEvents();

        var result = garden.Rename("My Garden", now.AddMinutes(1));

        Assert.True(result.IsSuccess);
        Assert.Empty(garden.DomainEvents);
    }

    [Fact]
    public void ChangeSurfaceArea_Succeeds_And_Raises_DomainEvent_When_Value_Changes()
    {
        var now = DateTimeOffset.UtcNow;

        var garden = Garden.Create(
                new UserId(Guid.NewGuid()),
                "My Garden",
                totalSurfaceArea: 10,
                targetHumidityLevel: 50,
                utcNow: now)
            .Value!;

        garden.ClearDomainEvents();

        var result = garden.ChangeSurfaceArea(20, now.AddMinutes(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(20, garden.TotalSurfaceArea.Value);
        var @event = Assert.Single(garden.DomainEvents);
        Assert.IsType<GardenSurfaceAreaChangedDomainEvent>(@event);
    }

    [Fact]
    public void ChangeSurfaceArea_Does_Nothing_When_Value_Is_Identical()
    {
        var now = DateTimeOffset.UtcNow;

        var garden = Garden.Create(
                new UserId(Guid.NewGuid()),
                "My Garden",
                totalSurfaceArea: 10,
                targetHumidityLevel: 50,
                utcNow: now)
            .Value!;

        garden.ClearDomainEvents();

        var result = garden.ChangeSurfaceArea(10, now.AddMinutes(1));

        Assert.True(result.IsSuccess);
        Assert.Empty(garden.DomainEvents);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void ChangeSurfaceArea_Fails_When_Value_Is_Non_Positive(decimal surfaceArea)
    {
        var now = DateTimeOffset.UtcNow;

        var garden = Garden.Create(
                new UserId(Guid.NewGuid()),
                "My Garden",
                totalSurfaceArea: 10,
                targetHumidityLevel: 50,
                utcNow: now)
            .Value!;

        garden.ClearDomainEvents();

        var result = garden.ChangeSurfaceArea(surfaceArea, now.AddMinutes(1));

        Assert.True(result.IsFailure);
        Assert.Equal("Garden.ValidationFailed", result.Error!.Code);
        Assert.Contains("totalSurfaceArea", result.Error.ValidationErrors!.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ChangeTargetHumidity_Succeeds_And_Raises_DomainEvent_When_Value_Changes()
    {
        var now = DateTimeOffset.UtcNow;

        var garden = Garden.Create(
                new UserId(Guid.NewGuid()),
                "My Garden",
                totalSurfaceArea: 10,
                targetHumidityLevel: 50,
                utcNow: now)
            .Value!;

        garden.ClearDomainEvents();

        var result = garden.ChangeTargetHumidity(60, now.AddMinutes(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(60, garden.TargetHumidityLevel.Value);
        var @event = Assert.Single(garden.DomainEvents);
        Assert.IsType<GardenTargetHumidityChangedDomainEvent>(@event);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void ChangeTargetHumidity_Fails_When_Value_Is_Out_Of_Range(int humidity)
    {
        var now = DateTimeOffset.UtcNow;

        var garden = Garden.Create(
                new UserId(Guid.NewGuid()),
                "My Garden",
                totalSurfaceArea: 10,
                targetHumidityLevel: 50,
                utcNow: now)
            .Value!;

        garden.ClearDomainEvents();

        var result = garden.ChangeTargetHumidity(humidity, now.AddMinutes(1));

        Assert.True(result.IsFailure);
        Assert.Equal("Garden.ValidationFailed", result.Error!.Code);
        Assert.Contains("targetHumidityLevel", result.Error.ValidationErrors!.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void MarkDeleted_Succeeds_And_Raises_DomainEvent_Once()
    {
        var now = DateTimeOffset.UtcNow;

        var garden = Garden.Create(
                new UserId(Guid.NewGuid()),
                "My Garden",
                totalSurfaceArea: 10,
                targetHumidityLevel: 50,
                utcNow: now)
            .Value!;

        garden.ClearDomainEvents();

        var first = garden.MarkDeleted(now.AddMinutes(1));
        var second = garden.MarkDeleted(now.AddMinutes(2));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.True(garden.IsDeleted);
        Assert.NotNull(garden.DeletedAt);
        var @event = Assert.Single(garden.DomainEvents);
        Assert.IsType<GardenDeletedDomainEvent>(@event);
    }
}

