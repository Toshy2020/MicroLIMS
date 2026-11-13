using System.Globalization;
using MicroLIMS.Application.Abstractions.Pdf;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

// Maps an AuditTrailReviewDto into the renderer-agnostic ReportDocument model
// for PDF generation. Entries themselves are never listed (only the count and
// cut-off boundary); findings, outcome/conclusion, and 21 CFR Part 11 §11.50
// signature manifestation (or an explicit unsigned notice) are rendered.
public static class AuditTrailReviewReportBuilder
{
    public static string FormatUtc(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
        return utc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
    }

    public static ReportDocument Build(AuditTrailReviewDto review)
    {
        var isSigned = review.Status == AuditTrailReviewStatus.Signed;
        var tone = !isSigned
            ? ReportTone.Neutral
            : review.Outcome == AuditTrailReviewOutcome.FindingsRecorded
                ? ReportTone.Warning
                : ReportTone.Positive;

        var doc = new ReportDocument
        {
            Kind = "Audit trail review",
            DocumentId = $"ATR-{review.Id}",
            Subtitle = review.Title,
            BadgeText = isSigned ? "SIGNED" : "IN PROGRESS",
            BadgeTone = tone,
            HeaderNote = $"Started {FormatUtc(review.StartedAtUtc)}"
        };

        var scopeLabel = ScopeKindLabel(review.ScopeKind);
        var newerNote = review.NewerEntriesCount == 0
            ? $"0 newer entries exist in scope after cut-off audit ID {review.CutoffAuditLogId}."
            : $"{review.NewerEntriesCount} newer {(review.NewerEntriesCount == 1 ? "entry exists" : "entries exist")} in scope after cut-off audit ID {review.CutoffAuditLogId} (excluded from this review).";

        var openCount = review.Findings.Count(f => f.Status == AuditFindingStatus.Open);

        doc.Blocks.Add(new CardBlock
        {
            Title = review.Title,
            Subtitle = $"Scope: {scopeLabel}",
            HeadlineValue = review.EntryCount.ToString(CultureInfo.InvariantCulture),
            HeadlineUnit = "entries in scope",
            Tone = tone,
            Details = new List<(string, string)>
            {
                ("Scope kind", scopeLabel),
                ("Cut-off audit ID", review.CutoffAuditLogId.ToString(CultureInfo.InvariantCulture)),
                ("Entry count", review.EntryCount.ToString(CultureInfo.InvariantCulture))
            },
            Stats = new List<(string, string)>
            {
                ("Entries in scope", review.EntryCount.ToString(CultureInfo.InvariantCulture)),
                ("Newer entries", review.NewerEntriesCount.ToString(CultureInfo.InvariantCulture)),
                ("Findings", review.Findings.Count.ToString(CultureInfo.InvariantCulture)),
                ("Open findings", openCount.ToString(CultureInfo.InvariantCulture))
            },
            MetaLines = new List<string> { newerNote },
            Rows = new List<(string, string)>
            {
                ("Period from (UTC)", FormatUtc(review.PeriodFromUtc)),
                ("Period to (UTC)", FormatUtc(review.PeriodToUtc)),
                ("Scope entity name", string.IsNullOrWhiteSpace(review.ScopeEntityName) ? "-" : review.ScopeEntityName),
                ("Scope record ID", string.IsNullOrWhiteSpace(review.ScopeRecordId) ? "-" : review.ScopeRecordId),
                ("Started by", string.IsNullOrWhiteSpace(review.StartedByName) ? $"User #{review.StartedByUserId}" : review.StartedByName),
                ("Started at (UTC)", FormatUtc(review.StartedAtUtc))
            }
        });

        doc.Blocks.Add(new HeadingBlock { Text = "Findings" });
        if (review.Findings.Count == 0)
        {
            doc.Blocks.Add(new ListBlock
            {
                Label = "Findings",
                Rows = new List<(string, string)>(),
                EmptyText = "No findings recorded."
            });
        }
        else
        {
            doc.Blocks.Add(new TableBlock
            {
                Label = "Findings",
                Summary = $"{review.Findings.Count} finding(s) recorded ({openCount} open).",
                Table = new ReportTable
                {
                    Columns = new List<TableColumn>
                    {
                        new("Audit entry ID", 0.12),
                        new("Note", 0.34),
                        new("Status", 0.10),
                        new("Closed by / at", 0.22),
                        new("Closing comment", 0.22)
                    },
                    Rows = review.Findings.Select(f =>
                    {
                        var closedByAt = f.Status == AuditFindingStatus.Closed
                            ? $"{(string.IsNullOrWhiteSpace(f.ClosedByName) ? $"User #{f.ClosedByUserId}" : f.ClosedByName)}\n{(f.ClosedAtUtc.HasValue ? FormatUtc(f.ClosedAtUtc.Value) : "-")}"
                            : "-";
                        return new List<string>
                        {
                            f.AuditLogId.ToString(CultureInfo.InvariantCulture),
                            f.Note,
                            f.Status == AuditFindingStatus.Closed ? "Closed" : "Open",
                            closedByAt,
                            string.IsNullOrWhiteSpace(f.ClosedComment) ? "-" : f.ClosedComment
                        };
                    }).ToList()
                }
            });
        }

        var outcomeText = !isSigned
            ? "Pending"
            : review.Outcome == AuditTrailReviewOutcome.FindingsRecorded
                ? "Findings recorded"
                : "No findings";

        doc.Blocks.Add(new HeadingBlock { Text = "Outcome & conclusion" });
        doc.Blocks.Add(new CardBlock
        {
            Title = "Review outcome",
            Subtitle = outcomeText,
            HeadlineValue = isSigned ? outcomeText : "In progress",
            Tone = tone,
            MetaLines = isSigned
                ? new List<string> { review.Conclusion ?? string.Empty }
                : new List<string> { "Review is in progress; conclusion not yet recorded." }
        });

        doc.Blocks.Add(new HeadingBlock { Text = "Electronic signature" });
        if (isSigned)
        {
            var meaning = string.IsNullOrWhiteSpace(review.SignatureMeaningText)
                ? ReportVocabulary.SignatureMeaning(nameof(SignatureMeaning.AuditTrailReviewed))
                : review.SignatureMeaningText;

            doc.Blocks.Add(new SignatureBlock
            {
                Signatures = new List<SignatureEntry>
                {
                    new()
                    {
                        PrintedName = review.SignerFullName ?? review.StartedByName ?? $"User #{review.StartedByUserId}",
                        Username = review.SignerUsername ?? string.Empty,
                        Role = string.Empty,
                        SignedAt = review.SignedAtUtc.HasValue ? FormatUtc(review.SignedAtUtc.Value) : string.Empty,
                        Meaning = meaning
                    }
                }
            });
        }
        else
        {
            doc.Blocks.Add(new CardBlock
            {
                Title = "Not signed - review in progress",
                Subtitle = "This audit trail review has not been electronically signed.",
                Tone = ReportTone.Warning,
                MetaLines = new List<string>
                {
                    "Not signed - review in progress."
                }
            });
        }

        return doc;
    }

    private static string ScopeKindLabel(AuditReviewScopeKind kind) => kind switch
    {
        AuditReviewScopeKind.All => "All",
        AuditReviewScopeKind.EntityName => "Entity name",
        AuditReviewScopeKind.Record => "Record",
        _ => ReportVocabulary.Humanize(kind.ToString())
    };
}
