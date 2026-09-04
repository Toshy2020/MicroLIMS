using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class DocumentMasterAssignment
{
    public int Id { get; set; }

    public int DocumentMasterId { get; set; }
    public DocumentMaster DocumentMaster { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public AssignmentRole AssignmentRole { get; set; }
    public bool IsActive { get; set; } = true;

    public int AssignedByUserId { get; set; }
    public User AssignedByUser { get; set; } = null!;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
