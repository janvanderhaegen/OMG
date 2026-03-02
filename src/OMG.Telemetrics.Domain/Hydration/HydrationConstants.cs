namespace OMG.Telemetrics.Domain.Hydration;

/// <summary>
/// Hardcoded irrigation simulation rules: decay per minute and watering increase by plant type.
/// </summary>
public static class HydrationConstants
{
    public const int InitialHumidityPercent = 50;
    public const int WateringDurationMinutes = 2;

    /// <summary>Humidity decay per minute: Vegetable 1%, Fruit 3%, Flower 4%.</summary>
    public static int DecayPercentPerMinute(PlantType type) => type switch
    {
        PlantType.Vegetable => 1,
        PlantType.Fruit => 3,
        PlantType.Flower => 4,
        _ => 1
    };

    /// <summary>Humidity increase after a full watering session: Vegetable 16%, Fruit 18%, Flower 20%.</summary>
    public static int WateringIncreasePercent(PlantType type) => type switch
    {
        PlantType.Vegetable => 16,
        PlantType.Fruit => 18,
        PlantType.Flower => 20,
        _ => 16
    };
}
