using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OMG.Api.Management.Models;
using OMG.Management.Infrastructure;
using OMG.Management.Infrastructure.Entities;
using OMG.Api.Tests.Auth;

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
        var authenticated = await AuthTestHelper.AuthenticateAsync(_factory, _client, "gardens-user@example.com");
        var userId = authenticated.UserId;
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

        var response = await _client.GetAsync($"/api/v1/management/gardens");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var gardens = await response.Content.ReadFromJsonAsync<List<GardenResponse>>();
        Assert.NotNull(gardens);
        Assert.Contains(gardens, g => g.Id == gardenId );
    }

    [Fact]
    public async Task GetGardenById_ReturnsGarden()
    {
        var authenticated = await AuthTestHelper.AuthenticateAsync(_factory, _client, "gardens-user2@example.com");
        var userId = authenticated.UserId;
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
    }

    [Fact]
    public async Task CreateGarden_CreatesAndReturnsGarden()
    {
        var authenticated = await AuthTestHelper.AuthenticateAsync(_factory, _client, "gardens-create@example.com");
        var userId = authenticated.UserId;

        var request = new CreateGardenRequest( 
            Name: "Created Garden",
            TotalSurfaceArea: 30,
            TargetHumidityLevel: 55);

        var response = await _client.PostAsJsonAsync("/api/v1/management/gardens", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var garden = await response.Content.ReadFromJsonAsync<GardenResponse>();
        Assert.NotNull(garden); 
        Assert.Equal("Created Garden", garden.Name);
        Assert.Equal(30, garden.TotalSurfaceArea);
        Assert.Equal(55, garden.TargetHumidityLevel);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();

            var entity = await db.Gardens.SingleAsync(g => g.Id == garden!.Id);
            Assert.Equal(userId, entity.UserId);
            Assert.Equal("Created Garden", entity.Name);
            Assert.Equal(30, entity.TotalSurfaceArea);
            Assert.Equal(55, entity.TargetHumidityLevel);
        }
    }

    [Fact]
    public async Task UpdateGarden_UpdatesAndReturnsGarden()
    {
        var authenticated = await AuthTestHelper.AuthenticateAsync(_factory, _client, "gardens-update@example.com");
        var userId = authenticated.UserId;
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

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();

            var entity = await db.Gardens.SingleAsync(g => g.Id == gardenId);
            Assert.Equal("Updated Name", entity.Name);
            Assert.Equal(25, entity.TotalSurfaceArea);
            Assert.Equal(65, entity.TargetHumidityLevel);
        }
    }

    [Fact]
    public async Task DeleteGarden_SoftDeletesGarden()
    {
        var authenticated = await AuthTestHelper.AuthenticateAsync(_factory, _client, "gardens-delete@example.com");
        var userId = authenticated.UserId;
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

