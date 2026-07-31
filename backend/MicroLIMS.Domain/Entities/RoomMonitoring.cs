namespace MicroLIMS.Domain.Entities;

// One EM sampling event at one position in one room - the record the
// EM Engine reads/writes for OOT trending over time.
public class RoomMonitoring
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public Room? Room { get; set; }
    public string SamplingPosition { get; set; } = string.Empty;
    public int TestOrderId { get; set; }
    public TestOrder? TestOrder { get; set; }
    public int Step1Count { get; set; }
    public int Step2Count { get; set; }
    public bool IsOutOfTrend { get; set; }
    public DateTime SampledAt { get; set; } = DateTime.UtcNow;
}
