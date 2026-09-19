using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Elemental Assay Entry (REQ-FP-022, Slice S3a)
// Represents one signed elemental assay entry per test order, encompassing all elements analyzed together.
public class ElementalAssayEntry
{
    public int Id { get; set; }

    public int TestOrderId { get; set; }
    public TestOrder? TestOrder { get; set; }

    public SampleMatrix SampleMatrix { get; set; }
    public decimal UnitAmount { get; set; } // average unit weight Wu (g) for Solid, dose volume Vd (mL) for Liquid
    public DateTime AnalysedAt { get; set; } // UTC analysis time from report

    public bool IsActive { get; set; } = true;

    public int EnteredByUserId { get; set; }
    public User? EnteredByUser { get; set; }

    public DateTime EnteredAt { get; set; } = DateTime.UtcNow;

    public int SignatureId { get; set; }
    public ElectronicSignature? Signature { get; set; }

    public string? Comment { get; set; }

    public List<ElementalAssayResult> Results { get; set; } = new();
}
