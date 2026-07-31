namespace MicroLIMS.Application.DTOs;

public class TestOrderSummaryDto
{
    public int TestOrderId { get; set; }
    public string TestCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

// Object transferred between API and Frontend - never exposes internal
// domain/persistence details (Frozen Principle #3).
public class SampleDto
{
    public int SampleId { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty; // Item name, or Sampling Point/Department/Machine name
    public int? DepartmentId { get; set; }
    public int? MachineId { get; set; }
    public string? ProductionStage { get; set; }
    public string CauseOfTesting { get; set; } = string.Empty;
    public string? BatchNumber { get; set; }
    public string ControlNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PreparationStatus { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public List<TestOrderSummaryDto> AssignedTests { get; set; } = new();
}
