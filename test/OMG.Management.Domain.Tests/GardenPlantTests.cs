using OMG.Management.Domain.Gardens;

namespace OMG.Management.Domain.Tests;

public class GardenPlantTests
{
    [Fact]
    public void AddPlant_Succeeds_And_Raises_DomainEvent_When_Within_SurfaceArea()
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

        var result = garden.AddPlant(
            name: "Tomato",
            species: "Solanum lycopersicum",
            type: PlantType.Vegetable,
            plantationDate: now.Date,
            surfaceAreaRequired: 5,
            idealHumidityLevel: 60,
            utcNow: now.AddMinutes(1));

        Assert.True(result.IsSuccess);
        var plant = result.Value!;
        Assert.Single(garden.Plants);
        Assert.Equal(plant.Id, garden.Plants.Single().Id);

        var @event = Assert.Single(garden.DomainEvents);
        Assert.IsType<PlantAddedToGardenDomainEvent>(@event);
    }

    [Fact]
    public void AddPlant_Fails_When_SurfaceArea_Constraint_Violated()
    {
        var now = DateTimeOffset.UtcNow;

        var garden = Garden.Create(
                new UserId(Guid.NewGuid()),
                "My Garden",
                totalSurfaceArea: 5,
                targetHumidityLevel: 50,
                utcNow: now)
            .Value!;

        garden.ClearDomainEvents();

        var first = garden.AddPlant(
            name: "Tomato",
            species: "Solanum lycopersicum",
            type: PlantType.Vegetable,
            plantationDate: now.Date,
            surfaceAreaRequired: 4,
            idealHumidityLevel: 60,
            utcNow: now.AddMinutes(1));

        Assert.True(first.IsSuccess);

        garden.ClearDomainEvents();

        var second = garden.AddPlant(
            name: "Cucumber",
            species: "Cucumis sativus",
            type: PlantType.Vegetable,
            plantationDate: now.Date,
            surfaceAreaRequired: 3,
            idealHumidityLevel: 60,
            utcNow: now.AddMinutes(2));

        Assert.True(second.IsFailure);
        Assert.Equal("Garden.ValidationFailed", second.Error!.Code);
        Assert.Contains("surfaceAreaRequired", second.Error.ValidationErrors!.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Empty(garden.DomainEvents);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddPlant_Fails_When_SurfaceAreaRequired_Is_Non_Positive(decimal surfaceArea)
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

        var result = garden.AddPlant(
            name: "Tomato",
            species: "Solanum lycopersicum",
            type: PlantType.Vegetable,
            plantationDate: now.Date,
            surfaceAreaRequired: surfaceArea,
            idealHumidityLevel: 60,
            utcNow: now.AddMinutes(1));

        Assert.True(result.IsFailure);
        Assert.Equal("Garden.ValidationFailed", result.Error!.Code);
        Assert.Contains("surfaceAreaRequired", result.Error.ValidationErrors!.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void AddPlant_Fails_When_IdealHumidity_Is_Out_Of_Range(int humidity)
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

        var result = garden.AddPlant(
            name: "Tomato",
            species: "Solanum lycopersicum",
            type: PlantType.Vegetable,
            plantationDate: now.Date,
            surfaceAreaRequired: 5,
            idealHumidityLevel: humidity,
            utcNow: now.AddMinutes(1));

        Assert.True(result.IsFailure);
        Assert.Equal("Garden.ValidationFailed", result.Error!.Code);
        Assert.Contains("idealHumidityLevel", result.Error.ValidationErrors!.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void RenamePlant_Succeeds_And_Raises_PlantRenamedDomainEvent_When_Name_Changes()
    {
        var now = DateTimeOffset.UtcNow;
        var garden = Garden.Create(new UserId(Guid.NewGuid()), "My Garden", 10, 50, now).Value!;
        var plant = garden.AddPlant("Tomato", "Solanum lycopersicum", PlantType.Vegetable, now.Date, 5, 60, now.AddMinutes(1)).Value!;
        garden.ClearDomainEvents();

        var result = garden.RenamePlant(plant.Id, "Cherry Tomato", now.AddMinutes(2));

        Assert.True(result.IsSuccess);
        Assert.Equal("Cherry Tomato", plant.Name);
        var evt = Assert.Single(garden.DomainEvents);
        Assert.IsType<PlantRenamedDomainEvent>(evt);
    }

    [Fact]
    public void RenamePlant_Does_Nothing_When_Name_Unchanged()
    {
        var now = DateTimeOffset.UtcNow;
        var garden = Garden.Create(new UserId(Guid.NewGuid()), "My Garden", 10, 50, now).Value!;
        var plant = garden.AddPlant("Tomato", "Solanum lycopersicum", PlantType.Vegetable, now.Date, 5, 60, now.AddMinutes(1)).Value!;
        garden.ClearDomainEvents();

        var result = garden.RenamePlant(plant.Id, "Tomato", now.AddMinutes(2));

        Assert.True(result.IsSuccess);
        Assert.Empty(garden.DomainEvents);
    }

    [Fact]
    public void RenamePlant_Fails_When_Name_Empty()
    {
        var now = DateTimeOffset.UtcNow;
        var garden = Garden.Create(new UserId(Guid.NewGuid()), "My Garden", 10, 50, now).Value!;
        var plant = garden.AddPlant("Tomato", "Solanum lycopersicum", PlantType.Vegetable, now.Date, 5, 60, now.AddMinutes(1)).Value!;
        garden.ClearDomainEvents();

        var result = garden.RenamePlant(plant.Id, "  ", now.AddMinutes(2));

        Assert.True(result.IsFailure);
        Assert.Contains("name", result.Error!.ValidationErrors!.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReclassifyPlant_Succeeds_And_Raises_PlantReclassifiedDomainEvent_When_Species_Or_Type_Change()
    {
        var now = DateTimeOffset.UtcNow;
        var garden = Garden.Create(new UserId(Guid.NewGuid()), "My Garden", 10, 50, now).Value!;
        var plant = garden.AddPlant("Tomato", "Solanum lycopersicum", PlantType.Vegetable, now.Date, 5, 60, now.AddMinutes(1)).Value!;
        garden.ClearDomainEvents();

        var result = garden.ReclassifyPlant(plant.Id, "Solanum lycopersicum var. cerasiforme", PlantType.Fruit, now.AddMinutes(2));

        Assert.True(result.IsSuccess);
        Assert.Equal("Solanum lycopersicum var. cerasiforme", plant.Species);
        Assert.Equal(PlantType.Fruit, plant.Type);
        var evt = Assert.Single(garden.DomainEvents);
        Assert.IsType<PlantReclassifiedDomainEvent>(evt);
    }

    [Fact]
    public void ReclassifyPlant_Does_Nothing_When_Species_And_Type_Unchanged()
    {
        var now = DateTimeOffset.UtcNow;
        var garden = Garden.Create(new UserId(Guid.NewGuid()), "My Garden", 10, 50, now).Value!;
        var plant = garden.AddPlant("Tomato", "Solanum lycopersicum", PlantType.Vegetable, now.Date, 5, 60, now.AddMinutes(1)).Value!;
        garden.ClearDomainEvents();

        var result = garden.ReclassifyPlant(plant.Id, plant.Species, plant.Type, now.AddMinutes(2));

        Assert.True(result.IsSuccess);
        Assert.Empty(garden.DomainEvents);
    }

    [Fact]
    public void DefineSurfaceAreaRequirement_Succeeds_And_Raises_Event_When_Value_Changes()
    {
        var now = DateTimeOffset.UtcNow;
        var garden = Garden.Create(new UserId(Guid.NewGuid()), "My Garden", 10, 50, now).Value!;
        var plant = garden.AddPlant("Tomato", "Solanum lycopersicum", PlantType.Vegetable, now.Date, 5, 60, now.AddMinutes(1)).Value!;
        garden.ClearDomainEvents();

        var result = garden.DefineSurfaceAreaRequirement(plant.Id, 6, now.AddMinutes(2));

        Assert.True(result.IsSuccess);
        Assert.Equal(6, plant.SurfaceAreaRequired.Value);
        var evt = Assert.Single(garden.DomainEvents);
        Assert.IsType<PlantSurfaceAreaRequirementChangedDomainEvent>(evt);
    }

    [Fact]
    public void DefineSurfaceAreaRequirement_Fails_When_SurfaceArea_Constraint_Violated()
    {
        var now = DateTimeOffset.UtcNow;
        var garden = Garden.Create(new UserId(Guid.NewGuid()), "My Garden", 10, 50, now).Value!;
        var first = garden.AddPlant("Tomato", "Solanum lycopersicum", PlantType.Vegetable, now.Date, 6, 60, now.AddMinutes(1)).Value!;
        garden.AddPlant("Cucumber", "Cucumis sativus", PlantType.Vegetable, now.Date, 4, 60, now.AddMinutes(2));
        garden.ClearDomainEvents();

        var result = garden.DefineSurfaceAreaRequirement(first.Id, 7, now.AddMinutes(3));

        Assert.True(result.IsFailure);
        Assert.Contains("surfaceAreaRequired", result.Error!.ValidationErrors!.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Empty(garden.DomainEvents);
    }

    [Fact]
    public void AdjustIdealHumidity_Succeeds_And_Raises_Event_When_Value_Changes()
    {
        var now = DateTimeOffset.UtcNow;
        var garden = Garden.Create(new UserId(Guid.NewGuid()), "My Garden", 10, 50, now).Value!;
        var plant = garden.AddPlant("Tomato", "Solanum lycopersicum", PlantType.Vegetable, now.Date, 5, 60, now.AddMinutes(1)).Value!;
        garden.ClearDomainEvents();

        var result = garden.AdjustIdealHumidity(plant.Id, 65, now.AddMinutes(2));

        Assert.True(result.IsSuccess);
        Assert.Equal(65, plant.IdealHumidityLevel.Value);
        var evt = Assert.Single(garden.DomainEvents);
        Assert.IsType<PlantIdealHumidityLevelChangedDomainEvent>(evt);
    }

    [Fact]
    public void AdjustIdealHumidity_Fails_When_Out_Of_Range()
    {
        var now = DateTimeOffset.UtcNow;
        var garden = Garden.Create(new UserId(Guid.NewGuid()), "My Garden", 10, 50, now).Value!;
        var plant = garden.AddPlant("Tomato", "Solanum lycopersicum", PlantType.Vegetable, now.Date, 5, 60, now.AddMinutes(1)).Value!;
        garden.ClearDomainEvents();

        var result = garden.AdjustIdealHumidity(plant.Id, 101, now.AddMinutes(2));

        Assert.True(result.IsFailure);
        Assert.Contains("idealHumidityLevel", result.Error!.ValidationErrors!.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void RemovePlant_Succeeds_And_Raises_DomainEvent_When_Plant_Exists()
    {
        var now = DateTimeOffset.UtcNow;

        var garden = Garden.Create(
                new UserId(Guid.NewGuid()),
                "My Garden",
                totalSurfaceArea: 10,
                targetHumidityLevel: 50,
                utcNow: now)
            .Value!;

        var plantResult = garden.AddPlant(
            name: "Tomato",
            species: "Solanum lycopersicum",
            type: PlantType.Vegetable,
            plantationDate: now.Date,
            surfaceAreaRequired: 5,
            idealHumidityLevel: 60,
            utcNow: now.AddMinutes(1));

        var plant = plantResult.Value!;

        garden.ClearDomainEvents();

        var removeResult = garden.RemovePlant(plant.Id, now.AddMinutes(2));

        Assert.True(removeResult.IsSuccess);
        Assert.Empty(garden.Plants);

        var @event = Assert.Single(garden.DomainEvents);
        Assert.IsType<PlantRemovedFromGardenDomainEvent>(@event);
    }

    [Fact]
    public void RemovePlant_Does_Nothing_When_Plant_Does_Not_Exist()
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

        var removeResult = garden.RemovePlant(new PlantId(Guid.NewGuid()), now.AddMinutes(1));

        Assert.True(removeResult.IsSuccess);
        Assert.Empty(garden.DomainEvents);
    }
}

