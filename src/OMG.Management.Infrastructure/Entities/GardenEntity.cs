namespace OMG.Management.Infrastructure.Entities;

public class GardenEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal TotalSurfaceArea { get; set; }

    public int TargetHumidityLevel { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool Deleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

