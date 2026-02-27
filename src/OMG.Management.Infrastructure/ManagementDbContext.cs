using Microsoft.EntityFrameworkCore;
using OMG.Management.Infrastructure.Entities;

namespace OMG.Management.Infrastructure;

public class ManagementDbContext(DbContextOptions<ManagementDbContext> options) : DbContext(options)
{
    public DbSet<GardenEntity> Gardens => Set<GardenEntity>();

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

        garden.Property(x => x.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        garden.Property(x => x.Deleted)
            .IsRequired();

        garden.Property(x => x.DeletedAt);

        garden.HasQueryFilter(x => !x.Deleted);
    }
}

