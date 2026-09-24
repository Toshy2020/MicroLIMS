namespace MicroLIMS.Application.DTOs;

// Query filters for the cross-laboratory tracking board (GET api/samples/tracking).
public class SampleTrackingFilterDto
{
    public int? LabSectionId { get; set; }

    // OverallSampleStatus name: InProgress | Approved | Rejected |
    // RetestRequested | Voided | Cancelled.
    public string? Overall { get; set; }
    public string? From { get; set; }
    public string? To { get; set; }
    public int? ItemId { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

// One laboratory section's stage on a tracked sample. A lab not on the
// sample is simply absent from SampleTrackingRowDto.Labs - the UI renders
// "not requested" for it rather than this DTO carrying a null/None state.
// Stage: Testing | UnderReview | UnderApproval | Approved | Rejected |
// RetestRequested | Closed | Voided.
public record SampleTrackingLabDto(int SectionId, string SectionName, string Stage);

public record SampleTrackingRowDto(
    int SampleId, string ReferenceNumber, string Product, string? BatchNumber,
    DateTime ReceivedAt, string OverallStatus, List<SampleTrackingLabDto> Labs);
