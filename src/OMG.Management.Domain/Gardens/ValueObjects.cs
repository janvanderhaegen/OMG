namespace OMG.Management.Domain.Gardens;

public readonly record struct GardenId(Guid Value)
{
    public static GardenId New() => new(Guid.NewGuid());

    public static GardenId From(Guid value) => new(value);
}

public readonly record struct UserId(Guid Value)
{
    public static UserId From(Guid value) => new(value);
}

public readonly record struct SurfaceArea(decimal Value)
{
    public static SurfaceArea From(decimal value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Surface area must be positive.");
        }

        return new SurfaceArea(value);
    }
}

public readonly record struct HumidityLevel(int Value)
{
    public static HumidityLevel From(int value)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Humidity level must be between 0 and 100.");
        }

        return new HumidityLevel(value);
    }
}

