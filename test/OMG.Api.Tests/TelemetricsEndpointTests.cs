using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OMG.Api.Tests.Auth;
using OMG.Management.Infrastructure;
using OMG.Management.Infrastructure.Entities;
using OMG.Telemetrics.Infrastructure;
using OMG.Telemetrics.Infrastructure.Entities;

namespace OMG.Api.Tests;

public class TelemetricsEndpointTests : IClassFixture<ManagementApiFactory>
{
    private readonly ManagementApiFactory _factory;
    private readonly HttpClient _client;

    public TelemetricsEndpointTests(ManagementApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetPlantMetricsForGarden_Returns_Empty_When_No_Plants_Tracked()
    {
        var authenticated = await AuthTestHelper.AuthenticateAsync(_factory, _client, "telemetrics-empty@example.com");
        var userId = authenticated.UserId;
        var gardenId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var managementDb = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();
            managementDb.Gardens.Add(new GardenEntity
            {
                Id = gardenId,
                UserId = userId,
                Name = "Telemetrics Garden",
                TotalSurfaceArea = 10,
                TargetHumidityLevel = 50,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Deleted = false
            });
            await managementDb.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/v1/telemetrics/gardens/{gardenId}/plants");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<PlantMetricsResponse>>();
        Assert.NotNull(list);
        Assert.Empty(list);
    }

    [Fact]
    public async Task GetPlantMetricsForGarden_Returns_Metrics_When_Hydration_State_Exists()
    {
        var authenticated = await AuthTestHelper.AuthenticateAsync(_factory, _client, "telemetrics-metrics@example.com");
        var userId = authenticated.UserId;
        var gardenId = Guid.NewGuid();
        var plantId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var managementDb = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();
            managementDb.Gardens.Add(new GardenEntity
            {
                Id = gardenId,
                UserId = userId,
                Name = "Telemetrics Garden",
                TotalSurfaceArea = 10,
                TargetHumidityLevel = 50,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Deleted = false
            });
            await managementDb.SaveChangesAsync();


            var db = scope.ServiceProvider.GetRequiredService<TelemetricsDbContext>();
            db.PlantHydrationStates.Add(new PlantHydrationStateEntity
            {
                PlantId = plantId,
                GardenId = gardenId,
                PlantType = "Vegetable",
                IdealHumidityLevel = 60,
                CurrentHumidity = 50,
                IsWatering = false,
                HasIrrigationLine = true
            });
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/v1/telemetrics/gardens/{gardenId}/plants");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<PlantMetricsResponse>>();
        Assert.NotNull(list);
        Assert.Single(list);
        Assert.Equal(plantId, list[0].PlantId);
        Assert.Equal(gardenId, list[0].GardenId);
        Assert.Equal(50, list[0].CurrentHumidityLevel);
        Assert.Equal(60, list[0].IdealHumidityLevel);
        Assert.True(list[0].HasIrrigationLine);
    }

    [Fact]
    public async Task GetPlantMetricsById_Returns_404_When_Not_Tracked()
    {
        var authenticated = await AuthTestHelper.AuthenticateAsync(_factory, _client, "telemetrics-notracked@example.com");
        var userId = authenticated.UserId;
        var gardenId = Guid.NewGuid();
        var plantId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var managementDb = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();
            managementDb.Gardens.Add(new GardenEntity
            {
                Id = gardenId,
                UserId = userId,
                Name = "Telemetrics Garden",
                TotalSurfaceArea = 10,
                TargetHumidityLevel = 50,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Deleted = false
            });
            await managementDb.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/v1/telemetrics/gardens/{gardenId}/plants/{plantId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetIrrigationLinesForGarden_Returns_Only_Plants_With_Lines()
    {
        var authenticated = await AuthTestHelper.AuthenticateAsync(_factory, _client, "telemetrics-lines@example.com");
        var userId = authenticated.UserId;
        var gardenId = Guid.NewGuid();
        var plant1 = Guid.NewGuid();
        var plant2 = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var managementDb = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();
            managementDb.Gardens.Add(new GardenEntity
            {
                Id = gardenId,
                UserId = userId,
                Name = "Telemetrics Garden",
                TotalSurfaceArea = 10,
                TargetHumidityLevel = 50,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Deleted = false
            });
            await managementDb.SaveChangesAsync();

            var db = scope.ServiceProvider.GetRequiredService<TelemetricsDbContext>();
            db.PlantHydrationStates.Add(new PlantHydrationStateEntity
            {
                PlantId = plant1,
                GardenId = gardenId,
                PlantType = "Vegetable",
                IdealHumidityLevel = 60,
                CurrentHumidity = 50,
                IsWatering = false,
                HasIrrigationLine = true
            });
            db.PlantHydrationStates.Add(new PlantHydrationStateEntity
            {
                PlantId = plant2,
                GardenId = gardenId,
                PlantType = "Fruit",
                IdealHumidityLevel = 65,
                CurrentHumidity = 50,
                IsWatering = false,
                HasIrrigationLine = false
            });
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/v1/telemetrics/gardens/{gardenId}/irrigation/lines");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<IrrigationLineResponse>>();
        Assert.NotNull(list);
        Assert.Single(list);
        Assert.Equal(plant1, list[0].PlantId);
    }

    [Fact]
    public async Task AttachIrrigationLine_Attaches_And_Returns_201()
    {
        var authenticated = await AuthTestHelper.AuthenticateAsync(_factory, _client, "telemetrics-attach@example.com");
        var userId = authenticated.UserId;
        var gardenId = Guid.NewGuid();
        var plantId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var managementDb = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();
            managementDb.Gardens.Add(new GardenEntity
            {
                Id = gardenId,
                UserId = userId,
                Name = "Telemetrics Garden",
                TotalSurfaceArea = 10,
                TargetHumidityLevel = 50,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Deleted = false
            });
            await managementDb.SaveChangesAsync();

            var db = scope.ServiceProvider.GetRequiredService<TelemetricsDbContext>();
            db.PlantHydrationStates.Add(new PlantHydrationStateEntity
            {
                PlantId = plantId,
                GardenId = gardenId,
                PlantType = "Vegetable",
                IdealHumidityLevel = 60,
                CurrentHumidity = 50,
                IsWatering = false,
                HasIrrigationLine = false
            });
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/telemetrics/gardens/{gardenId}/irrigation/lines",
            new AttachIrrigationLineRequest(plantId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var line = await response.Content.ReadFromJsonAsync<IrrigationLineResponse>();
        Assert.NotNull(line);
        Assert.Equal(plantId, line.PlantId);
        Assert.Equal(gardenId, line.GardenId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TelemetricsDbContext>();
            var state = await db.PlantHydrationStates.FirstAsync(s => s.PlantId == plantId);
            Assert.True(state.HasIrrigationLine);
        }
    }

    [Fact]
    public async Task AttachIrrigationLine_Returns_400_When_Already_Has_Line()
    {
        var authenticated = await AuthTestHelper.AuthenticateAsync(_factory, _client, "telemetrics-attach-bad@example.com");
        var userId = authenticated.UserId;
        var gardenId = Guid.NewGuid();
        var plantId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var managementDb = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();
            managementDb.Gardens.Add(new GardenEntity
            {
                Id = gardenId,
                UserId = userId,
                Name = "Telemetrics Garden",
                TotalSurfaceArea = 10,
                TargetHumidityLevel = 50,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Deleted = false
            });
            await managementDb.SaveChangesAsync();  

            var db = scope.ServiceProvider.GetRequiredService<TelemetricsDbContext>();
            db.PlantHydrationStates.Add(new PlantHydrationStateEntity
            {
                PlantId = plantId,
                GardenId = gardenId,
                PlantType = "Vegetable",
                IdealHumidityLevel = 60,
                CurrentHumidity = 50,
                IsWatering = false,
                HasIrrigationLine = true
            });
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/telemetrics/gardens/{gardenId}/irrigation/lines",
            new AttachIrrigationLineRequest(plantId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DetachIrrigationLine_Returns_204_And_Removes_Line()
    {
        var authenticated = await AuthTestHelper.AuthenticateAsync(_factory, _client, "telemetrics-detach@example.com");
        var userId = authenticated.UserId;
        var gardenId = Guid.NewGuid();
        var plantId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var managementDb = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();
            managementDb.Gardens.Add(new GardenEntity
            {
                Id = gardenId,
                UserId = userId,
                Name = "Telemetrics Garden",
                TotalSurfaceArea = 10,
                TargetHumidityLevel = 50,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Deleted = false
            });
            await managementDb.SaveChangesAsync();

            var db = scope.ServiceProvider.GetRequiredService<TelemetricsDbContext>();
            db.PlantHydrationStates.Add(new PlantHydrationStateEntity
            {
                PlantId = plantId,
                GardenId = gardenId,
                PlantType = "Vegetable",
                IdealHumidityLevel = 60,
                CurrentHumidity = 50,
                IsWatering = false,
                HasIrrigationLine = true
            });
            await db.SaveChangesAsync();
        }

        var response = await _client.DeleteAsync($"/api/v1/telemetrics/gardens/{gardenId}/irrigation/lines/{plantId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TelemetricsDbContext>();
            var state = await db.PlantHydrationStates.FirstAsync(s => s.PlantId == plantId);
            Assert.False(state.HasIrrigationLine);
        }
    }

    private sealed record PlantMetricsResponse(
        Guid PlantId,
        Guid GardenId,
        int CurrentHumidityLevel,
        int IdealHumidityLevel,
        DateTimeOffset? LastIrrigationStart,
        DateTimeOffset? LastIrrigationEnd,
        bool IsWatering,
        bool HasIrrigationLine);

    private sealed record IrrigationLineResponse(Guid PlantId, Guid GardenId);

    private sealed record AttachIrrigationLineRequest(Guid PlantId);
}
