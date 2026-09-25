namespace MicroLIMS.Domain.Entities;

public class CalibrationRun
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty; // e.g. "ICP-MIN CAL 01/092026"

    public int TestDefinitionId { get; set; }
    public TestDefinition? TestDefinition { get; set; }

    public int SectionId { get; set; }
    public DocumentSection? Section { get; set; }

    public int EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    public int CalibrationStandardMaterialId { get; set; }
    public Material? CalibrationStandardMaterial { get; set; }

    public int? IcvStandardMaterialId { get; set; }
    public Material? IcvStandardMaterial { get; set; }

    public DateTime CalibrationAt { get; set; }

    public int PerformedByUserId { get; set; }
    public User? PerformedByUser { get; set; }

    public DateTime PerformedAt { get; set; }

    public int SignatureId { get; set; }
    public ElectronicSignature? Signature { get; set; }

    public string? Comment { get; set; }

    public int AnalytesPassed { get; set; }
    public int AnalytesTotal { get; set; }
    public bool Passed { get; set; }

    public MicroLIMS.Domain.Enums.CalibrationRunStatus Status { get; set; } = MicroLIMS.Domain.Enums.CalibrationRunStatus.Active;
    public DateTime? WithdrawnAt { get; set; }
    public int? WithdrawnByUserId { get; set; }
    public User? WithdrawnByUser { get; set; }
    public string? WithdrawalReason { get; set; }
    public int? WithdrawalSignatureId { get; set; }
    public ElectronicSignature? WithdrawalSignature { get; set; }

    public CalibrationRunDocument? Document { get; set; }
    public List<CalibrationRunAnalyte> Analytes { get; set; } = new();

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public List<AffectedApprovedOrder> AffectedApprovedOrders { get; set; } = new();
}

public record AffectedApprovedOrder(
    string SampleReference,
    string TestCode,
    string Element);

