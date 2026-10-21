namespace MicroLIMS.Domain.Entities;

// A test an occurrence requires, snapshotted at generation. Exactly one of
// RoomTestConfigurationId, MachinePartConfigurationId or TestCode (Water) is set.
public class MonitoringOccurrenceItem
{
    public int Id { get; set; }
    public int MonitoringOccurrenceId { get; set; }
    public MonitoringOccurrence? MonitoringOccurrence { get; set; }

    public int? RoomTestConfigurationId { get; set; }
    public int? MachinePartConfigurationId { get; set; }
    public string? TestCode { get; set; }
}
