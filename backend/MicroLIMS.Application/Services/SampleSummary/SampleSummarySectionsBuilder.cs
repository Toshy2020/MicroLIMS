using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

public static class SampleSummarySectionsBuilder
{
    public static List<SampleSectionSummaryDto> Build(
        Sample sample, IReadOnlyCollection<int>? sectionIds, Dictionary<int, SampleSectionRow> sectionRows, Func<int, string> NameOf)
    {
        var sampleSectionIds = SampleSectionRollup.SectionIds(sample);
        return sampleSectionIds.Select(id =>
        {
            var signoff = sample.SectionSignoffs.FirstOrDefault(r => r.SectionId == id);
            var canView = sectionIds is null || sectionIds.Contains(id);
            sectionRows.TryGetValue(id, out var row);
            return new SampleSectionSummaryDto
            {
                SectionId = id,
                SectionCode = row?.Code ?? string.Empty,
                SectionName = row?.Name ?? string.Empty,
                // A section closed without its own decision (another lab
                // rejected the sample) reads as "Closed" rather than the
                // raw Cancelled enum name.
                Status = SampleSectionRollup.StatusOf(sample, id) == SectionSignoffStatus.Cancelled
                    ? "Closed" : SampleSectionRollup.StatusOf(sample, id).ToString(),
                CanView = canView,
                CoaAvailable = SampleSectionRollup.StatusOf(sample, id) == SectionSignoffStatus.Approved,
                // Another section's reviewer/approver/remarks stay with that section.
                ReviewedByName = canView && signoff?.ReviewedByUserId is int reviewer ? NameOf(reviewer) : null,
                ReviewedAt = canView ? signoff?.ReviewedAt : null,
                ApprovedByName = canView && signoff?.ApprovedByUserId is int approver ? NameOf(approver) : null,
                ApprovedAt = canView ? signoff?.ApprovedAt : null,
                ApprovalDecision = canView ? signoff?.ApprovalDecision?.ToString() : null,
                CertificateRemarks = canView ? signoff?.CertificateRemarks : null,
                ClosedByName = canView && signoff?.ClosedByUserId is int closer ? NameOf(closer) : null,
                ClosedAt = canView ? signoff?.ClosedAt : null,
                CloseReason = canView ? signoff?.CloseReason : null
            };
        }).ToList();
    }
}
