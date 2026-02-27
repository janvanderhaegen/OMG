using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.AddServiceDefaults();

var app = builder.Build();

app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "OpenAPI V1");
}); 
app.MapScalarApiReference();


app.MapGet("/api/v1/health", () =>
    Results.Ok(new HealthResponse(
        Status: "Healthy",
        Service: "OMG.Api",
        Version: "1.0.0",
        UtcTimestamp: DateTimeOffset.UtcNow)))
   .WithName("GetHealth")
   .WithSummary("Returns health status for the OMG API.")
   .WithDescription("Basic readiness check for the Open Modular Gardening backend.");

app.Run();

public sealed record HealthResponse(
    string Status,
    string Service,
    string Version,
    DateTimeOffset UtcTimestamp);

public partial class Program;
