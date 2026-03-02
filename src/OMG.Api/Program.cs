using Scalar.AspNetCore;
using OMG.Api.Infrastructure.Messaging;
using OMG.Management.Infrastructure;
using OMG.Management.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using OMG.Api.Management;
using OMG.Management.Domain.Abstractions;
using OMG.Management.Domain.Gardens;
using OMG.Management.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.AddServiceDefaults();

builder.Services.AddDbContext<ManagementDbContext>(options =>
{
    if (builder.Environment.IsEnvironment("Testing"))
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

builder.Services.AddScoped<IGardenRepository, GardenRepository>();
builder.Services.AddScoped<IManagementUnitOfWork, ManagementUnitOfWork>();

builder.Services.AddScoped<IGardenIntegrationEventPublisher, GardenIntegrationEventPublisher>();

builder.Services.AddMessaging(builder.Configuration);

var app = builder.Build();

// In development, ensure the management database schema exists (migrations if available, otherwise create).
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();
    db.Database.EnsureCreated();
    var migrations = db.Database.GetMigrations();
    if (db.Database.GetMigrations().Any())
    {
        db.Database.Migrate();
    } 
}

app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "OMG API V1");
}); 
app.MapScalarApiReference();

app.MapHealthEndpoints();
app.MapManagementGardenEndpoints();
app.MapManagementPlantEndpoints();

app.Run();

public sealed record HealthResponse(
    string Status,
    string Service,
    string Version,
    DateTimeOffset UtcTimestamp);

public partial class Program;
