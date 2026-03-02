using Microsoft.EntityFrameworkCore;
using OMG.Telemetrics.Infrastructure.Entities;

namespace OMG.Telemetrics.Infrastructure;

public class TelemetricsDbContext : DbContext
{
    public TelemetricsDbContext(DbContextOptions<TelemetricsDbContext> options) : base(options) { }

    public DbSet<PlantHydrationStateEntity> PlantHydrationStates => Set<PlantHydrationStateEntity>();
    public DbSet<WateringSessionEntity> WateringSessions => Set<WateringSessionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("tele");

        var hydration = modelBuilder.Entity<PlantHydrationStateEntity>();
        hydration.ToTable("plant_hydration_state");
        hydration.HasKey(x => x.PlantId);
        hydration.Property(x => x.PlantId).IsRequired();
        hydration.Property(x => x.GardenId).IsRequired();
        hydration.Property(x => x.PlantType).IsRequired().HasMaxLength(50);
        hydration.Property(x => x.IdealHumidityLevel).IsRequired();
        hydration.Property(x => x.CurrentHumidity).IsRequired();
        hydration.Property(x => x.LastIrrigationStart);
        hydration.Property(x => x.LastIrrigationEnd);
        hydration.Property(x => x.IsWatering).IsRequired();
        hydration.Property(x => x.HasIrrigationLine).IsRequired();
        hydration.HasIndex(x => x.GardenId);

        var session = modelBuilder.Entity<WateringSessionEntity>();
        session.ToTable("watering_sessions");
        session.HasKey(x => x.SessionId);
        session.Property(x => x.SessionId).IsRequired();
        session.Property(x => x.PlantId).IsRequired();
        session.Property(x => x.GardenId).IsRequired();
        session.Property(x => x.StartedAt).IsRequired();
        session.Property(x => x.EndsAt).IsRequired();
        session.Property(x => x.Status).IsRequired().HasMaxLength(50);
        session.HasIndex(x => new { x.PlantId, x.Status });
        session.HasIndex(x => x.EndsAt);
    }
}
