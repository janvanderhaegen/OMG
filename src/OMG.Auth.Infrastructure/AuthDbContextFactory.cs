using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OMG.Auth.Infrastructure;

/// <summary>
/// Design-time factory for <see cref="AuthDbContext"/> used by EF Core tools to
/// create migrations without depending on the API host configuration.
/// </summary>
public sealed class AuthDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AuthDbContext>();

        // Prefer an environment variable so local developers can override the connection.
        var connectionString =
            Environment.GetEnvironmentVariable("OMG_AUTH_CONNECTION") ??
            "Host=localhost;Port=5432;Database=omg_auth;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString);

        return new AuthDbContext(optionsBuilder.Options);
    }
}

