using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class NotApplicableTest
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public Item? Item { get; set; }

    // null means every grade
    public string? Grade { get; set; }
    public string TestCode { get; set; } = string.Empty;

    public string Justification { get; set; } = string.Empty;
    public string Criticality { get; set; } = string.Empty;
    public string ImmediateAction { get; set; } = string.Empty;
    public string LongTermAction { get; set; } = string.Empty;

    // e.g. "QC form NA-01"
    public string? Reference { get; set; }

    public NotApplicableStatus Status { get; set; } = NotApplicableStatus.Draft;

    public int? HeadSignatureId { get; set; }
    public ElectronicSignature? HeadSignature { get; set; }

    public int? FinalSignatureId { get; set; }
    public ElectronicSignature? FinalSignature { get; set; }

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
