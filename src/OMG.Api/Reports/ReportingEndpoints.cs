using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OMG.Api.Management.Models;
using OMG.Api.Security;
using OMG.Management.Infrastructure;
using OMG.Telemetrics.Infrastructure;

namespace OMG.Api.Reports;

/// <summary>
/// Result row for raw SQL count queries (e.g. COUNT(*) AS "Count").
/// </summary>
internal sealed record CountRow(long Count);

public static class ReportingEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/reports")
            .WithTags("Reports")
            .RequireAuthorization();

        group.MapGet(
                "/garden-report",
                async Task<Results<Ok<GardenReportResponse>, UnauthorizedHttpResult>> (
                    [FromServices] ManagementDbContext managementDbContext,
                    [FromServices] TelemetryDbContext telemetryDbContext,
                    ClaimsPrincipal user,
                    [FromQuery] Guid? gardenId,
                    [FromQuery] DateTimeOffset? from,
                    [FromQuery] DateTimeOffset? to,
                    [FromQuery] int? lastMinutes, 
                    CancellationToken cancellationToken) =>
                {
                    var currentUserId = user.GetDomainUserId();
                    if (currentUserId is null)
                    {
                        return TypedResults.Unauthorized();
                    }

                    if (managementDbContext.Database.IsInMemory() || telemetryDbContext.Database.IsInMemory())
                    {
                        var periodEnd = DateTimeOffset.UtcNow;
                        var periodStart = lastMinutes.HasValue ? periodEnd.AddMinutes(-lastMinutes.Value) : (from ?? periodEnd.AddHours(-1));
                        var periodEndParam = lastMinutes.HasValue ? periodEnd : (to ?? periodEnd);
                        return TypedResults.Ok(new GardenReportResponse(
                            WateredCount: 0,
                            UnwateredCount: 0,
                            WateringFrequencyPerPlant: [],
                            PlantsAddedSince: 0,
                            PlantsDeletedSince: 0,
                            PeriodStart: periodStart,
                            PeriodEnd: periodEndParam));
                    }

                    var gardenIds = await managementDbContext.Gardens
                        .AsNoTracking()
                        .Where(g => g.UserId == currentUserId.Value.Value && !g.Deleted)
                        .Select(g => g.Id)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);

                    if (gardenId.HasValue)
                    {
                        if (!gardenIds.Contains(gardenId.Value))
                        {
                            return TypedResults.Ok(new GardenReportResponse(
                                WateredCount: 0,
                                UnwateredCount: 0,
                                WateringFrequencyPerPlant: [],
                                PlantsAddedSince: 0,
                                PlantsDeletedSince: 0, 
                                PeriodStart: null,
                                PeriodEnd: null));
                        }

                        gardenIds = [gardenId.Value];
                    }

                    var periodEndUtc = to ?? DateTimeOffset.UtcNow;
                    var periodStartUtc = from ?? (lastMinutes.HasValue ? periodEndUtc.AddMinutes(-lastMinutes.Value) : periodEndUtc.AddHours(-1));

                    var totalPlantsInScope = 0L;
                    var wateredCount = 0L;
                    var frequency = new List<WateringFrequencyItem>();

                    if (gardenIds.Count > 0)
                    {
                        var gardenIdsArray = gardenIds.ToArray();
                        totalPlantsInScope = await managementDbContext.Database
                            .SqlQueryRaw<CountRow>(
                                """
                                SELECT COUNT(*) AS "Count"
                                FROM "gm"."plants" AS p
                                WHERE p."GardenId" = ANY({0}) 
                                  AND p."DeletedAt" IS NULL
                                """,
                                gardenIdsArray)
                            .Select(r => r.Count)
                            .FirstOrDefaultAsync(cancellationToken)
                            .ConfigureAwait(false); 

                        wateredCount = await telemetryDbContext.Database
                            .SqlQueryRaw<CountRow>(
                                """
                                SELECT COUNT(DISTINCT ws."PlantId") AS "Count"
                                FROM "telemetry"."watering_sessions" AS ws
                                INNER JOIN "telemetry"."plants" AS tp ON tp."PlantId" = ws."PlantId"
                                WHERE ws."EndedAt" IS NOT NULL
                                  AND ws."EndedAt" >= {0}
                                  AND ws."EndedAt" <= {1}
                                  AND tp."GardenId" = ANY({2})
                                """,
                                periodStartUtc,
                                periodEndUtc,
                                gardenIdsArray)
                            .Select(r => r.Count)
                            .FirstOrDefaultAsync(cancellationToken)
                            .ConfigureAwait(false);

                        frequency = await telemetryDbContext.Database
                            .SqlQueryRaw<WateringFrequencyItem>(
                                """
                                SELECT ws."PlantId" AS "PlantId", COUNT(*)::int AS "Count"
                                FROM "telemetry"."watering_sessions" AS ws
                                INNER JOIN "telemetry"."plants" AS tp ON tp."PlantId" = ws."PlantId"
                                WHERE ws."EndedAt" IS NOT NULL
                                  AND ws."EndedAt" >= {0}
                                  AND ws."EndedAt" <= {1}
                                  AND tp."GardenId" = ANY({2})
                                GROUP BY ws."PlantId"
                                """,
                                periodStartUtc,
                                periodEndUtc,
                                gardenIdsArray)
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(false);
                    }

                    var plantsAddedSince = 0L;
                    var plantsDeletedSince = 0L;

                    if (gardenIds.Count > 0 )
                    {
                        var gardenIdsArray = gardenIds.ToArray();

                        plantsAddedSince = await managementDbContext.Database
                            .SqlQueryRaw<CountRow>(
                                """
                                SELECT COUNT(*) AS "Count"
                                FROM "gm"."plants" AS p
                                WHERE p."GardenId" = ANY({0})
                                  AND p."CreatedAt" >= {1}
                                  AND p."CreatedAt" <= {2}
                                """,
                                gardenIdsArray,
                                periodStartUtc,
                                periodEndUtc
                               )
                            .Select(r => r.Count)
                            .FirstOrDefaultAsync(cancellationToken)
                            .ConfigureAwait(false);

                        plantsDeletedSince = await managementDbContext.Database
                            .SqlQueryRaw<CountRow>(
                                """
                                SELECT COUNT(*) AS "Count"
                                FROM "gm"."plants" AS p
                                WHERE p."GardenId" = ANY({0})
                                  AND p."DeletedAt" IS NOT NULL
                                  AND p."DeletedAt" >= {1}
                                  AND p."DeletedAt" <= {2}
                                """,
                                gardenIdsArray,
                                 periodStartUtc,
                                periodEndUtc)
                            .Select(r => r.Count)
                            .FirstOrDefaultAsync(cancellationToken)
                            .ConfigureAwait(false);
                    }

                    var report = new GardenReportResponse(
                        WateredCount: (int)wateredCount,
                        UnwateredCount: (int)Math.Max(0, totalPlantsInScope - wateredCount),
                        WateringFrequencyPerPlant: frequency,
                        PlantsAddedSince: (int)plantsAddedSince,
                        PlantsDeletedSince: (int)plantsDeletedSince, 
                        PeriodStart: periodStartUtc,
                        PeriodEnd: periodEndUtc);

                    return TypedResults.Ok(report);
                })
            .WithName("GetGardenReport")
            .WithSummary("Garden / irrigation report")
            .WithDescription("Returns a detailed report: watered and unwatered plant counts, watering frequency per plant in the period, and plants added or deleted since a date. Restricted to the current user's gardens.");

        return endpoints;
    }
}
