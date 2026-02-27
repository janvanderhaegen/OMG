using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OMG.Api.Management.Models;
using OMG.Management.Infrastructure;
using OMG.Management.Infrastructure.Entities;

namespace OMG.Api.Tests;

public class GardenManagementEndpointTests : IClassFixture<ManagementApiFactory>
{
    private readonly ManagementApiFactory _factory;
    private readonly HttpClient _client;

    public GardenManagementEndpointTests(ManagementApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetGardensForUser_ReturnsGardens()
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
                Name = "Test Garden",
                TotalSurfaceArea = 10,
                TargetHumidityLevel = 50,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Deleted = false
            });

            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/v1/management/gardens?userId={userId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var gardens = await response.Content.ReadFromJsonAsync<List<GardenResponse>>();
        Assert.NotNull(gardens);
        Assert.Contains(gardens, g => g.Id == gardenId && g.UserId == userId);
    }

    [Fact]
    public async Task GetGardenById_ReturnsGarden()
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
                Name = "Single Garden",
                TotalSurfaceArea = 20,
                TargetHumidityLevel = 60,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Deleted = false
            });

            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/v1/management/gardens/{gardenId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var garden = await response.Content.ReadFromJsonAsync<GardenResponse>();
        Assert.NotNull(garden);
        Assert.Equal(gardenId, garden.Id);
        Assert.Equal(userId, garden.UserId);
    }

    [Fact]
    public async Task CreateGarden_CreatesAndReturnsGarden()
    {
        var userId = Guid.NewGuid();

        var request = new CreateGardenRequest(
            UserId: userId,
            Name: "Created Garden",
            TotalSurfaceArea: 30,
            TargetHumidityLevel: 55);

        var response = await _client.PostAsJsonAsync("/api/v1/management/gardens", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var garden = await response.Content.ReadFromJsonAsync<GardenResponse>();
        Assert.NotNull(garden);
        Assert.Equal(userId, garden.UserId);
        Assert.Equal("Created Garden", garden.Name);
        Assert.Equal(30, garden.TotalSurfaceArea);
        Assert.Equal(55, garden.TargetHumidityLevel);
    }

    [Fact]
    public async Task UpdateGarden_UpdatesAndReturnsGarden()
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
                Name = "Original Name",
                TotalSurfaceArea = 10,
                TargetHumidityLevel = 50,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Deleted = false
            });

            await db.SaveChangesAsync();
        }

        var request = new UpdateGardenRequest(
            Name: "Updated Name",
            TotalSurfaceArea: 25,
            TargetHumidityLevel: 65);

        var response = await _client.PutAsJsonAsync($"/api/v1/management/gardens/{gardenId}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var garden = await response.Content.ReadFromJsonAsync<GardenResponse>();
        Assert.NotNull(garden);
        Assert.Equal("Updated Name", garden.Name);
        Assert.Equal(25, garden.TotalSurfaceArea);
        Assert.Equal(65, garden.TargetHumidityLevel);
    }

    [Fact]
    public async Task DeleteGarden_SoftDeletesGarden()
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
                Name = "To Delete",
                TotalSurfaceArea = 10,
                TargetHumidityLevel = 50,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Deleted = false
            });

            await db.SaveChangesAsync();
        }

        var response = await _client.DeleteAsync($"/api/v1/management/gardens/{gardenId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();

            // Query without ignoring filter should not find the garden
            var visible = await db.Gardens.SingleOrDefaultAsync(g => g.Id == gardenId);
            Assert.Null(visible);

            // But including deleted should show it as soft-deleted
            var all = await db.Gardens
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(g => g.Id == gardenId);

            Assert.NotNull(all);
            Assert.True(all!.Deleted);
            Assert.NotNull(all.DeletedAt);
        }
    }
}

