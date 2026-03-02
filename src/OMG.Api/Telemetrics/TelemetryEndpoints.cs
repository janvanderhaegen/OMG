using MassTransit;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OMG.Management.Domain.Abstractions;
using OMG.Management.Infrastructure;
using OMG.Management.Infrastructure.Entities;
using OMG.Messaging.Contracts.Telemetry;
using OMG.Telemetrics.Infrastructure;

namespace OMG.Api.Telemetrics;

public static class TelemetryEndpoints
{
    private const string TelemetryApiKeyHeader = "X-Garden-Telemetry-Key";

    public static IEndpointRouteBuilder MapTelemetryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/telemetry")
            .WithTags("Telemetry");

        group.MapPost(
                "/readings",
                async Task<Results<Ok, ValidationProblem, UnauthorizedHttpResult, NotFound>> (
                    [FromServices] ManagementDbContext managementDbContext,
                    [FromServices] TelemetryDbContext telemetryDbContext,
                    [FromServices] IPublishEndpoint publishEndpoint,
                    [FromServices] IPublishUnitOfWork unitOfWork,
                    HttpRequest httpRequest,
                    [FromBody] IReadOnlyList<TelemetryReadingRequest> readings,
                    CancellationToken cancellationToken) =>
                {
                    if (!httpRequest.Headers.TryGetValue(TelemetryApiKeyHeader, out var headerValues)
                        || string.IsNullOrWhiteSpace(headerValues.FirstOrDefault()))
                    {
                        return TypedResults.Unauthorized();
                    }

                    var apiKey = headerValues.First()!.Trim();

                    var garden = await managementDbContext.Gardens
                        .AsNoTracking()
                        .FirstOrDefaultAsync(g => g.TelemetryApiKey == apiKey, cancellationToken)
                        .ConfigureAwait(false);

                    if (garden is null)
                    {
                        return TypedResults.NotFound();
                    }

                    var validationErrors = await ProcessReadingsForGardenAsync(
                        managementDbContext,
                        telemetryDbContext,
                        unitOfWork,
                        publishEndpoint,
                        garden,
                        readings,
                        cancellationToken).ConfigureAwait(false);

                    if (validationErrors is not null && validationErrors.Count > 0)
                    {
                        return TypedResults.ValidationProblem(validationErrors);
                    }

                    return TypedResults.Ok();
                })
            .WithName("PostTelemetryReadings")
            .WithSummary("Webhook: Ingests telemetry readings for a garden.")
            .WithDescription("(Called from the 'external system - IrrigationSimulationWorker.cs'. Accepts batched meter readings (humidity, watering flag) identified by a garden telemetry API key.");

        return endpoints;
    }

    internal static async Task<Dictionary<string, string[]>?> ProcessReadingsForGardenAsync(
        ManagementDbContext managementDbContext,
        TelemetryDbContext telemetryDbContext,
        IPublishUnitOfWork unitOfWork,
        IPublishEndpoint publishEndpoint,
        GardenEntity garden,
        IReadOnlyList<TelemetryReadingRequest> readings,
        CancellationToken cancellationToken)
    {
        var validationErrors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var reading in readings)
        {
            if (string.IsNullOrWhiteSpace(reading.MeterId))
            {
                validationErrors["meterId"] = ["MeterId is required for all telemetry readings."];
            }

            if (reading.CurrentHumidity is < 0 or > 100)
            {
                validationErrors["currentHumidity"] = ["Current humidity must be between 0 and 100."];
            }
        }

        if (validationErrors.Count > 0)
        {
            return validationErrors;
        }

        var now = DateTimeOffset.UtcNow;

        foreach (var reading in readings)
        {
            var meterId = reading.MeterId!.Trim();

            // Find the management plant for this meter.
            var managementPlant = await managementDbContext.Plants
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.GardenId == garden.Id && p.MeterId == meterId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (managementPlant is null)
            {
                continue;
            }

            var telemetryPlant = await telemetryDbContext.Plants
                .FirstOrDefaultAsync(
                    p => p.GardenId == garden.Id && p.MeterId == meterId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (telemetryPlant is null)
            {
                telemetryPlant = new OMG.Telemetrics.Infrastructure.Entities.TelemetryPlantEntity
                {
                    PlantId = managementPlant.Id,
                    GardenId = garden.Id,
                    MeterId = meterId,
                    IdealHumidityLevel = managementPlant.IdealHumidityLevel,
                    CurrentHumidityLevel = reading.CurrentHumidity,
                    IsWatering = reading.IsWatering,
                    HasIrrigationLine = true,
                    LastTelemetryAt = now
                };

                await telemetryDbContext.Plants.AddAsync(telemetryPlant, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                telemetryPlant.CurrentHumidityLevel = reading.CurrentHumidity;
                telemetryPlant.IsWatering = reading.IsWatering;
                telemetryPlant.LastTelemetryAt = now;
            }

            if (telemetryPlant.CurrentHumidityLevel < telemetryPlant.IdealHumidityLevel)
            {
                await publishEndpoint.Publish(
                    new WateringNeeded(
                        managementPlant.MeterId!,
                        telemetryPlant.CurrentHumidityLevel,
                        telemetryPlant.IdealHumidityLevel,
                        now),
                    cancellationToken).ConfigureAwait(false);
                await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            else if (telemetryPlant.CurrentHumidityLevel > telemetryPlant.IdealHumidityLevel)
            {
                await publishEndpoint.Publish(
                    new HydrationSatisfied(
                        managementPlant.MeterId!,
                        telemetryPlant.CurrentHumidityLevel,
                        telemetryPlant.IdealHumidityLevel,
                        now),
                    cancellationToken).ConfigureAwait(false);
                await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        await telemetryDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return null;
    }
}

