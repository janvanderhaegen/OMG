using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OMG.Auth.Infrastructure;
using OMG.Auth.Infrastructure.Entities;
using OMG.Management.Domain.Gardens;
using OMG.Management.Infrastructure;
using OMG.Management.Infrastructure.Entities;

namespace OMG.Api.Infrastructure.Seeding;

public static class ManagementDbContextSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();

        var authDb = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var managementDb = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();

        var bram = await authDb.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == "bram@inthepocket.com", cancellationToken)
            .ConfigureAwait(false);

        if (bram is null)
        {
            return;
        }

        var hasGarden = await managementDb.Gardens
            .AnyAsync(g => g.UserId == bram.Id, cancellationToken)
            .ConfigureAwait(false);

        if (hasGarden)
        {
            return;
        }

        var now = new DateTimeOffset(DateTimeOffset.UtcNow.Date, TimeSpan.Zero);
        var plantationDate = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var gardenId = Guid.NewGuid();

        var garden = new GardenEntity
        {
            Id = gardenId,
            UserId = bram.Id,
            Name = "Bram's Demo Garden",
            TotalSurfaceArea = 25,
            TargetHumidityLevel = 55,
            TelemetryApiKey = Guid.NewGuid().ToString("N"),
            CreatedAt = now,
            UpdatedAt = now,
            Deleted = false
        };

        managementDb.Gardens.Add(garden);

        var plants = new[]
        {
            new PlantEntity
            {
                Id = Guid.NewGuid(),
                GardenId = gardenId,
                Name = "Tomato",
                Species = "Solanum lycopersicum",
                Type = "Vegetable",
                PlantationDate = plantationDate,
                SurfaceAreaRequired = 3,
                IdealHumidityLevel = 60
            },
            new PlantEntity
            {
                Id = Guid.NewGuid(),
                GardenId = gardenId,
                Name = "Cucumber",
                Species = "Cucumis sativus",
                Type = "Vegetable",
                PlantationDate = plantationDate,
                SurfaceAreaRequired = 3,
                IdealHumidityLevel = 58
            },
            new PlantEntity
            {
                Id = Guid.NewGuid(),
                GardenId = gardenId,
                Name = "Strawberry",
                Species = "Fragaria × ananassa",
                Type = "Fruit",
                PlantationDate = plantationDate,
                SurfaceAreaRequired = 3,
                IdealHumidityLevel = 65
            },
            new PlantEntity
            {
                Id = Guid.NewGuid(),
                GardenId = gardenId,
                Name = "Lettuce",
                Species = "Lactuca sativa",
                Type = "Vegetable",
                PlantationDate = plantationDate,
                SurfaceAreaRequired = 2,
                IdealHumidityLevel = 55
            },
            new PlantEntity
            {
                Id = Guid.NewGuid(),
                GardenId = gardenId,
                Name = "Basil",
                Species = "Ocimum basilicum",
                Type = "Flower",
                PlantationDate = plantationDate,
                SurfaceAreaRequired = 1,
                IdealHumidityLevel = 70
            }
        };

        managementDb.Plants.AddRange(plants);

        await managementDb.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

