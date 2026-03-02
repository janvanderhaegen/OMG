using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OMG.Telemetrics.Infrastructure;

namespace OMG.Api.Telemetrics;

public static class TelemetricsEndpoints
{
    public static IEndpointRouteBuilder MapTelemetricsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/telemetrics")
            .WithTags("Telemetrics");

        group.MapGet(
                "gardens/{gardenId:guid}/plants",
                async Task<IResult> (
                    [FromServices] TelemetricsDbContext db,
                    Guid gardenId,
                    CancellationToken cancellationToken) =>
                {
                    var states = await db.PlantHydrationStates
                        .Where(s => s.GardenId == gardenId)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);

                    var responses = states
                        .Select(s => new PlantMetricsResponse(
                            s.PlantId,
                            s.GardenId,
                            s.CurrentHumidity,
                            s.IdealHumidityLevel,
                            s.LastIrrigationStart,
                            s.LastIrrigationEnd,
                            s.IsWatering,
                            s.HasIrrigationLine))
                        .ToList();

                    return TypedResults.Ok((IReadOnlyList<PlantMetricsResponse>)responses);
                })
            .WithName("GetPlantMetricsForGarden")
            .WithSummary("Lists realtime metrics for all plants in the garden.");

        group.MapGet(
                "gardens/{gardenId:guid}/plants/{plantId:guid}",
                async Task<IResult> (
                    [FromServices] TelemetricsDbContext db,
                    Guid gardenId,
                    Guid plantId,
                    CancellationToken cancellationToken) =>
                {
                    var state = await db.PlantHydrationStates
                        .FirstOrDefaultAsync(s => s.GardenId == gardenId && s.PlantId == plantId, cancellationToken)
                        .ConfigureAwait(false);

                    if (state is null)
                        return TypedResults.NotFound();

                    return TypedResults.Ok(new PlantMetricsResponse(
                        state.PlantId,
                        state.GardenId,
                        state.CurrentHumidity,
                        state.IdealHumidityLevel,
                        state.LastIrrigationStart,
                        state.LastIrrigationEnd,
                        state.IsWatering,
                        state.HasIrrigationLine));
                })
            .WithName("GetPlantMetricsById")
            .WithSummary("Gets realtime metrics for a single plant.");

        group.MapGet(
                "gardens/{gardenId:guid}/irrigation/lines",
                async Task<IResult> (
                    [FromServices] TelemetricsDbContext db,
                    Guid gardenId,
                    CancellationToken cancellationToken) =>
                {
                    var states = await db.PlantHydrationStates
                        .Where(s => s.GardenId == gardenId && s.HasIrrigationLine)
                        .Select(s => new { s.PlantId, s.GardenId })
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);

                    var responses = states
                        .Select(s => new IrrigationLineResponse(s.PlantId, s.GardenId))
                        .ToList();

                    return TypedResults.Ok((IReadOnlyList<IrrigationLineResponse>)responses);
                })
            .WithName("GetIrrigationLinesForGarden")
            .WithSummary("Lists plants that have an irrigation line in the garden.");

        group.MapPost(
                "gardens/{gardenId:guid}/irrigation/lines",
                async Task<IResult> (
                    [FromServices] TelemetricsDbContext db,
                    Guid gardenId,
                    [FromBody] AttachIrrigationLineRequest request,
                    CancellationToken cancellationToken) =>
                {
                    var state = await db.PlantHydrationStates
                        .FirstOrDefaultAsync(s => s.GardenId == gardenId && s.PlantId == request.PlantId, cancellationToken)
                        .ConfigureAwait(false);

                    if (state is null)
                        return TypedResults.NotFound();

                    if (state.HasIrrigationLine)
                        return TypedResults.BadRequest("This plant already has an irrigation line.");

                    state.HasIrrigationLine = true;
                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    return TypedResults.Created(
                        $"/api/v1/telemetrics/gardens/{gardenId}/irrigation/lines",
                        new IrrigationLineResponse(state.PlantId, state.GardenId));
                })
            .WithName("AttachIrrigationLine")
            .WithSummary("Attaches an irrigation line to a plant (at most one per plant).");

        group.MapDelete(
                "gardens/{gardenId:guid}/irrigation/lines/{plantId:guid}",
                async Task<IResult> (
                    [FromServices] TelemetricsDbContext db,
                    Guid gardenId,
                    Guid plantId,
                    CancellationToken cancellationToken) =>
                {
                    var state = await db.PlantHydrationStates
                        .FirstOrDefaultAsync(s => s.GardenId == gardenId && s.PlantId == plantId, cancellationToken)
                        .ConfigureAwait(false);

                    if (state is null)
                        return TypedResults.NotFound();

                    state.HasIrrigationLine = false;
                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    return TypedResults.NoContent();
                })
            .WithName("DetachIrrigationLine")
            .WithSummary("Detaches the irrigation line from a plant.");

        return endpoints;
    }
}

public sealed record PlantMetricsResponse(
    Guid PlantId,
    Guid GardenId,
    int CurrentHumidityLevel,
    int IdealHumidityLevel,
    DateTimeOffset? LastIrrigationStart,
    DateTimeOffset? LastIrrigationEnd,
    bool IsWatering,
    bool HasIrrigationLine);

public sealed record IrrigationLineResponse(Guid PlantId, Guid GardenId);

public sealed record AttachIrrigationLineRequest(Guid PlantId);
