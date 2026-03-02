using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OMG.Telemetrics.Infrastructure;

/// <summary>
/// Design-time factory for EF Core tools to create <see cref="TelemetricsDbContext"/>.
/// </summary>
public sealed class TelemetricsDbContextFactory : IDesignTimeDbContextFactory<TelemetricsDbContext>
{
    public TelemetricsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TelemetricsDbContext>();
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__postgres")
            ?? "Host=localhost;Port=5432;Database=omg;Username=omg;Password=omg-password";
        optionsBuilder.UseNpgsql(connectionString);
        return new TelemetricsDbContext(optionsBuilder.Options);
    }
}
