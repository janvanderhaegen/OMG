namespace OMG.Management.Infrastructure.Entities;

public class PlantEntity
{
    public Guid Id { get; set; }

    public Guid GardenId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Species { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public DateTimeOffset PlantationDate { get; set; }

    public decimal SurfaceAreaRequired { get; set; }

    public int IdealHumidityLevel { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

