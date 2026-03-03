using MassTransit;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OMG.Management.Domain.Abstractions;
using OMG.Management.Infrastructure;
using OMG.Management.Infrastructure.Entities;
using OMG.Messaging.Contracts.Telemetry;
using OMG.Telemetrics.Domain.Hydration;
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
                    [FromServices] IPlantHydrationStateRepository plantRepository,
                    [FromServices] ITelemetryIntegrationEventPublisher eventPublisher,
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
                        plantRepository,
                        eventPublisher,
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
            .WithDescription("(Called from the 'external system - IrrigationSimulationWorker.cs'. Accepts batched meter readings (humidity, watering flag) identified by a garden telemetry API key.")
            .ExcludeFromDescription(); // exclude from openAPI documentation since this is a webhook intended for internal use only

        return endpoints;
    }

    internal static async Task<Dictionary<string, string[]>?> ProcessReadingsForGardenAsync(
        ManagementDbContext managementDbContext,
        TelemetryDbContext telemetryDbContext,
        IPlantHydrationStateRepository plantRepository,
        ITelemetryIntegrationEventPublisher eventPublisher,
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
        var domainEventsToPublish = new List<object>();

        foreach (var reading in readings)
        {
            var meterId = reading.MeterId!.Trim();

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

            var plantType = Enum.TryParse<PlantType>(managementPlant.Type, ignoreCase: true, out var parsed)
                ? parsed
                : PlantType.Vegetable;

            var state = await plantRepository.GetByMeterIdAsync(garden.Id, meterId, cancellationToken)
                .ConfigureAwait(false);

            var isNew = false;
            if (state is null)
            {
                var createResult = PlantHydrationState.InitializeFromPlantAdded(
                    managementPlant.Id,
                    meterId,
                    plantType,
                    managementPlant.IdealHumidityLevel);

                if (createResult.IsFailure)
                {
                    if (createResult.Error!.ValidationErrors is { } errs)
                    {
                        foreach (var (key, messages) in errs)
                        {
                            validationErrors[key] = [.. (validationErrors.GetValueOrDefault(key) ?? Array.Empty<string>()), .. messages];
                        }
                    }
                    continue;
                }

                state = createResult.Value!;
                state.AttachIrrigationLine();
                isNew = true;
            }

            var registerResult = state.RegisterCurrentHumidity(reading.CurrentHumidity);
            if (registerResult.IsFailure)
            {
                if (registerResult.Error!.ValidationErrors is { } errs)
                {
                    foreach (var (key, messages) in errs)
                    {
                        validationErrors[key] = [.. (validationErrors.GetValueOrDefault(key) ?? Array.Empty<string>()), .. messages];
                    }
                }
                continue;
            }

            if (!state.IsWatering && state.CurrentHumidityLevel < state.IdealHumidityLevel)
            {
                var startResult = state.StartWatering(now);
                if (startResult.IsFailure)
                {
                    if (startResult.Error!.ValidationErrors is { } errs)
                    {
                        foreach (var (key, messages) in errs)
                        {
                            validationErrors[key] = [.. (validationErrors.GetValueOrDefault(key) ?? Array.Empty<string>()), .. messages];
                        }
                    }
                    continue;
                }
            }
            else if (state.IsWatering && reading.IsWatering == false)
            {
                state.StopWatering(now);
            }

            if (isNew)
            {
                await plantRepository.AddAsync(state, meterId, garden.Id, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await plantRepository.SaveAsync(state, meterId, garden.Id, cancellationToken).ConfigureAwait(false);
            }

            domainEventsToPublish.AddRange(state.DomainEvents);
            state.ClearDomainEvents();
        }

        await telemetryDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (domainEventsToPublish.Count > 0)
        {
            await eventPublisher.PublishAsync(domainEventsToPublish, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return null;
    }
}

