namespace OMG.Api.Management.Models;

/// <summary>
/// Watering count for a single plant within the report period.
/// </summary>
public sealed record WateringFrequencyItem(Guid PlantId, int Count);

/// <summary>
/// Detailed garden/irrigation report: watered and unwatered counts, frequency per plant, and plant delta since a date.
/// </summary>
public sealed record GardenReportResponse(
    int WateredCount,
    int UnwateredCount,
    IReadOnlyList<WateringFrequencyItem> WateringFrequencyPerPlant,
    int PlantsAddedSince,
    int PlantsDeletedSince,
    DateTimeOffset? PeriodStart,
    DateTimeOffset? PeriodEnd);
