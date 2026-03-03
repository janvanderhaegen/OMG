using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OMG.Api.Auth;
using OMG.Api.Infrastructure.Messaging;
using OMG.Api.Infrastructure.Seeding;
using OMG.Api.Management;
using OMG.Api.Reports;
using OMG.Api.Telemetrics;
using OMG.Auth.Infrastructure;
using OMG.Auth.Infrastructure.Entities;
using OMG.Auth.Infrastructure.Messaging;
using OMG.Auth.Infrastructure.Options;
using OMG.Auth.Infrastructure.Security;
using OMG.Auth.Infrastructure.Services;
using OMG.Management.Domain.Abstractions;
using OMG.Management.Domain.Gardens;
using OMG.Management.Infrastructure;
using OMG.Management.Infrastructure.Messaging;
using OMG.Management.Infrastructure.Repositories;
using OMG.Telemetrics.Domain.Hydration;
using OMG.Telemetrics.Infrastructure;
using Scalar.AspNetCore;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var isDevelopment = builder.Environment.IsDevelopment();
var isTesting = builder.Environment.IsEnvironment("Testing");

builder.Services.AddOpenApi();
builder.AddServiceDefaults();

builder.Services.AddDbContext<ManagementDbContext>(options =>
{
    if (isTesting)
    {
        options.UseInMemoryDatabase("ManagementTests");
    }
    else
    {
        var connectionString = builder.Configuration.GetConnectionString("postgres")
                              ?? throw new InvalidOperationException("Postgres connection string 'postgres' is not configured.");

        options.UseNpgsql(connectionString);
    }
});


builder.Services.AddDbContext<AuthDbContext>(options =>
{
    if (isTesting)
    {
        options.UseInMemoryDatabase("AuthTests");
    }
    else
    {
        var connectionString = builder.Configuration.GetConnectionString("postgres")
                              ?? throw new InvalidOperationException("Postgres connection string 'postgres' is not configured.");

        options.UseNpgsql(connectionString);
    }
});

builder.Services.AddDbContext<TelemetryDbContext>(options =>
{
    if (isTesting)
    {
        options.UseInMemoryDatabase("TelemetricsTests");
    }
    else
    {
        var connectionString = builder.Configuration.GetConnectionString("postgres")
                              ?? throw new InvalidOperationException("Postgres connection string 'postgres' is not configured.");
        options.UseNpgsql(connectionString);
    }
});

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = true;

        options.User.RequireUniqueEmail = true;

        options.SignIn.RequireConfirmedEmail = true;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AuthDbContext>();

builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, BCryptPasswordHasher>();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSection = builder.Configuration.GetSection("Jwt");
        var jwtOptions = jwtSection.Get<JwtOptions>()
                         ?? throw new InvalidOperationException("Jwt options are not configured. Please configure the 'Jwt' section.");

        var key = Encoding.UTF8.GetBytes(jwtOptions.Secret);

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

if (!isTesting)
{
    builder.Services.AddRateLimiter(options =>
    {
        options.AddPolicy("login", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = isDevelopment ? TimeSpan.FromSeconds(30) : TimeSpan.FromMinutes(5),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
    });
}

builder.Services.AddScoped<IGardenRepository, GardenRepository>();
builder.Services.AddScoped<IPublishUnitOfWork, PublishingUnitOfWork>();

builder.Services.AddScoped<IGardenIntegrationEventPublisher, GardenIntegrationEventPublisher>();
builder.Services.AddScoped<IAuthIntegrationEventPublisher, AuthIntegrationEventPublisher>();

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IPlantHydrationStateRepository, PlantTelemetryRepository>();
builder.Services.AddScoped<ITelemetryIntegrationEventPublisher, TelemetryIntegrationEventPublisher>();
builder.Services.AddScoped<IIrrigationSystemAdapter, MockIrrigationSystemAdapter>();
builder.Services.AddHostedService<IrrigationSimulationWorker>();

builder.Services.AddMessaging(builder.Configuration);

var app = builder.Build();

// In development, ensure the management and telemetrics database schemas exist.
if (isDevelopment)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();

    if (db.Database.GetMigrations().Any())
    {
        db.Database.Migrate();
    }

    var authDb = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    if (authDb.Database.GetMigrations().Any())
    {
        authDb.Database.Migrate();
    }

    await AuthDbContextSeeder.SeedAsync(scope.ServiceProvider);

    await ManagementDbContextSeeder.SeedAsync(scope.ServiceProvider);

    var teleDb = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
    if (teleDb.Database.GetMigrations().Any())
    {
        teleDb.Database.Migrate();
    }

}

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseRateLimiter();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "OMG API V1");
});
app.MapScalarApiReference();

app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapManagementGardenEndpoints();
app.MapManagementPlantEndpoints();
app.MapReportEndpoints();
app.MapTelemetryEndpoints();

app.Run();

public sealed record HealthResponse(
    string Status,
    string Service,
    string Version,
    DateTimeOffset UtcTimestamp);

public partial class Program;
