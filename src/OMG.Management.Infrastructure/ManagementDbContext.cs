using Microsoft.EntityFrameworkCore;
using OMG.Management.Infrastructure.Entities;

namespace OMG.Management.Infrastructure;

public class ManagementDbContext(DbContextOptions<ManagementDbContext> options) : DbContext(options)
{
    public DbSet<GardenEntity> Gardens => Set<GardenEntity>();

    public DbSet<PlantEntity> Plants => Set<PlantEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var garden = modelBuilder.Entity<GardenEntity>();

        garden.ToTable("gardens", schema: "gm");

        garden.HasKey(x => x.Id);

        garden.Property(x => x.Id)
            .IsRequired();

        garden.Property(x => x.UserId)
            .IsRequired();

        garden.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        garden.Property(x => x.TotalSurfaceArea)
            .IsRequired();

        garden.Property(x => x.TargetHumidityLevel)
            .IsRequired();

        garden.Property(x => x.CreatedAt)
            .IsRequired();

        garden.Property(x => x.UpdatedAt)
            .IsRequired();

        garden.Property(x => x.RowVersion);

        garden.Property(x => x.Deleted)
            .IsRequired();

        garden.Property(x => x.DeletedAt);

        garden.HasQueryFilter(x => !x.Deleted);

        garden.HasMany(g => g.Plants)
            .WithOne()
            .HasForeignKey(p => p.GardenId)
            .OnDelete(DeleteBehavior.Cascade);

        var plant = modelBuilder.Entity<PlantEntity>();

        plant.ToTable("plants", schema: "gm");

        plant.HasKey(x => x.Id);

        plant.Property(x => x.Id)
            .IsRequired();

        plant.Property(x => x.GardenId)
            .IsRequired();

        plant.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        plant.Property(x => x.Species)
            .IsRequired()
            .HasMaxLength(200);

        plant.Property(x => x.Type)
            .IsRequired()
            .HasMaxLength(50);

        plant.Property(x => x.PlantationDate)
            .IsRequired();

        plant.Property(x => x.SurfaceAreaRequired)
            .IsRequired();

        plant.Property(x => x.IdealHumidityLevel)
            .IsRequired();

        plant.HasIndex(x => x.GardenId);
    }
}

