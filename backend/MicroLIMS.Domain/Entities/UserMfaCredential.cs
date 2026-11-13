using MicroLIMS.Domain.Attributes;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class UserMfaCredential
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public MfaCredentialType Type { get; set; }
    [AuditRedacted]
    public string SecretProtected { get; set; } = string.Empty;
    public int StepSeconds { get; set; }
    public string? SerialNumber { get; set; }
    public MfaCredentialStatus Status { get; set; }
    public long? LastAcceptedStep { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public int? AssignedByUserId { get; set; }
    public User? AssignedByUser { get; set; }
    public DateTime? RevokedAt { get; set; }
    public int? RevokedByUserId { get; set; }
    public User? RevokedByUser { get; set; }
    public string? RevokeReason { get; set; }
}
