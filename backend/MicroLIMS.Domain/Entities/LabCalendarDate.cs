namespace MicroLIMS.Domain.Entities;

// A non-working date (public holiday, shutdown) in the laboratory calendar.
public class LabCalendarDate : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public DateOnly Date { get; set; }
    public string Description { get; set; } = string.Empty;
}
