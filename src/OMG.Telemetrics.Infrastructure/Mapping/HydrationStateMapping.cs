using OMG.Telemetrics.Domain.Hydration;
using OMG.Telemetrics.Infrastructure.Entities;

namespace OMG.Telemetrics.Infrastructure.Mapping;

public static class HydrationStateMapping
{
    public static string ToString(PlantType type) => type.ToString();

    public static PlantType ToPlantType(string value)
    {
        return Enum.TryParse<PlantType>(value, ignoreCase: true, out var t) ? t : PlantType.Vegetable;
    }

    public static PlantHydrationStateEntity ToEntity(PlantHydrationState state)
    {
        return new PlantHydrationStateEntity
        {
            PlantId = state.PlantId,
            GardenId = state.GardenId,
            PlantType = ToString(state.PlantType),
            IdealHumidityLevel = state.IdealHumidityLevel,
            CurrentHumidity = state.CurrentHumidityLevel,
            LastIrrigationStart = state.LastIrrigationStart,
            LastIrrigationEnd = state.LastIrrigationEnd,
            IsWatering = state.IsWatering,
            HasIrrigationLine = state.HasIrrigationLine
        };
    }

    public static PlantHydrationState ToDomain(PlantHydrationStateEntity entity, WateringSession? activeSession)
    {
        return PlantHydrationState.FromPersistence(
            entity.PlantId,
            entity.GardenId,
            ToPlantType(entity.PlantType),
            entity.IdealHumidityLevel,
            entity.CurrentHumidity,
            entity.LastIrrigationStart,
            entity.LastIrrigationEnd,
            entity.IsWatering,
            entity.HasIrrigationLine,
            activeSession);
    }

    public static void ApplyToEntity(PlantHydrationState state, PlantHydrationStateEntity entity)
    {
        entity.PlantType = ToString(state.PlantType);
        entity.IdealHumidityLevel = state.IdealHumidityLevel;
        entity.CurrentHumidity = state.CurrentHumidityLevel;
        entity.LastIrrigationStart = state.LastIrrigationStart;
        entity.LastIrrigationEnd = state.LastIrrigationEnd;
        entity.IsWatering = state.IsWatering;
        entity.HasIrrigationLine = state.HasIrrigationLine;
    }

    public static WateringSessionEntity ToEntity(WateringSession session)
    {
        return new WateringSessionEntity
        {
            SessionId = session.SessionId,
            PlantId = session.PlantId,
            GardenId = session.GardenId,
            StartedAt = session.StartedAt,
            EndsAt = session.EndsAt,
            Status = session.Status.ToString()
        };
    }

    public static WateringSession ToDomain(WateringSessionEntity entity)
    {
        return WateringSession.FromPersistence(
            entity.SessionId,
            entity.PlantId,
            entity.GardenId,
            entity.StartedAt,
            entity.EndsAt,
            Enum.TryParse<WateringSessionStatus>(entity.Status, ignoreCase: true, out var s) ? s : WateringSessionStatus.Started);
    }
}
