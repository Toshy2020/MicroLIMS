namespace MicroLIMS.Application.DTOs;

// Group DTO representing one entire OOS investigation chain (grouped by OosGroupCode).
public class OosGroupDto
{
    public string OosGroupCode { get; set; } = string.Empty;
    public int OriginSampleId { get; set; }
    public string OriginReferenceNumber { get; set; } = string.Empty;
    public string OriginSampleStatus { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? BatchNumber { get; set; }
    public DateTime OpenedAt { get; set; }
    public bool HasInvestigationDocument { get; set; }
    public List<OosTrackingEntryDto> RetestSamples { get; set; } = new();
}

// One entry per retest spin-off sample within the OOS group.
public class OosTrackingEntryDto
{
    public int NewSampleId { get; set; }
    public string NewReferenceNumber { get; set; } = string.Empty;
    public string NewSampleStatus { get; set; } = string.Empty;
    public int OriginSampleId { get; set; }
    public string OriginReferenceNumber { get; set; } = string.Empty;
    public string OriginSampleStatus { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? BatchNumber { get; set; }
    // "RetestRetainedSample" or "NewSampleRequest" - the immediate origin's ApprovalDecision.
    public string RetestType { get; set; } = string.Empty;
    public List<string> TestCodes { get; set; } = new();
    public List<string> AnalystNames { get; set; } = new();
    public DateTime OpenedAt { get; set; }
}
