using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OMG.Management.Infrastructure;

/// <summary>
/// Design-time factory for EF Core tools to create <see cref="ManagementDbContext"/> without relying on the API startup configuration.
/// </summary>
public sealed class ManagementDbContextFactory : IDesignTimeDbContextFactory<ManagementDbContext>
{
    public ManagementDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ManagementDbContext>();

        // Prefer environment-provided connection string when available (e.g. from docker-compose or local env).
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__postgres")
            ?? "Host=localhost;Port=5432;Database=omg;Username=omg;Password=omg-password";

        optionsBuilder.UseNpgsql(connectionString);

        return new ManagementDbContext(optionsBuilder.Options);
    }
}

