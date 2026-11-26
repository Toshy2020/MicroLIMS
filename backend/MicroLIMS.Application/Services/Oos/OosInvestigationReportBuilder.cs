using System.Text.Json;
using MicroLIMS.Application.Abstractions.Pdf;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Oos;

public sealed class OosInvestigationReportBuilder(
    OosInvestigationService investigations,
    IPdfGenerator pdfGenerator,
    DataExportAuditService exportAudit,
    IMicroLimsDbContext db)
{
    public async Task<(string FileName, byte[] Bytes)> GeneratePdfAsync(int investigationId, CancellationToken ct = default)
    {
        var detail = await investigations.GetByIdAsync(investigationId, ct);
        var document = Build(detail);
        var bytes = await pdfGenerator.GenerateReportAsync(document);

        var actor = db.CurrentUserId ?? 0;
        var filterJson = JsonSerializer.Serialize(new { investigationId, number = detail.Number });
        await exportAudit.LogExportAsync(actor, filterJson, 1, "OosInvestigationPdf");

        return ($"{detail.Number}.pdf", bytes);
    }

    public static ReportDocument Build(OosInvestigationDetailDto d)
    {
        var isExcursion = d.Kind == OosInvestigationKind.Excursion;
        var tone = d.Status != OosInvestigationStatus.Concluded ? ReportTone.Warning
            : d.Conclusion == OosConclusion.Confirmed ? ReportTone.Danger
            : ReportTone.Positive;

        var doc = new ReportDocument
        {
            Kind = isExcursion ? "Excursion review report" : "OOS investigation report",
            DocumentId = d.Number,
            Subtitle = $"{ReportVocabulary.Humanize(d.SampleCategory)} · {d.SampleReferenceNumber}" +
                       (string.IsNullOrWhiteSpace(d.SampleDisplayName) ? "" : $" · {d.SampleDisplayName}"),
            SubtitleEmphasis = string.IsNullOrWhiteSpace(d.SampleDisplayName) ? null : d.SampleDisplayName,
            BadgeText = d.Conclusion is OosConclusion c
                ? ReportVocabulary.Humanize(c.ToString())
                : ReportVocabulary.Humanize(d.Status.ToString()),
            BadgeTone = tone,
            HeaderNote = $"Opened {OosInvestigationService.FormatUtcWithZone(d.OpenedAtUtc)}"
        };

        var left = new List<(string, string)>
        {
            ("Number", d.Number),
            ("Kind", ReportVocabulary.Humanize(d.Kind.ToString())),
            ("Status", ReportVocabulary.Humanize(d.Status.ToString())),
            ("Laboratory", d.SectionName),
            ("Sample", d.SampleReferenceNumber),
            ("Batch", d.BatchNumber ?? "-"),
            ("Opened by", d.OpenedByName),
            ("Opened at", OosInvestigationService.FormatUtcWithZone(d.OpenedAtUtc))
        };
        if (d.PhaseOneDueDate is not null) left.Add(("Phase I due", d.PhaseOneDueDate.Value.ToString("yyyy-MM-dd")));
        if (d.DueDate is not null) left.Add(("Overall due", d.DueDate.Value.ToString("yyyy-MM-dd")));

        var right = new List<(string, string)>();
        if (d.PhaseOneAssessment is not null)
        {
            right.Add(("Phase I assessment", ReportVocabulary.Humanize(d.PhaseOneAssessment.ToString())));
            if (d.PhaseOneCauseCategoryName is not null) right.Add(("Phase I cause", d.PhaseOneCauseCategoryName));
            if (d.PhaseOneSignedByName is not null) right.Add(("Phase I signed by", d.PhaseOneSignedByName));
            if (d.PhaseOneSignedAtUtc is not null) right.Add(("Phase I signed at", OosInvestigationService.FormatUtcWithZone(d.PhaseOneSignedAtUtc.Value)));
        }
        if (d.Conclusion is not null)
        {
            right.Add(("Conclusion", ReportVocabulary.Humanize(d.Conclusion.ToString())));
            if (d.RootCauseCategoryName is not null) right.Add(("Root cause", d.RootCauseCategoryName));
            if (d.RootCauseDescription is not null) right.Add(("Cause details", d.RootCauseDescription));
            if (d.ConclusionSummary is not null) right.Add(("Summary", d.ConclusionSummary));
            if (d.ConcludedByName is not null) right.Add(("Concluded by", d.ConcludedByName));
            if (d.ConcludedAtUtc is not null) right.Add(("Concluded at", OosInvestigationService.FormatUtcWithZone(d.ConcludedAtUtc.Value)));
        }
        if (right.Count == 0)
            right.Add(("Assessment", "Pending"));

        doc.Blocks.Add(new TwoColumnBlock
        {
            LeftLabel = "investigation identity",
            Left = left,
            RightLabel = "assessment & conclusion",
            Right = right
        });

        doc.Blocks.Add(new HeadingBlock { Text = "affected tests" });
        doc.Blocks.Add(new ListBlock
        {
            Label = "affected tests",
            EmptyText = "No test orders linked.",
            Rows = d.Results.Select(r => (
                $"{r.TestCode} - {r.TestDisplayName}",
                $"{ReportVocabulary.Humanize(r.OutcomeAtOpening.ToString())}: {r.ResultSummaryAtOpening}" +
                (r.IsInvalidated ? " [INVALIDATED]" : "")
            )).ToList()
        });

        doc.Blocks.Add(new HeadingBlock { Text = "investigation entries" });
        doc.Blocks.Add(new ListBlock
        {
            Label = "investigation entries",
            EmptyText = "No entries recorded.",
            Rows = d.Entries.Select(e => (
                $"Phase {e.Phase} · {ReportVocabulary.Humanize(e.Kind.ToString())}" +
                (e.ChecklistItemKey is null ? "" : $" ({e.ChecklistItemKey})") +
                (e.Answer is null ? "" : $": {e.Answer}"),
                $"{e.Text}  |  {e.RecordedByName}  |  {OosInvestigationService.FormatUtcWithZone(e.RecordedAtUtc)}"
            )).ToList()
        });

        if (d.RetestPlans.Count > 0)
        {
            doc.Blocks.Add(new HeadingBlock { Text = "retest plans" });
            doc.Blocks.Add(new ListBlock
            {
                Label = "retest plans",
                Rows = d.RetestPlans.Select(p => (
                    $"Revision {p.Revision} · {ReportVocabulary.Humanize(p.Source.ToString())} ({p.RetestCount})",
                    $"Rule: {p.JudgingRule}" +
                    (p.SignedByName is null ? " (Draft)" : $"  |  Signed by {p.SignedByName} at {OosInvestigationService.FormatUtcWithZone(p.SignedAtUtc!.Value)}") +
                    (p.Samples.Count == 0 ? "" : $"  |  Samples: {string.Join(", ", p.Samples.Select(FormatPlanSample))}")
                )).ToList()
            });
        }

        if (d.DeviationRequests.Count > 0)
        {
            doc.Blocks.Add(new HeadingBlock { Text = "qa deviation requests" });
            doc.Blocks.Add(new ListBlock
            {
                Label = "qa deviation requests",
                Rows = d.DeviationRequests.Select(q => (
                    $"{ReportVocabulary.Humanize(q.Status.ToString())} · {q.RequestedByName}",
                    $"{q.Reason}  |  {OosInvestigationService.FormatUtcWithZone(q.RequestedAtUtc)}" +
                    (string.IsNullOrWhiteSpace(q.AddressedComment) ? "" : $"  |  Addressed: {q.AddressedComment}")
                )).ToList()
            });
        }

        if (d.Reopenings.Count > 0)
        {
            doc.Blocks.Add(new HeadingBlock { Text = "reopening history" });
            doc.Blocks.Add(new ListBlock
            {
                Label = "reopening history",
                Rows = d.Reopenings.Select(r => (
                    $"Reopened by {r.ReopenedByName}",
                    $"{OosInvestigationService.FormatUtcWithZone(r.ReopenedAtUtc)}  |  Reason: {r.Reason}  |  Previous: {ReportVocabulary.Humanize(r.PreviousConclusion.ToString())}"
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

    private static string FormatPlanSample(OosRetestPlanSampleDto sample)
    {
        var marker = sample.IsReportable ? " [Reportable]" : "";
        var results = sample.ResultSummaries is { Count: > 0 }
            ? $" ({string.Join("; ", sample.ResultSummaries)})"
            : "";
        return $"{sample.ReferenceNumber}{marker}{results}";
    }
}
