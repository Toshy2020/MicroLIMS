using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// One row per Room/MachinePart included in an EM/After Cleaning batch
// sample's TestOrder - replaces the old model where each location got
// its own TestOrder. A TestOrder now represents one TestCode for the
// whole batch; this table is where the per-location result lives.
public class SampleLocation
{
    public int Id { get; set; }
    public int SampleId { get; set; }
    public Sample? Sample { get; set; }
    public int TestOrderId { get; set; }
    public TestOrder? TestOrder { get; set; }
    public LocationType LocationType { get; set; }
    public int? RoomTestConfigurationId { get; set; }
    public RoomTestConfiguration? RoomTestConfiguration { get; set; }
    public int? MachinePartConfigurationId { get; set; }
    public MachinePartConfiguration? MachinePartConfiguration { get; set; }

    // Shared across every location in the batch - set once when the
    // analyst opens the result grid for this TestOrder.
    public decimal DilutionFactor { get; set; }

    public decimal? CFUResult { get; set; }
    public decimal? CalculatedResult { get; set; }
    public string? ReportedResult { get; set; }

    // Snapshotted from the RoomTestConfiguration/MachinePartConfiguration
    // at result entry time, same reasoning as CountTestReading's own
    // Alert/Action/Spec snapshot - never re-read live after the fact.
    public string? AlertLimit { get; set; }
    public string? ActionLimit { get; set; }
    public string? SpecLimit { get; set; }
    public string? Status { get; set; }

    public DateTime? EnteredAt { get; set; }
    public int? EnteredByUserId { get; set; }
}
