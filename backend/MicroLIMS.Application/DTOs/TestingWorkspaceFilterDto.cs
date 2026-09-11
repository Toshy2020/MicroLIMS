namespace MicroLIMS.Application.DTOs;

/// <summary>
/// Query string filter parameters for the Receiving &amp; Testing Workspace.
/// Supports free-text search, category, sample status, test status, analyst id,
/// urgency (overdue), date range, workload tile filter, and server-side paging.
/// </summary>
public class TestingWorkspaceFilterDto
{
    public string? Search { get; set; }
    public string? Category { get; set; }
    public string? Status { get; set; }
    public string? SampleStatus { get; set; }
    public string? TestStatus { get; set; }
    public int? AnalystId { get; set; }
    public string? Urgency { get; set; }
    public string? FromDate { get; set; }
    public string? ToDate { get; set; }
    public string? WorkloadFilter { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;

    /// <summary>
    /// Returns SampleStatus if explicitly populated, falling back to Status (e.g. from deep link ?status=...).
    /// </summary>
    public string? ResolvedSampleStatus =>
        !string.IsNullOrWhiteSpace(SampleStatus) ? SampleStatus : Status;
}
