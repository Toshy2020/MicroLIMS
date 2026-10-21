namespace MicroLIMS.Domain.Entities;

public class RoomTestConfiguration : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    // Inactive rows are kept for history but hidden from receiving and
    // preparation; a row referenced by samples is deactivated, not deleted.
    public bool IsActive { get; set; } = true;
    public int RoomId { get; set; }
    public Room? Room { get; set; }
    public string TestType { get; set; } = string.Empty; // one of RoomTestTypes.All
    public string TestCode { get; set; } = string.Empty; // e.g. "EM_TAMC"
    public string AlertLimit { get; set; } = string.Empty;
    public string ActionLimit { get; set; } = string.Empty;
    public string SpecLimit { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
}
