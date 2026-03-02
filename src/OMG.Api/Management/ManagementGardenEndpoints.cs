using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OMG.Api.Management.Models;
using OMG.Api.Security;
using OMG.Management.Domain.Abstractions;
using OMG.Management.Domain.Common;
using OMG.Management.Domain.Gardens;
using OMG.Management.Infrastructure;
using OMG.Management.Infrastructure.Entities;
using OMG.Management.Infrastructure.Messaging;

namespace OMG.Api.Management;

public static class ManagementGardenEndpoints
{
    public static IEndpointRouteBuilder MapManagementGardenEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/management/gardens")
            .WithTags("Garden Management")
            .RequireAuthorization();

        group.MapGet(
                "/",
                async Task<Results<Ok<IReadOnlyList<GardenResponse>>, UnauthorizedHttpResult>> (
                    [FromServices] ManagementDbContext dbContext,
                    ClaimsPrincipal user,
                    CancellationToken cancellationToken) =>
                {
                    var currentUserId = user.GetDomainUserId();
                    if (currentUserId is null)
                    {
                        return TypedResults.Unauthorized();
                    }

                    var gardens = await dbContext.Database
                        .SqlQueryRaw<GardenResponse>(
                            """
                            SELECT g."Id",
                                   g."Name",
                                   g."TotalSurfaceArea",
                                   g."TargetHumidityLevel",
                                   g."CreatedAt",
                                   g."UpdatedAt"
                            FROM "gm"."gardens" AS g
                            WHERE g."Deleted" = FALSE
                              AND g."UserId" = {0}
                            """,
                            currentUserId.Value.Value)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);

                    return TypedResults.Ok((IReadOnlyList<GardenResponse>)gardens);
                })
            .WithName("GetGardensForUser")
            .WithSummary("Lists all gardens for the specified user.")
            .WithDescription("Returns all gardens owned by the specified user.");

        group.MapGet(
                "/{gardenId:guid}",
                async Task<Results<Ok<GardenResponse>, NotFound, UnauthorizedHttpResult>> (
                    [FromServices] ManagementDbContext dbContext,
                    ClaimsPrincipal user,
                    Guid gardenId,
                    CancellationToken cancellationToken) =>
                {
                    var currentUserId = user.GetDomainUserId();
                    if (currentUserId is null)
                    {
                        return TypedResults.Unauthorized();
                    }

                    var garden = await dbContext.Database
                        .SqlQueryRaw<GardenResponse>(
                            """
                            SELECT g."Id",
                                   g."Name",
                                   g."TotalSurfaceArea",
                                   g."TargetHumidityLevel",
                                   g."CreatedAt",
                                   g."UpdatedAt"
                            FROM "gm"."gardens" AS g
                            WHERE g."Deleted" = FALSE
                              AND g."UserId" = {0}
                              AND g."Id" = {1}
                            """,
                            currentUserId.Value.Value,
                            gardenId)
                        .FirstOrDefaultAsync(cancellationToken)
                        .ConfigureAwait(false);

                    if (garden is null)
                    {
                        return TypedResults.NotFound();
                    }

                    return TypedResults.Ok(garden);
                })
            .WithName("GetGardenById")
            .WithSummary("Gets a single garden's details.")
            .WithDescription("Returns the details of a garden by its identifier.");

        group.MapPost(
                "/",
                async Task<Results<Created<GardenResponse>, ValidationProblem, UnauthorizedHttpResult>> (
                    [FromServices] IGardenRepository gardenRepository,
                    [FromServices] IManagementUnitOfWork unitOfWork,
                    [FromServices] IGardenIntegrationEventPublisher integrationEventPublisher,
                    ClaimsPrincipal user,
                    [FromBody] CreateGardenRequest request,
                    CancellationToken cancellationToken) =>
                {
                    var currentUserId = user.GetDomainUserId();
                    if (currentUserId is null)
                    {
                        return TypedResults.Unauthorized();
                    }

                    var utcNow = DateTimeOffset.UtcNow;

                    var result = Garden.Create(
                        currentUserId.Value,
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
                    await integrationEventPublisher
                        .PublishIntegrationEventsAsync(garden.DomainEvents, cancellationToken)
                        .ConfigureAwait(false);
                    await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

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
                async Task<Results<Ok<GardenResponse>, NotFound, ValidationProblem, UnauthorizedHttpResult>> (
                    [FromServices] IGardenRepository gardenRepository,
                    [FromServices] IManagementUnitOfWork unitOfWork,
                    [FromServices] IGardenIntegrationEventPublisher integrationEventPublisher,
                    ClaimsPrincipal user,
                    Guid gardenId,
                    [FromBody] UpdateGardenRequest request,
                    CancellationToken cancellationToken) =>
                {
                    var currentUserId = user.GetDomainUserId();
                    if (currentUserId is null)
                    {
                        return TypedResults.Unauthorized();
                    }

                    var garden = await gardenRepository
                        .GetByIdAsync(new GardenId(gardenId), cancellationToken)
                        .ConfigureAwait(false);

                    if (garden is null || garden.UserId != currentUserId.Value)
                    {
                        return TypedResults.NotFound();
                    }

                    var utcNow = DateTimeOffset.UtcNow;

                    var anyChange = false;

                    var trimmedName = request.Name?.Trim();
                    if (trimmedName is not null && !string.Equals(trimmedName, garden.Name, StringComparison.Ordinal))
                    {
                        var renameResult = garden.Rename(trimmedName, utcNow);
                        if (renameResult.IsFailure)
                        {
                            return CreateValidationProblem(renameResult.Error!);
                        }

                        anyChange = true;
                    }

                    if (request.TotalSurfaceArea is not null
                        && request.TotalSurfaceArea.Value != garden.TotalSurfaceArea.Value)
                    {
                        var surfaceResult = garden.ChangeSurfaceArea(request.TotalSurfaceArea.Value, utcNow);
                        if (surfaceResult.IsFailure)
                        {
                            return CreateValidationProblem(surfaceResult.Error!);
                        }

                        anyChange = true;
                    }

                    if (request.TargetHumidityLevel is not null
                        && request.TargetHumidityLevel.Value != garden.TargetHumidityLevel.Value)
                    {
                        var humidityResult = garden.ChangeTargetHumidity(request.TargetHumidityLevel.Value, utcNow);
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

                    await gardenRepository.SaveAsync(garden, cancellationToken).ConfigureAwait(false);
                    await integrationEventPublisher
                        .PublishIntegrationEventsAsync(garden.DomainEvents, cancellationToken)
                        .ConfigureAwait(false);
                    await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    return TypedResults.Ok(MapToResponse(garden));
                })
            .WithName("UpdateGarden")
            .WithSummary("Updates an existing garden.")
            .WithDescription("Updates an existing garden's details.");

        group.MapDelete(
                "/{gardenId:guid}",
                async Task<Results<NoContent, NotFound, UnauthorizedHttpResult>> (
                    [FromServices] IGardenRepository gardenRepository,
                    [FromServices] IManagementUnitOfWork unitOfWork,
                    [FromServices] IGardenIntegrationEventPublisher integrationEventPublisher,
                    ClaimsPrincipal user,
                    Guid gardenId,
                    CancellationToken cancellationToken) =>
                {
                    var currentUserId = user.GetDomainUserId();
                    if (currentUserId is null)
                    {
                        return TypedResults.Unauthorized();
                    }

                    var garden = await gardenRepository
                        .GetByIdAsync(new GardenId(gardenId), cancellationToken)
                        .ConfigureAwait(false);

                    if (garden is null || garden.UserId != currentUserId.Value)
                    {
                        return TypedResults.NotFound();
                    }

                    var utcNow = DateTimeOffset.UtcNow;
                    garden.MarkDeleted(utcNow);

                    gardenRepository.Remove(garden);
                    await integrationEventPublisher
                        .PublishIntegrationEventsAsync(garden.DomainEvents, cancellationToken)
                        .ConfigureAwait(false);
                    await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

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
            garden.Name,
            garden.TotalSurfaceArea.Value,
            garden.TargetHumidityLevel.Value,
            garden.CreatedAt,
            garden.UpdatedAt);

    private static GardenResponse MapToResponse(GardenEntity garden) =>
        new(
            garden.Id,
            garden.Name,
            garden.TotalSurfaceArea,
            garden.TargetHumidityLevel,
            garden.CreatedAt,
            garden.UpdatedAt);
}

