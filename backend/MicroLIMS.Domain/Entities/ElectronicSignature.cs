using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// 21 CFR Part 11 electronic signature record.
//
// APPEND-ONLY: once written, a row here must NEVER be updated or
// deleted - not even by SystemAdministrator. No service method or API
// endpoint exists to modify or remove a signature, and none should ever
// be added; doing so would defeat the entire purpose of Part 11
// (a tamper-evident record of who signed what, and when).
public class ElectronicSignature
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }

    // Printed name/username/role captured AT THE MOMENT OF SIGNING
    // (11.50(a)) - must keep displaying correctly even if the user is
    // later renamed, reassigned a different role, or deactivated.
    public string UserFullNameSnapshot { get; set; } = string.Empty;
    public string UsernameSnapshot { get; set; } = string.Empty;
    public string RoleSnapshot { get; set; } = string.Empty;

    public SignatureMeaning MeaningOfSignature { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public DateTime SignedAt { get; set; } = DateTime.UtcNow;
    public string? Comment { get; set; }
    public string? IpAddress { get; set; }
}
