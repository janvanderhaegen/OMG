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

public static class ManagementPlantEndpoints
{
    public static IEndpointRouteBuilder MapManagementPlantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/management/gardens/{gardenId:guid}/plants")
            .WithTags("Plant Management")
            .RequireAuthorization();

        group.MapGet(
                "/",
                async Task<Results<Ok<IReadOnlyList<PlantResponse>>, NotFound, UnauthorizedHttpResult>> (
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
                            SELECT g.[Id],
                                   g.[Name],
                                   g.[TotalSurfaceArea],
                                   g.[TargetHumidityLevel],
                                   g.[CreatedAt],
                                   g.[UpdatedAt]
                            FROM [gm].[gardens] AS g
                            WHERE g.[Deleted] = 0
                              AND g.[UserId] = {0}
                              AND g.[Id] = {1}
                            """,
                            currentUserId.Value,
                            gardenId)
                        .FirstOrDefaultAsync(cancellationToken)
                        .ConfigureAwait(false);

                    if (garden is null)
                    {
                        return TypedResults.NotFound();
                    }

                    var plants = await dbContext.Database
                        .SqlQueryRaw<PlantResponse>(
                            """
                            SELECT p.[Id],
                                   p.[GardenId],
                                   p.[Name],
                                   p.[Species],
                                   p.[Type],
                                   p.[PlantationDate],
                                   p.[SurfaceAreaRequired],
                                   p.[IdealHumidityLevel]
                            FROM [gm].[plants] AS p
                            WHERE p.[GardenId] = {0}
                            """,
                            gardenId)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);

                    return TypedResults.Ok((IReadOnlyList<PlantResponse>)plants);
                })
            .WithName("GetPlantsForGarden")
            .WithSummary("Lists all plants in the specified garden.")
            .WithDescription("Returns all plants that belong to the specified garden.");

        group.MapGet(
                "/{plantId:guid}",
                async Task<Results<Ok<PlantResponse>, NotFound, UnauthorizedHttpResult>> (
                    [FromServices] ManagementDbContext dbContext,
                    ClaimsPrincipal user,
                    Guid gardenId,
                    Guid plantId,
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
                            SELECT g.[Id],
                                   g.[Name],
                                   g.[TotalSurfaceArea],
                                   g.[TargetHumidityLevel],
                                   g.[CreatedAt],
                                   g.[UpdatedAt]
                            FROM [gm].[gardens] AS g
                            WHERE g.[Deleted] = 0
                              AND g.[UserId] = {0}
                              AND g.[Id] = {1}
                            """,
                            currentUserId.Value,
                            gardenId)
                        .FirstOrDefaultAsync(cancellationToken)
                        .ConfigureAwait(false);

                    if (garden is null)
                    {
                        return TypedResults.NotFound();
                    }

                    var plant = await dbContext.Database
                        .SqlQueryRaw<PlantResponse>(
                            """
                            SELECT p.[Id],
                                   p.[GardenId],
                                   p.[Name],
                                   p.[Species],
                                   p.[Type],
                                   p.[PlantationDate],
                                   p.[SurfaceAreaRequired],
                                   p.[IdealHumidityLevel]
                            FROM [gm].[plants] AS p
                            WHERE p.[GardenId] = {0}
                              AND p.[Id] = {1}
                            """,
                            gardenId,
                            plantId)
                        .FirstOrDefaultAsync(cancellationToken)
                        .ConfigureAwait(false);

                    if (plant is null)
                    {
                        return TypedResults.NotFound();
                    }

                    return TypedResults.Ok(plant);
                })
            .WithName("GetPlantById")
            .WithSummary("Gets a single plant's details.")
            .WithDescription("Returns the details of a plant in the specified garden.");

        group.MapPost(
                "/",
                async Task<Results<Created<PlantResponse>, NotFound, ValidationProblem, UnauthorizedHttpResult>> (
                    [FromServices] IGardenRepository gardenRepository,
                    [FromServices] IManagementUnitOfWork unitOfWork,
                    [FromServices] IGardenIntegrationEventPublisher integrationEventPublisher,
                    ClaimsPrincipal user,
                    Guid gardenId,
                    [FromBody] CreatePlantRequest request,
                    CancellationToken cancellationToken) =>
                {
                    var currentUserId = user.GetDomainUserId();
                    if (currentUserId is null)
                    {
                        return TypedResults.Unauthorized();
                    }

                    var garden = await gardenRepository
                        .GetByIdWithPlantsAsync(new GardenId(gardenId), cancellationToken)
                        .ConfigureAwait(false);

                    if (garden is null || garden.UserId != currentUserId.Value)
                    {
                        return TypedResults.NotFound();
                    }

                    if (!Enum.TryParse<PlantType>(request.Type, ignoreCase: true, out var plantType))
                    {
                        var validationErrors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["type"] = ["Invalid plant type. Allowed values are Vegetable, Fruit, or Flower."]
                        };

                        var error = new Error(
                            ErrorCodes.PlantValidationFailed,
                            "One or more validation errors occurred while adding a plant to a garden.",
                            validationErrors);

                        return CreateValidationProblem(error);
                    }

                    var utcNow = DateTimeOffset.UtcNow;

                    var result = garden.AddPlant(
                        request.Name,
                        request.Species,
                        plantType,
                        request.PlantationDate,
                        request.SurfaceAreaRequired,
                        request.IdealHumidityLevel,
                        utcNow);

                    if (result.IsFailure || result.Value is null)
                    {
                        return CreateValidationProblem(result.Error!);
                    }

                    var plant = result.Value;

                    await gardenRepository.AddPlantAsync(garden, plant, cancellationToken).ConfigureAwait(false);
                    await integrationEventPublisher
                        .PublishIntegrationEventsAsync(garden.DomainEvents, cancellationToken)
                        .ConfigureAwait(false);
                    await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    var response = MapToResponse(garden.Id.Value, plant);

                    return TypedResults.Created(
                        $"/api/v1/management/gardens/{garden.Id.Value}/plants/{response.Id}",
                        response);
                })
            .WithName("CreatePlant")
            .WithSummary("Creates a new plant in the specified garden.")
            .WithDescription("Creates a new plant in the specified garden and returns the created resource.");

        group.MapPut(
                "/{plantId:guid}",
                async Task<Results<Ok<PlantResponse>, NotFound, ValidationProblem, UnauthorizedHttpResult>> (
                    [FromServices] IGardenRepository gardenRepository,
                    [FromServices] IManagementUnitOfWork unitOfWork,
                    [FromServices] IGardenIntegrationEventPublisher integrationEventPublisher,
                    ClaimsPrincipal user,
                    Guid gardenId,
                    Guid plantId,
                    [FromBody] UpdatePlantRequest request,
                    CancellationToken cancellationToken) =>
                {
                    var currentUserId = user.GetDomainUserId();
                    if (currentUserId is null)
                    {
                        return TypedResults.Unauthorized();
                    }

                    var garden = await gardenRepository
                        .GetByIdWithPlantsAsync(new GardenId(gardenId), cancellationToken)
                        .ConfigureAwait(false);

                    if (garden is null || garden.UserId != currentUserId.Value)
                    {
                        return TypedResults.NotFound();
                    }

                    var plant = garden.Plants.FirstOrDefault(p => p.Id.Value == plantId);
                    if (plant is null)
                    {
                        return TypedResults.NotFound();
                    }

                    if (!Enum.TryParse<PlantType>(request.Type, ignoreCase: true, out var plantType))
                    {
                        var validationErrors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["type"] = ["Invalid plant type. Allowed values are Vegetable, Fruit, or Flower."]
                        };

                        var error = new Error(
                            ErrorCodes.PlantValidationFailed,
                            "One or more validation errors occurred while updating a plant in a garden.",
                            validationErrors);

                        return CreateValidationProblem(error);
                    }

                    var utcNow = DateTimeOffset.UtcNow;
                    var plantIdDomain = new PlantId(plantId);

                    var trimmedName = request.Name?.Trim();
                    if (trimmedName is not null && !string.Equals(trimmedName, plant.Name, StringComparison.Ordinal))
                    {
                        var result = garden.RenamePlant(plantIdDomain, trimmedName, utcNow);
                        if (result.IsFailure)
                            return CreateValidationProblem(result.Error!);
                    }

                    var trimmedSpecies = request.Species?.Trim() ?? plant.Species;
                    if (!string.Equals(trimmedSpecies, plant.Species, StringComparison.Ordinal) || plant.Type != plantType)
                    {
                        var result = garden.ReclassifyPlant(plantIdDomain, trimmedSpecies, plantType, utcNow);
                        if (result.IsFailure)
                            return CreateValidationProblem(result.Error!);
                    }

                    if (request.SurfaceAreaRequired != plant.SurfaceAreaRequired.Value)
                    {
                        var result = garden.DefineSurfaceAreaRequirement(plantIdDomain, request.SurfaceAreaRequired, utcNow);
                        if (result.IsFailure)
                            return CreateValidationProblem(result.Error!);
                    }

                    if (request.IdealHumidityLevel != plant.IdealHumidityLevel.Value)
                    {
                        var result = garden.AdjustIdealHumidity(plantIdDomain, request.IdealHumidityLevel, utcNow);
                        if (result.IsFailure)
                            return CreateValidationProblem(result.Error!);
                    }

                    if (request.PlantationDate != plant.PlantationDate)
                    {
                        var result = garden.SetPlantationDate(plantIdDomain, request.PlantationDate, utcNow);
                        if (result.IsFailure)
                            return CreateValidationProblem(result.Error!);
                    }

                    await gardenRepository.SaveAsync(garden, cancellationToken).ConfigureAwait(false);
                    await integrationEventPublisher
                        .PublishIntegrationEventsAsync(garden.DomainEvents, cancellationToken)
                        .ConfigureAwait(false);
                    await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    var response = MapToResponse(garden.Id.Value, plant);

                    return TypedResults.Ok(response);
                })
            .WithName("UpdatePlant")
            .WithSummary("Updates an existing plant in the specified garden.")
            .WithDescription("Updates an existing plant's details in the specified garden.");

        group.MapDelete(
                "/{plantId:guid}",
                async Task<Results<NoContent, NotFound, UnauthorizedHttpResult>> (
                    [FromServices] IGardenRepository gardenRepository,
                    [FromServices] IManagementUnitOfWork unitOfWork,
                    [FromServices] IGardenIntegrationEventPublisher integrationEventPublisher,
                    ClaimsPrincipal user,
                    Guid gardenId,
                    Guid plantId,
                    CancellationToken cancellationToken) =>
                {
                    var currentUserId = user.GetDomainUserId();
                    if (currentUserId is null)
                    {
                        return TypedResults.Unauthorized();
                    }

                    var garden = await gardenRepository
                        .GetByIdWithPlantsAsync(new GardenId(gardenId), cancellationToken)
                        .ConfigureAwait(false);

                    if (garden is null || garden.UserId != currentUserId.Value)
                    {
                        return TypedResults.NotFound();
                    }

                    var plant = garden.Plants.FirstOrDefault(p => p.Id.Value == plantId);
                    if (plant is null)
                    {
                        return TypedResults.NotFound();
                    }

                    var utcNow = DateTimeOffset.UtcNow;

                    var result = garden.RemovePlant(new PlantId(plantId), utcNow);
                    if (result.IsFailure)
                    {
                        return TypedResults.NotFound();
                    }

                    await gardenRepository.SaveAsync(garden, cancellationToken).ConfigureAwait(false);
                    await integrationEventPublisher
                        .PublishIntegrationEventsAsync(garden.DomainEvents, cancellationToken)
                        .ConfigureAwait(false);
                    await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    return TypedResults.NoContent();
                })
            .WithName("DeletePlant")
            .WithSummary("Deletes a plant from the specified garden.")
            .WithDescription("Deletes a plant from the specified garden by its identifier.");

        return endpoints;
    }

    private static ValidationProblem CreateValidationProblem(Error error)
    {
        var validationErrors = error.ValidationErrors ?? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        return TypedResults.ValidationProblem(validationErrors);
    }

    private static PlantResponse MapToResponse(Guid gardenId, Plant plant) =>
        new(
            plant.Id.Value,
            gardenId,
            plant.Name,
            plant.Species,
            plant.Type.ToString(),
            plant.PlantationDate,
            plant.SurfaceAreaRequired.Value,
            plant.IdealHumidityLevel.Value);

    private static PlantResponse MapToResponse(Guid gardenId, PlantEntity plant) =>
        new(
            plant.Id,
            gardenId,
            plant.Name,
            plant.Species,
            plant.Type,
            plant.PlantationDate,
            plant.SurfaceAreaRequired,
            plant.IdealHumidityLevel);
}

