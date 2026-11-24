namespace MicroLIMS.Domain.Entities;

public enum CompetencyOverrideEndReason { Reviewed, Cancelled, Superseded, Reassigned }

public sealed class CompetencyOverride
{
    public int Id { get; set; }
    public int TestOrderId { get; set; }
    public TestOrder TestOrder { get; set; } = null!;
    public int SampleId { get; set; }
    public int AnalystUserId { get; set; }
    public User AnalystUser { get; set; } = null!;
    public string Reason { get; set; } = string.Empty;
    public int GrantedByUserId { get; set; }
    public DateTime GrantedAtUtc { get; set; }
    public int SignatureId { get; set; }
    public ElectronicSignature Signature { get; set; } = null!;
    public DateTime? EndedAtUtc { get; set; }
    public CompetencyOverrideEndReason? EndReason { get; set; }
}
