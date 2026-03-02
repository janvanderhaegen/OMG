using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OMG.Api.Management.Models;
using OMG.Management.Infrastructure;
using OMG.Management.Infrastructure.Entities;

namespace OMG.Api.Tests;

public class PlantManagementEndpointTests : IClassFixture<ManagementApiFactory>
{
    private readonly ManagementApiFactory _factory;
    private readonly HttpClient _client;

    public PlantManagementEndpointTests(ManagementApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetPlantsForGarden_ReturnsPlants()
    {
        var userId = Guid.NewGuid();
        var gardenId = Guid.NewGuid();
        var plantId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();

            db.Gardens.Add(new GardenEntity
            {
                Id = gardenId,
                UserId = userId,
                Name = "Test Garden",
                TotalSurfaceArea = 10,
                TargetHumidityLevel = 50,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Deleted = false
            });

            db.Plants.Add(new PlantEntity
            {
                Id = plantId,
                GardenId = gardenId,
                Name = "Tomato",
                Species = "Solanum lycopersicum",
                Type = "Vegetable",
                PlantationDate = DateTimeOffset.UtcNow.Date,
                SurfaceAreaRequired = 5,
                IdealHumidityLevel = 60
            });

            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/v1/management/gardens/{gardenId}/plants");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var plants = await response.Content.ReadFromJsonAsync<List<PlantResponse>>();
        Assert.NotNull(plants);
        Assert.Contains(plants, p => p.Id == plantId && p.GardenId == gardenId);
    }

    [Fact]
    public async Task GetPlantById_ReturnsPlant()
    {
        var userId = Guid.NewGuid();
        var gardenId = Guid.NewGuid();
        var plantId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();

            db.Gardens.Add(new GardenEntity
            {
                Id = gardenId,
                UserId = userId,
                Name = "Single Garden",
                TotalSurfaceArea = 10,
                TargetHumidityLevel = 50,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Deleted = false
            });

            db.Plants.Add(new PlantEntity
            {
                Id = plantId,
                GardenId = gardenId,
                Name = "Tomato",
                Species = "Solanum lycopersicum",
                Type = "Vegetable",
                PlantationDate = DateTimeOffset.UtcNow.Date,
                SurfaceAreaRequired = 5,
                IdealHumidityLevel = 60
            });

            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/v1/management/gardens/{gardenId}/plants/{plantId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var plant = await response.Content.ReadFromJsonAsync<PlantResponse>();
        Assert.NotNull(plant);
        Assert.Equal(plantId, plant.Id);
        Assert.Equal(gardenId, plant.GardenId);
    }

    [Fact]
    public async Task CreatePlant_CreatesAndReturnsPlant()
    {
        var userId = Guid.NewGuid();
        var gardenId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();

            db.Gardens.Add(new GardenEntity
            {
                Id = gardenId,
                UserId = userId,
                Name = "Garden For Plants",
                TotalSurfaceArea = 10,
                TargetHumidityLevel = 50,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Deleted = false
            });

            await db.SaveChangesAsync();
        }

        var request = new CreatePlantRequest(
            Name: "Tomato",
            Species: "Solanum lycopersicum",
            Type: "Vegetable",
            PlantationDate: DateTimeOffset.UtcNow.Date,
            SurfaceAreaRequired: 5,
            IdealHumidityLevel: 60);

        var response = await _client.PostAsJsonAsync($"/api/v1/management/gardens/{gardenId}/plants", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var plant = await response.Content.ReadFromJsonAsync<PlantResponse>();
        Assert.NotNull(plant);
        Assert.Equal(gardenId, plant.GardenId);
        Assert.Equal("Tomato", plant.Name);
        Assert.Equal("Solanum lycopersicum", plant.Species);
        Assert.Equal("Vegetable", plant.Type);
        Assert.Equal(5, plant.SurfaceAreaRequired);
        Assert.Equal(60, plant.IdealHumidityLevel);
    }

    [Fact]
    public async Task CreatePlant_Fails_When_SurfaceArea_Constraint_Violated()
    {
        var userId = Guid.NewGuid();
        var gardenId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();

            db.Gardens.Add(new GardenEntity
            {
                Id = gardenId,
                UserId = userId,
                Name = "Small Garden",
                TotalSurfaceArea = 5,
                TargetHumidityLevel = 50,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Deleted = false
            });

            await db.SaveChangesAsync();
        }

        var request = new CreatePlantRequest(
            Name: "Tomato",
            Species: "Solanum lycopersicum",
            Type: "Vegetable",
            PlantationDate: DateTimeOffset.UtcNow.Date,
            SurfaceAreaRequired: 6,
            IdealHumidityLevel: 60);

        var response = await _client.PostAsJsonAsync($"/api/v1/management/gardens/{gardenId}/plants", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problemDetails);
        Assert.True(problemDetails.Errors.ContainsKey("surfaceAreaRequired"));
    }

    [Fact]
    public async Task UpdatePlant_UpdatesAndReturnsPlant()
    {
        var userId = Guid.NewGuid();
        var gardenId = Guid.NewGuid();
        var plantId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();

            db.Gardens.Add(new GardenEntity
            {
                Id = gardenId,
                UserId = userId,
                Name = "Garden",
                TotalSurfaceArea = 10,
                TargetHumidityLevel = 50,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Deleted = false
            });

            db.Plants.Add(new PlantEntity
            {
                Id = plantId,
                GardenId = gardenId,
                Name = "Tomato",
                Species = "Solanum lycopersicum",
                Type = "Vegetable",
                PlantationDate = DateTimeOffset.UtcNow.Date,
                SurfaceAreaRequired = 5,
                IdealHumidityLevel = 60
            });

            await db.SaveChangesAsync();
        }

        var request = new UpdatePlantRequest(
            Name: "Cherry Tomato",
            Species: "Solanum lycopersicum var. cerasiforme",
            Type: "Fruit",
            PlantationDate: DateTimeOffset.UtcNow.Date.AddDays(-1),
            SurfaceAreaRequired: 6,
            IdealHumidityLevel: 65);

        var response = await _client.PutAsJsonAsync($"/api/v1/management/gardens/{gardenId}/plants/{plantId}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var plant = await response.Content.ReadFromJsonAsync<PlantResponse>();
        Assert.NotNull(plant);
        Assert.Equal("Cherry Tomato", plant.Name);
        Assert.Equal("Solanum lycopersicum var. cerasiforme", plant.Species);
        Assert.Equal("Fruit", plant.Type);
        Assert.Equal(6, plant.SurfaceAreaRequired);
        Assert.Equal(65, plant.IdealHumidityLevel);
    }

    [Fact]
    public async Task DeletePlant_RemovesPlant()
    {
        var userId = Guid.NewGuid();
        var gardenId = Guid.NewGuid();
        var plantId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();

            db.Gardens.Add(new GardenEntity
            {
                Id = gardenId,
                UserId = userId,
                Name = "Garden",
                TotalSurfaceArea = 10,
                TargetHumidityLevel = 50,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Deleted = false
            });

            db.Plants.Add(new PlantEntity
            {
                Id = plantId,
                GardenId = gardenId,
                Name = "Tomato",
                Species = "Solanum lycopersicum",
                Type = "Vegetable",
                PlantationDate = DateTimeOffset.UtcNow.Date,
                SurfaceAreaRequired = 5,
                IdealHumidityLevel = 60
            });

            await db.SaveChangesAsync();
        }

        var response = await _client.DeleteAsync($"/api/v1/management/gardens/{gardenId}/plants/{plantId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();

            var plant = await db.Plants.SingleOrDefaultAsync(p => p.Id == plantId);
            Assert.Null(plant);
        }
    }
}

