namespace MicroLIMS.Domain.Entities;

public class Room
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public Department? Department { get; set; }
    public string GradeClassification { get; set; } = string.Empty; // e.g. Grade A/B/C/D

    // Passive Air Sample (Settle Plate) / Surface Air Sample (Contact
    // Plate) - each with its own Alert/Action/Spec limits, configured
    // by Section Head. Checking one of these at preparation time is
    // what generates the TestOrder for that room.
    public List<RoomTestConfiguration> TestConfigurations { get; set; } = new();
}
