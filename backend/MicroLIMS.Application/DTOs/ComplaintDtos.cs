using MicroLIMS.Domain.Entities.Quality;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs;

public sealed record ReceiveComplaintRequest(string Title, string Source, DateOnly ReceivedOn,
    string? ExternalReference, int? ItemId, string? LotText, string ComplaintText, int? SectionId);

public sealed record RequestComplaintInvestigationRequest(int ClassificationId, int OwnerUserId,
    DateOnly? DueDate, string WhatToTest);

public sealed record SaveComplaintInvestigationRequest(string Findings, IReadOnlyList<int>? SampleIds = null);

public sealed record ConcludeComplaintRequest(ComplaintOutcome Outcome, string Summary, string Password,
    int? FollowUpRecordId = null);

public sealed record ComplaintPasswordRequest(string Password);

public sealed record ComplaintReasonRequest(string Reason, string Password);

public sealed record ComplaintDto(QualityRecordDto Record, ComplaintStatus Status, string Source, DateOnly ReceivedOn,
    string? ExternalReference, int? ItemId, string? LotText, string ComplaintText, int? ClassificationId,
    DateOnly? DueDate, string? WhatToTest, string? Findings, ComplaintOutcome? Outcome, string? ConclusionSummary,
    string? CancelReason)
{
    public int Id => Record.Id;
    public uint Version => Record.Version;

    internal static ComplaintDto From(QualityRecord record, ComplaintDetail detail) =>
        new(QualityRecordDto.From(record), detail.Status, detail.Source, detail.ReceivedOn,
            detail.ExternalReference, detail.ItemId, detail.LotText, detail.ComplaintText,
            record.ClassificationId, record.DueDate, detail.WhatToTest, detail.Findings,
            detail.Outcome, detail.ConclusionSummary, record.CancelReason);
}
