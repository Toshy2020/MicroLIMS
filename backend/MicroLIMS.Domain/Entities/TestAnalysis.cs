using System.ComponentModel.DataAnnotations.Schema;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class TestAnalysis
{
    public int Id { get; set; }

    public int TestOrderId { get; set; }
    public TestOrder? TestOrder { get; set; }

    public WorkflowType AnalysisType { get; set; }

    public int? EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    public DateTime AnalysedAt { get; set; }

    public decimal? UnitAmount { get; set; }
    public SampleMatrix? SampleMatrix { get; set; }
    public string? ConditionsJson { get; set; }
    public string? ValidityRecordType { get; set; }
    public int? ValidityRecordId { get; set; }

    public bool IsActive { get; set; } = true;

    public int EnteredByUserId { get; set; }
    public User? EnteredByUser { get; set; }

    public DateTime EnteredAt { get; set; } = DateTime.UtcNow;

    public int SignatureId { get; set; }
    public ElectronicSignature? Signature { get; set; }

    public string? Comment { get; set; }

    public List<ParameterResult> ParameterResults { get; set; } = new();

    [NotMapped]
    public List<ParameterResult> Results => ParameterResults;
}
