namespace MicroLIMS.Domain.Entities;

// Denormalized EM-specific room record capturing the sampling positions
// used by the EM workflow (settle plates, swabs, air sampling).
public class EMRoom
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public Room? Room { get; set; }
    public List<string> SamplingPositions { get; set; } = new();
}
