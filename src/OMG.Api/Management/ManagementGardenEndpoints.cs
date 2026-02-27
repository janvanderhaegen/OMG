using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OMG.Api.Management.Models;
using OMG.Management.Domain.Abstractions;
using OMG.Management.Domain.Common;
using OMG.Management.Domain.Gardens;
using OMG.Management.Infrastructure;
using OMG.Management.Infrastructure.Messaging;

namespace OMG.Api.Management;

public static class ManagementGardenEndpoints
{
    public static IEndpointRouteBuilder MapManagementGardenEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/management/gardens")
            .WithTags("Garden Management");

        group.MapGet(
            "/",
            async Task<Ok<IReadOnlyList<GardenResponse>>> (
                [FromServices] IGardenRepository gardenRepository,
                [FromQuery] Guid userId,
                CancellationToken cancellationToken) =>
            {
                var gardens = await gardenRepository
                    .ListByUserAsync(new UserId(userId), cancellationToken)
                    .ConfigureAwait(false);

                var responses = gardens
                    .Select(MapToResponse)
                    .ToList();

                return TypedResults.Ok((IReadOnlyList<GardenResponse>)responses);
            })
            .WithName("GetGardensForUser")
            .WithSummary("Lists all gardens for the specified user.")
            .WithDescription("Returns all gardens owned by the specified user.");

        group.MapGet(
                "/{gardenId:guid}",
                async Task<Results<Ok<GardenResponse>, NotFound>> (
                    [FromServices] IGardenRepository gardenRepository,
                    Guid gardenId,
                    CancellationToken cancellationToken) =>
                {
                    var garden = await gardenRepository
                        .GetByIdAsync(new GardenId(gardenId), cancellationToken)
                        .ConfigureAwait(false);

                    if (garden is null)
                    {
                        return TypedResults.NotFound();
                    }

                    return TypedResults.Ok(MapToResponse(garden));
                })
            .WithName("GetGardenById")
            .WithSummary("Gets a single garden's details.")
            .WithDescription("Returns the details of a garden by its identifier.");

        group.MapPost(
                "/",
                async Task<Results<Created<GardenResponse>, ValidationProblem>> (
                    [FromServices] IGardenRepository gardenRepository,
                    [FromServices] IManagementUnitOfWork unitOfWork,
                    [FromServices] IGardenIntegrationEventPublisher integrationEventPublisher,
                    [FromBody] CreateGardenRequest request,
                    CancellationToken cancellationToken) =>
                {
                    var utcNow = DateTimeOffset.UtcNow;

                    var result = Garden.Create(
                        new UserId(request.UserId),
                        request.Name,
                        request.TotalSurfaceArea,
                        request.TargetHumidityLevel,
                        utcNow);

                    if (result.IsFailure || result.Value is null)
                    {
                        return CreateValidationProblem(result.Error!);
                    }

                    var garden = result.Value;

                    await gardenRepository.AddAsync(garden, cancellationToken).ConfigureAwait(false);
                    await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    await integrationEventPublisher
                        .PublishIntegrationEventsAsync(garden.DomainEvents, cancellationToken)
                        .ConfigureAwait(false);

                    var response = MapToResponse(garden);
                    return TypedResults.Created(
                        $"/api/v1/management/gardens/{response.Id}",
                        response);
                })
            .WithName("CreateGarden")
            .WithSummary("Creates a new garden.")
            .WithDescription("Creates a new garden for a user and returns the created resource.");

        group.MapPut(
                "/{gardenId:guid}",
                async Task<Results<Ok<GardenResponse>, NotFound, ValidationProblem>> (
                    [FromServices] IGardenRepository gardenRepository,
                    [FromServices] IManagementUnitOfWork unitOfWork,
                    [FromServices] IGardenIntegrationEventPublisher integrationEventPublisher,
                    Guid gardenId,
                    [FromBody] UpdateGardenRequest request,
                    CancellationToken cancellationToken) =>
                {
                    var garden = await gardenRepository
                        .GetByIdAsync(new GardenId(gardenId), cancellationToken)
                        .ConfigureAwait(false);

                    if (garden is null)
                    {
                        return TypedResults.NotFound();
                    }

                    var utcNow = DateTimeOffset.UtcNow;

                    var anyChange = false;

                    if (!string.Equals(request.Name, garden.Name, StringComparison.Ordinal))
                    {
                        var renameResult = garden.Rename(request.Name, utcNow);
                        if (renameResult.IsFailure)
                        {
                            return CreateValidationProblem(renameResult.Error!);
                        }

                        anyChange = true;
                    }

                    if (request.TotalSurfaceArea != garden.TotalSurfaceArea.Value)
                    {
                        var surfaceResult = garden.ChangeSurfaceArea(request.TotalSurfaceArea, utcNow);
                        if (surfaceResult.IsFailure)
                        {
                            return CreateValidationProblem(surfaceResult.Error!);
                        }

                        anyChange = true;
                    }

                    if (request.TargetHumidityLevel != garden.TargetHumidityLevel.Value)
                    {
                        var humidityResult = garden.ChangeTargetHumidity(request.TargetHumidityLevel, utcNow);
                        if (humidityResult.IsFailure)
                        {
                            return CreateValidationProblem(humidityResult.Error!);
                        }

                        anyChange = true;
                    }

                    if (!anyChange)
                    {
                        return TypedResults.Ok(MapToResponse(garden));
                    }

                    await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    await integrationEventPublisher
                        .PublishIntegrationEventsAsync(garden.DomainEvents, cancellationToken)
                        .ConfigureAwait(false);

                    return TypedResults.Ok(MapToResponse(garden));
                })
            .WithName("UpdateGarden")
            .WithSummary("Updates an existing garden.")
            .WithDescription("Updates an existing garden's details.");

        group.MapDelete(
                "/{gardenId:guid}",
                async Task<Results<NoContent, NotFound>> (
                    [FromServices] IGardenRepository gardenRepository,
                    [FromServices] IManagementUnitOfWork unitOfWork,
                    [FromServices] IGardenIntegrationEventPublisher integrationEventPublisher,
                    Guid gardenId,
                    CancellationToken cancellationToken) =>
                {
                    var garden = await gardenRepository
                        .GetByIdAsync(new GardenId(gardenId), cancellationToken)
                        .ConfigureAwait(false);

                    if (garden is null)
                    {
                        return TypedResults.NotFound();
                    }

                    var utcNow = DateTimeOffset.UtcNow;
                    garden.MarkDeleted(utcNow);

                    gardenRepository.Remove(garden);
                    await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    await integrationEventPublisher
                        .PublishIntegrationEventsAsync(garden.DomainEvents, cancellationToken)
                        .ConfigureAwait(false);

                    return TypedResults.NoContent();
                })
            .WithName("DeleteGarden")
            .WithSummary("Deletes a garden.")
            .WithDescription("Deletes a garden by its identifier.");

        return endpoints;
    }

    private static ValidationProblem CreateValidationProblem(Error error)
    {
        var validationErrors = error.ValidationErrors ?? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        return TypedResults.ValidationProblem(validationErrors);
    }

    private static GardenResponse MapToResponse(Garden garden) =>
        new(
            garden.Id.Value,
            garden.UserId.Value,
            garden.Name,
            garden.TotalSurfaceArea.Value,
            garden.TargetHumidityLevel.Value,
            garden.CreatedAt,
            garden.UpdatedAt);
}

