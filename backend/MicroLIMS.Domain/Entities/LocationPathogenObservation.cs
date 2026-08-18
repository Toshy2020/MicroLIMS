using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class LocationPathogenObservation
{
    public int Id { get; set; }

    public int SampleLocationId { get; set; }
    public SampleLocation? SampleLocation { get; set; }

    public int TestOrderId { get; set; }
    public TestOrder? TestOrder { get; set; }

    public GrowthObservation GrowthObservation { get; set; }
    public string? SelectiveMediaSnapshot { get; set; } // ALCOA+ snapshot of selective media used

    public DateTime ObservedAt { get; set; } = DateTime.UtcNow;
    public int ObservedByUserId { get; set; }
    public User? ObservedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<ConfirmatoryPlateObservation> ConfirmatoryPlateObservations { get; set; } = new List<ConfirmatoryPlateObservation>();
}
