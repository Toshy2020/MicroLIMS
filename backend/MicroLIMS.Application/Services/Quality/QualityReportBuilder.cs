using System.Text.Json;
using MicroLIMS.Application.Abstractions.Pdf;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Quality;

public sealed class QualityReportBuilder(
    QualityQueryService queries,
    QualityLinkService links,
    IPdfGenerator pdfGenerator,
    DataExportAuditService exportAudit,
    IMicroLimsDbContext db)
{
    public async Task<(string FileName, byte[] Bytes)> GeneratePdfAsync(int recordId, int userId,
        CancellationToken ct = default)
    {
        var detail = await queries.GetDetailAsync(recordId, userId, ct);
        var resolvedLinks = await links.ResolveAsync(recordId, userId, 0, 100, ct);
        var document = Build(detail, resolvedLinks);
        var bytes = await pdfGenerator.GenerateReportAsync(document);

        var actor = db.CurrentUserId ?? userId;
        var filterJson = JsonSerializer.Serialize(new { recordId, number = detail.Number });
        await exportAudit.LogExportAsync(actor, filterJson, 1, "QualityRecordPdf");

        return ($"{detail.Number}.pdf", bytes);
    }

    public static ReportDocument Build(QualityRecordDetailDto d, IReadOnlyList<QualityLinkDto> resolvedLinks)
    {
        var tone = d.IsCancelled ? ReportTone.Danger
            : d.Status is "Closed" or "Approved" or "ApprovedForImplementation" ? ReportTone.Positive
            : d.Status is "Rejected" ? ReportTone.Danger
            : ReportTone.Warning;

        var doc = new ReportDocument
        {
            Kind = $"{ReportVocabulary.Humanize(d.Type.ToString())} report",
            DocumentId = d.Number,
            Subtitle = d.Title,
            BadgeText = ReportVocabulary.Humanize(d.Status),
            BadgeTone = tone,
            HeaderNote = $"Raised {QualityQueryService.FormatUtcWithZone(d.RaisedAtUtc)}"
        };

        var left = new List<(string, string)>
        {
            ("Number", d.Number),
            ("Type", ReportVocabulary.Humanize(d.Type.ToString())),
            ("Status", ReportVocabulary.Humanize(d.Status)),
            ("Laboratory", d.SectionName ?? "Site-wide"),
            ("Classification", d.ClassificationName ?? "-"),
            ("Raised by", d.RaisedByName ?? "System"),
            ("Raised at", QualityQueryService.FormatUtcWithZone(d.RaisedAtUtc)),
            ("Owner", d.OwnerName ?? "-"),
            ("Due date", d.DueDate?.ToString("yyyy-MM-dd") ?? "-")
        };

        var right = new List<(string, string)>
        {
            ("Description", d.Description)
        };
        if (d.Deviation is { } dev)
        {
            right.Add(("Occurred at", QualityQueryService.FormatUtcWithZone(dev.OccurredAtUtc)));
            if (dev.Location is not null) right.Add(("Location", dev.Location));
            right.Add(("Immediate action", dev.ImmediateAction));
            if (dev.RootCauseCategoryName is not null) right.Add(("Root cause", dev.RootCauseCategoryName));
            if (dev.RootCauseDescription is not null) right.Add(("Root cause details", dev.RootCauseDescription));
            if (dev.ImpactAssessment is not null) right.Add(("Impact assessment", dev.ImpactAssessment));
            if (dev.ProposedActions is not null) right.Add(("Proposed actions", dev.ProposedActions));
            if (dev.RejectionReason is not null) right.Add(("Rejection reason", dev.RejectionReason));
        }
        else if (d.Capa is { } capa)
        {
            if (capa.SourceRecordNumber is not null) right.Add(("Source record", capa.SourceRecordNumber));
            if (capa.EffectivenessCriteria is not null) right.Add(("Effectiveness criteria", capa.EffectivenessCriteria));
            if (capa.EffectivenessMethod is not null) right.Add(("Effectiveness method", capa.EffectivenessMethod));
            if (capa.EffectivenessPlannedDate is not null) right.Add(("Planned check", capa.EffectivenessPlannedDate.Value.ToString("yyyy-MM-dd")));
            if (capa.EffectivenessResult is not null) right.Add(("Effectiveness result", ReportVocabulary.Humanize(capa.EffectivenessResult.ToString())));
            if (capa.EffectivenessFindings is not null) right.Add(("Effectiveness findings", capa.EffectivenessFindings));
        }
        else if (d.ChangeControl is { } cc)
        {
            right.Add(("Reason", cc.Reason));
            if (cc.ImpactSummary is not null) right.Add(("Impact summary", cc.ImpactSummary));
            if (cc.PostChangeReview is not null) right.Add(("Post-change review", cc.PostChangeReview));
            if (cc.UnchangedJustification is not null) right.Add(("Unchanged justification", cc.UnchangedJustification));
            if (cc.RejectionReason is not null) right.Add(("Rejection reason", cc.RejectionReason));
        }
        else if (d.Complaint is { } cmp)
        {
            right.Add(("Source", cmp.Source));
            right.Add(("Received on", cmp.ReceivedOn.ToString("yyyy-MM-dd")));
            if (cmp.ExternalReference is not null) right.Add(("External ref", cmp.ExternalReference));
            if (cmp.ItemName is not null) right.Add(("Product", cmp.ItemName));
            if (cmp.LotText is not null) right.Add(("Lot", cmp.LotText));
            if (cmp.WhatToTest is not null) right.Add(("What to test", cmp.WhatToTest));
            if (cmp.Findings is not null) right.Add(("Findings", cmp.Findings));
            if (cmp.Outcome is not null) right.Add(("Outcome", ReportVocabulary.Humanize(cmp.Outcome.ToString())));
            if (cmp.ConclusionSummary is not null) right.Add(("Conclusion", cmp.ConclusionSummary));
        }
        if (d.CancelReason is not null)
            right.Add(("Cancel reason", d.CancelReason));

        doc.Blocks.Add(new TwoColumnBlock
        {
            LeftLabel = "record identity",
            Left = left,
            RightLabel = "details",
            Right = right
        });

        if (d.Actions.Count > 0)
        {
            doc.Blocks.Add(new HeadingBlock { Text = "actions" });
            doc.Blocks.Add(new ListBlock
            {
                Label = "actions",
                Rows = d.Actions.Select(a => (
                    $"{ReportVocabulary.Humanize(a.Kind.ToString())} · {a.ResponsibleName} (due {a.DueDate:yyyy-MM-dd})",
                    a.Description + (a.DoneAtUtc is not null
                        ? $"  |  Completed {QualityQueryService.FormatUtcWithZone(a.DoneAtUtc.Value)} by {a.DoneByName}"
                        : "  |  Open")
                )).ToList()
            });
        }

        if (resolvedLinks.Count > 0)
        {
            doc.Blocks.Add(new HeadingBlock { Text = "linked records" });
            doc.Blocks.Add(new ListBlock
            {
                Label = "linked records",
                Rows = resolvedLinks.Select(l => (
                    l.Number ?? "-",
                    l.IsRestricted ? "Restricted" : (l.Label ?? "-")
                )).ToList()
            });
        }

        doc.Blocks.Add(new HeadingBlock { Text = "electronic signatures" });
        doc.Blocks.Add(new SignatureBlock
        {
            Signatures = d.Timeline.Select(t => new SignatureEntry
            {
                PrintedName = t.SignerFullName,
                Username = t.Username,
                Role = ReportVocabulary.Humanize(t.Role),
                SignedAt = t.SignedAtUtcText,
                Meaning = t.MeaningText,
                Comment = t.Comment
            }).ToList()
        });

        return doc;
    }
}
