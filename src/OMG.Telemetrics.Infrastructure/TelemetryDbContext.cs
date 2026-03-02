using Microsoft.EntityFrameworkCore;
using OMG.Telemetrics.Infrastructure.Entities;

namespace OMG.Telemetrics.Infrastructure;

public class TelemetryDbContext(DbContextOptions<TelemetryDbContext> options) : DbContext(options)
{
    public DbSet<TelemetryPlantEntity> Plants => Set<TelemetryPlantEntity>();

    public DbSet<WateringSessionEntity> WateringSessions => Set<WateringSessionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var plant = modelBuilder.Entity<TelemetryPlantEntity>();

        plant.ToTable("plants", schema: "telemetry");

        plant.HasKey(x => x.PlantId);

        plant.Property(x => x.PlantId)
            .IsRequired();

        plant.Property(x => x.GardenId)
            .IsRequired();

        plant.Property(x => x.MeterId)
            .HasMaxLength(100);

        plant.Property(x => x.IdealHumidityLevel)
            .IsRequired();

        plant.Property(x => x.CurrentHumidityLevel)
            .IsRequired();

        plant.Property(x => x.IsWatering)
            .IsRequired();

        plant.Property(x => x.HasIrrigationLine)
            .IsRequired();

        plant.Property(x => x.LastTelemetryAt);

        plant.HasIndex(x => x.MeterId);
        plant.HasIndex(x => x.GardenId);

        var session = modelBuilder.Entity<WateringSessionEntity>();

        session.ToTable("watering_sessions", schema: "telemetry");

        session.HasKey(x => x.SessionId);

        session.Property(x => x.SessionId)
            .IsRequired();

        session.Property(x => x.PlantId)
            .IsRequired();

        session.Property(x => x.StartedAt)
            .IsRequired();

        session.Property(x => x.EndedAt);

        session.Property(x => x.IsActive)
            .IsRequired();

        session.HasIndex(x => x.PlantId);
    }
}

