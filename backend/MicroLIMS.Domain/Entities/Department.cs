namespace MicroLIMS.Domain.Entities;

public class Department : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty; // e.g. Grade A/B/C/D classification of the department
    public string TestingFrequency { get; set; } = string.Empty; // informational only, e.g. "Monthly"
    public List<Room> Rooms { get; set; } = new();
}
