namespace MicroLIMS.Application.DTOs;

/// <summary>
/// Aggregate counts for the 6 Receiving &amp; Testing workspace workload tiles,
/// corresponding to WorkloadFilterKey in the frontend KPI cards.
/// </summary>
public class WorkspaceTileCountsDto
{
    public int NeedsPreparation { get; set; }
    public int ReadyToRead { get; set; }
    public int AwaitingReview { get; set; }
    public int Overdue { get; set; }
    public int Mine { get; set; }
    public int Unassigned { get; set; }
}
