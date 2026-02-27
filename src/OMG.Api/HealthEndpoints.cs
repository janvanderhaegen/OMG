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

public static class Health
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/health")
            .WithTags("System");
        group.MapGet("/", async Task<IResult> (ManagementDbContext dbContext, CancellationToken cancellationToken) =>
        {
            try
            {
                // In test environment we use an in-memory provider; a simple connectivity check is enough.
                if (!dbContext.Database.IsInMemory())
                {
                    // For relational providers, touch the database and fail fast if unavailable.
                    await dbContext.Database.ExecuteSqlRawAsync("SELECT 1 FROM gm.gardens", cancellationToken);
                }

                return Results.Ok(new HealthResponse(
                    Status: "Healthy",
                    Service: "OMG.Api",
                    Version: "1.0.0",
                    UtcTimestamp: DateTimeOffset.UtcNow));
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    title: "Dependency health check failed.",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    instance: "/api/v1/health",
                    type: "https://httpstatuses.com/503");
            }
        })
       .WithName("GetHealth")
       .WithSummary("Health check")
       .WithDescription("Checks basic readiness for the Open Modular Gardening backend by verifying API availability and  database connectivity.");

        return endpoints;
    }

}

