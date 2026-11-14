using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Section-scoped escalation rule evaluated in laboratory working hours.
// Never edited in place: changes retire the previous rule and create a replacement
// linked via ReplacesRuleId so firing history stays attributable.
public class EscalationRule
{
    public int Id { get; set; }
    public int SectionId { get; set; }
    public DocumentSection? Section { get; set; }
    public EscalationTrigger Trigger { get; set; }
    public decimal ThresholdWorkingHours { get; set; }
    public RoleType NotifyRoleType { get; set; }
    public bool IsActive { get; set; } = true;
    public int CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RetiredAt { get; set; }
    public int? RetiredByUserId { get; set; }
    public User? RetiredByUser { get; set; }
    public int? ReplacesRuleId { get; set; }
    public EscalationRule? ReplacesRule { get; set; }
}
