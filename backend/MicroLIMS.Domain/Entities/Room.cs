using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class Room : IVersionedEntity, IHasFrequency
{
    public int Id { get; set; }
    public uint Version { get; set; }
    // Inactive rows are kept for history but hidden from receiving and
    // preparation; a row referenced by samples is deactivated, not deleted.
    public bool IsActive { get; set; } = true;

    public FrequencyType? FrequencyType { get; set; }
    public int? FrequencyInterval { get; set; }
    public DayOfWeek? SamplingWeekday { get; set; }
    public int? FrequencyAnchorMonth { get; set; }
    public int? FrequencyWeekOfMonth { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public Department? Department { get; set; }
    public string GradeClassification { get; set; } = string.Empty; // e.g. Grade A/B/C/D

    // An LAF sits inside a Room (ParentRoomId), has its own grade, test
    // configurations and limits, and may override the frequency itself.
    public RoomKind Kind { get; set; } = RoomKind.Room;
    public int? ParentRoomId { get; set; }
    public Room? ParentRoom { get; set; }

    // Passive Air Sample (Settle Plate) / Surface Air Sample (Contact
    // Plate) - each with its own Alert/Action/Spec limits, configured
    // by Section Head. Checking one of these at preparation time is
    // what generates the TestOrder for that room.
    public List<RoomTestConfiguration> TestConfigurations { get; set; } = new();
}
