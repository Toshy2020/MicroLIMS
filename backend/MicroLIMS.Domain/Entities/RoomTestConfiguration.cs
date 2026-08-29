namespace MicroLIMS.Domain.Entities;

public class RoomTestConfiguration
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public Room? Room { get; set; }
    public string TestType { get; set; } = string.Empty; // "PassiveAirSample" or "SurfaceAirSample"
    public string TestCode { get; set; } = string.Empty; // e.g. "EM_TAMC"
    public string AlertLimit { get; set; } = string.Empty;
    public string ActionLimit { get; set; } = string.Empty;
    public string SpecLimit { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
}
