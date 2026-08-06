using MicroLIMS.Application.DTOs;
using MicroLIMS.Infrastructure.Pdf;

namespace MicroLIMS.Application.Services;

// Maps each summary DTO onto the renderer-agnostic ReportDocument model.
// Kept in one place so the three archived documents share a vocabulary
// (same section order, same wording) rather than each inventing its own.
public static class ReportDocumentMapper
{
    private static string Dt(DateTime? v) => v is null ? "-" : v.Value.ToString("dd-MMM-yyyy HH:mm");
    private static string D(DateTime? v) => v is null ? "-" : v.Value.ToString("dd-MMM-yyyy");

    // Enum names arrive PascalCase; a controlled document should read as
    // English. Consecutive capitals are preserved so acronyms survive.
    private static string Humanize(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return "-";
        var spaced = System.Text.RegularExpressions.Regex.Replace(v, "([a-z0-9])([A-Z])", "$1 $2");
        spaced = System.Text.RegularExpressions.Regex.Replace(spaced, "([A-Z]+)([A-Z][a-z])", "$1 $2");
        return System.Text.RegularExpressions.Regex.Replace(spaced, @"\s+(.)", m => " " + char.ToLowerInvariant(m.Groups[1].Value[0]));
    }

    // 11.50(b) wants the meaning attested to, not the signature's type.
    private static readonly Dictionary<string, string> MeaningText = new()
    {
        ["Reviewed"] = "I have reviewed the test data and confirm it is complete and accurate.",
        ["Approved"] = "I approve the release of this record for its intended use.",
        ["Rejected"] = "I reject this record; it does not conform to specification.",
        ["RetestRequested"] = "I am ordering a retest of the retained sample.",
        ["InvestigationOrdered"] = "I am ordering an investigation into these results."
    };

    private static SignatureBlock ToSignatureBlock(List<SignatureDto> signatures) => new()
    {
        Signatures = signatures.Select(s => new SignatureEntry
        {
            PrintedName = s.PrintedName,
            Username = s.Username,
            Role = Humanize(s.Role),
            SignedAt = Dt(s.SignedAt),
            Meaning = MeaningText.TryGetValue(s.Meaning, out var m) ? m : Humanize(s.Meaning),
            Comment = s.Comment
        }).ToList()
    };

    private static ListBlock ToTimelineBlock(List<SampleWorkflowEventDto> timeline) => new()
    {
        Label = "lifecycle timeline",
        EmptyText = "No lifecycle events recorded.",
        Rows = timeline.Select(e => (
            Left: Humanize(e.EventType) + (e.Decision is null ? "" : $" - {Humanize(e.Decision)}"),
            Right: $"{e.PerformedByName}  |  {Dt(e.Timestamp)}" + (string.IsNullOrWhiteSpace(e.Comment) ? "" : $"  |  \"{e.Comment}\"")
        )).ToList()
    };

    public static ReportDocument ForSample(SampleSummaryDto s)
    {
        var tone = s.Status switch
        {
            "Approved" => ReportTone.Positive,
            "Rejected" => ReportTone.Danger,
            "UnderReview" or "UnderApproval" => ReportTone.Warning,
            _ => ReportTone.Neutral
        };

        var doc = new ReportDocument
        {
            Kind = "Sample summary report",
            DocumentId = s.ReferenceNumber,
            Subtitle = $"{Humanize(s.Category)} · {s.DisplayName}" + (s.BatchNumber is null ? "" : $" · Batch: {s.BatchNumber}"),
            SubtitleEmphasis = s.DisplayName,
            BadgeText = Humanize(s.Status),
            BadgeTone = tone,
            HeaderNote = $"Received {Dt(s.ReceivedAt)}"
        };

        var identity = new List<(string, string)>
        {
            ("Reference", s.ReferenceNumber),
            ("Category", Humanize(s.Category)),
            ("Item", s.DisplayName),
            ("Batch", s.BatchNumber ?? "-"),
            ("Control", s.ControlNumber),
            ("Cause", s.CauseOfTesting),
            ("Quantity", s.SampleQuantity ?? "-")
        };
        if (s.ProductionStage is not null) identity.Insert(3, ("Stage", s.ProductionStage));
        if (s.WaterSamplingPointCode is not null) identity.Add(("Point", $"{s.WaterSamplingPointCode} - {s.WaterSamplingPointLocation}"));

        var dates = new List<(string, string)>
        {
            ("Manufactured", D(s.MfgDate)),
            ("Expires", D(s.ExpDate)),
            ("Received by", s.ReceivedByName),
            ("Received at", Dt(s.ReceivedAt)),
            ("Sampled by", s.SampledBy)
        };
        if (s.ReviewedByName is not null) dates.Add(("Reviewed by", s.ReviewedByName));
        if (s.ApprovedByName is not null) dates.Add(("Approved by", s.ApprovedByName));
        if (s.ApprovalDecision is not null) dates.Add(("Decision", Humanize(s.ApprovalDecision)));

        doc.Blocks.Add(new TwoColumnBlock
        {
            LeftLabel = "sample identity", Left = identity,
            RightLabel = "dates & personnel", Right = dates
        });

        if (s.Preparation is not null)
        {
            var p = s.Preparation;
            doc.Blocks.Add(new StripBlock
            {
                Label = "sample preparation",
                Items = new()
                {
                    ("Amount", $"{p.Amount} {p.Unit}", null),
                    ("Technique", Humanize(p.Technique), p.Technique == "Filtration" ? $"Filt {p.FiltrationVolume} / Wash {p.WashingVolume}" : null),
                    ("Neutralizer", string.IsNullOrEmpty(p.NeutralizerName) ? "-" : p.NeutralizerName, null),
                    ("Prepared by", p.PreparedByName, Dt(p.PreparedAt))
                }
            });
        }

        doc.Blocks.Add(new HeadingBlock { Text = "test results" });
        foreach (var t in s.TestOrders) doc.Blocks.Add(ToTestCard(t));
        if (s.TestOrders.Count == 0)
            doc.Blocks.Add(new ListBlock { Label = "test results", Rows = new(), EmptyText = "No tests recorded." });

        doc.Blocks.Add(new HeadingBlock { Text = "lifecycle timeline" });
        doc.Blocks.Add(ToTimelineBlock(s.Timeline));

        if (s.Signatures.Count > 0)
        {
            doc.Blocks.Add(new HeadingBlock { Text = "electronic signatures" });
            doc.Blocks.Add(ToSignatureBlock(s.Signatures));
        }

        return doc;
    }

    private static CardBlock ToTestCard(TestOrderSummaryDetailDto t)
    {
        var reading = t.CountTestReadings.LastOrDefault();
        var incubation = t.Incubations.LastOrDefault();
        var lastResult = t.Results.LastOrDefault();
        var detected = t.PathogenObservations.Any(p => p.GrowthObserved);
        var outOfSpec = reading?.Status == "OutOfSpecification";
        var hasNonConformingLocation = t.Locations.Any(l => l.Status is not (null or "WithinLimits" or "Absent"));

        var tone = t.IsSuperseded ? ReportTone.Neutral
            : outOfSpec || detected || hasNonConformingLocation ? ReportTone.Danger
            : ReportTone.Positive;

        // The headline slot is a compact single-value stat (a CFU number,
        // "Detected"/"Absent") - the full "X locations: Y conform..."
        // sentence belongs in the table summary below, not here, or it
        // starves the title column of width and wraps it word-by-word.
        string? worstLocationStatus = null;
        if (t.Locations.Count > 0)
        {
            var severity = new[] { "WithinLimits", "Absent", "AlertLimitExceeded", "ActionLimitExceeded", "OutOfSpecification", "Detected" };
            worstLocationStatus = "WithinLimits";
            foreach (var loc in t.Locations)
            {
                if (loc.Status is null) continue;
                if (Array.IndexOf(severity, loc.Status) > Array.IndexOf(severity, worstLocationStatus))
                    worstLocationStatus = loc.Status;
            }
        }

        var headline = worstLocationStatus is not null ? Humanize(worstLocationStatus)
            : reading?.ReportedResult
            ?? (t.PathogenObservations.Count > 0 ? (detected ? "Detected" : "Absent") : null)
            ?? lastResult?.InterpretedValue ?? lastResult?.RawValue ?? "-";

        var card = new CardBlock
        {
            Title = $"{t.TestCode} - {t.TestDisplayName}",
            Subtitle = (t.IsSuperseded ? "SUPERSEDED BY RETEST · " : "")
                + $"Step: {Humanize(incubation?.StepName ?? t.CurrentStep)}"
                + (incubation?.MediaLotNumber is null ? "" : $" · Media lot: {incubation.MediaLotNumber}"),
            HeadlineValue = headline,
            HeadlineUnit = t.Locations.Count > 0 ? $"{t.Locations.Count} location{(t.Locations.Count == 1 ? "" : "s")}"
                : reading is not null ? $"CFU · {Humanize(reading.Status)}" : Humanize(t.Status),
            Tone = tone
        };

        if (incubation is not null)
        {
            card.Details = new()
            {
                ("Incubator", incubation.IncubatorName ?? "-"),
                ("Temperature", incubation.Temperature ?? "-"),
                ("Duration", incubation.Duration ?? "-"),
                ("Expected reading", Dt(incubation.ExpectedReadingAt))
            };
        }

        if (reading is not null)
        {
            card.Stats = new()
            {
                ("Plate count", reading.PlateReadings),
                ("Dilution", reading.DilutionFactor.ToString()),
                ("Average", reading.Average.ToString()),
                ("Calculated", reading.CalculatedResult.ToString())
            };
            card.MetaLines.Add($"Reported: {reading.ReportedResult}    Limits (alert/action/spec): {reading.AlertLimit ?? "-"} / {reading.ActionLimit ?? "-"} / {reading.SpecLimit ?? "-"}");
            card.MetaLines.Add($"Actual reading: {Dt(reading.EnteredAt)}");
        }

        foreach (var p in t.PathogenObservations.OrderBy(p => p.StepOrder))
            card.Rows.Add((Humanize(p.StepName), $"{(p.GrowthObserved ? "Growth observed" : "No growth")}  |  {p.ObservedByName}  |  {Dt(p.ObservedAt)}"));

        // EM/After Cleaning batch results - a genuine table, one row per
        // location, mirroring SampleSummaryDialog's LocationResultsTable
        // columns exactly (Location / Limits / CFU / Reported / Status / Entered By).
        if (t.Locations.Count > 0)
        {
            card.TableColumns = new()
            {
                new("Location", 0.20),
                new("Limits (Alert/Action/Spec)", 0.22),
                new("CFU", 0.08),
                new("Reported", 0.10),
                new("Status", 0.16),
                new("Entered By", 0.24)
            };
            card.TableStatusColumnIndex = 4;
            foreach (var loc in t.Locations)
            {
                var label = loc.GradeClassification is null ? loc.LocationName : $"{loc.LocationName} ({loc.GradeClassification})";
                card.TableRows.Add(new List<string>
                {
                    label,
                    $"{loc.AlertLimit ?? "-"}/{loc.ActionLimit ?? "-"}/{loc.SpecLimit ?? "-"}",
                    loc.CFUResult?.ToString() ?? "-",
                    loc.ReportedResult ?? "-",
                    loc.Status ?? "-",
                    $"{loc.EnteredByName ?? "-"} · {Dt(loc.EnteredAt)}"
                });
            }
        }

        if (reading is null && t.PathogenObservations.Count == 0 && t.Locations.Count == 0 && lastResult is not null)
            card.Rows.Add(("Result", lastResult.InterpretedValue ?? lastResult.RawValue));

        var enteredBy = reading?.EnteredByName ?? lastResult?.EnteredByName
            ?? t.PathogenObservations.LastOrDefault()?.ObservedByName
            ?? t.Locations.LastOrDefault()?.EnteredByName;
        var enteredAt = reading?.EnteredAt ?? lastResult?.EnteredAt
            ?? t.PathogenObservations.LastOrDefault()?.ObservedAt
            ?? t.Locations.LastOrDefault()?.EnteredAt;

        card.FooterLeft = enteredBy is null ? "No result recorded yet" : $"Entered by {enteredBy} · {Dt(enteredAt)}";
        card.FooterRight = t.IsSuperseded ? "Superseded"
            : t.Locations.Count > 0 ? (hasNonConformingLocation ? "Non-conforming location(s)" : "All locations within spec")
            : Humanize(reading?.Status ?? (t.PathogenObservations.Count > 0 ? (detected ? "Detected" : "Absent") : t.Status));

        return card;
    }

    public static ReportDocument ForMedia(MediaSummaryDto s)
    {
        var tone = s.IsReleasedForUse ? ReportTone.Positive
            : s.ApprovalStatus == "Rejected" || s.Status == "QuarantineFailed" ? ReportTone.Danger
            : s.Evaluation?.Outcome == "Conform" ? ReportTone.Warning
            : ReportTone.Neutral;

        var badge = s.IsReleasedForUse ? "Released for use"
            : s.ApprovalStatus == "Rejected" || s.Status == "QuarantineFailed" ? "Quarantined"
            : s.Evaluation?.Outcome == "Conform" ? "Awaiting release approval"
            : s.Evaluation?.Outcome == "NonConform" ? "Evaluation failed"
            : "Pending evaluation";

        var doc = new ReportDocument
        {
            Kind = "Media lot record",
            DocumentId = s.LotNumber,
            Subtitle = $"{Humanize(s.MediaClass)} · {s.MaterialName}",
            BadgeText = badge,
            BadgeTone = tone,
            HeaderNote = $"Prepared {Dt(s.PreparedAt)}"
        };

        var release = new List<(string, string)>
        {
            ("Inventory status", Humanize(s.Status)),
            ("Approval", Humanize(s.ApprovalStatus)),
            ("Released", s.IsReleasedForUse ? "Yes" : "No"),
            ("Prepared by", s.PreparedByName)
        };
        if (s.ApprovedByName is not null) release.Add(("Decided by", s.ApprovedByName));
        if (s.ApprovedAt is not null) release.Add(("Decided at", Dt(s.ApprovedAt)));

        doc.Blocks.Add(new TwoColumnBlock
        {
            LeftLabel = "lot identity",
            Left = new()
            {
                ("Lot number", s.LotNumber),
                ("Media class", Humanize(s.MediaClass)),
                ("Source material", s.MaterialName),
                ("Manufacturer", string.IsNullOrEmpty(s.ManufacturerName) ? "-" : s.ManufacturerName),
                ("Mfr. lot", string.IsNullOrEmpty(s.ManufacturerLot) ? "-" : s.ManufacturerLot),
                ("Expires", D(s.ExpiryDate))
            },
            RightLabel = "release status", Right = release
        });

        doc.Blocks.Add(new StripBlock
        {
            Label = "preparation record",
            Items = new()
            {
                ("Weight / volume", $"{s.TotalWeight} g · {s.TotalVolume}", null),
                ("Autoclave", s.AutoclaveName ?? "-", $"{s.AutoclaveProgram} · {s.LoadType}"),
                ("Cycle", $"{s.Temperature} °C · {s.CycleTime} min", $"Cycle no. {s.CycleNumber}"),
                ("pH", s.Ph.ToString(), Dt(s.PreparedAt))
            }
        });

        doc.Blocks.Add(new HeadingBlock { Text = "media evaluation" });
        if (s.Evaluation is null)
        {
            doc.Blocks.Add(new ListBlock { Label = "media evaluation", Rows = new(), EmptyText = "No evaluation assigned." });
        }
        else
        {
            var e = s.Evaluation;
            var card = new CardBlock
            {
                Title = Humanize(e.EvaluationType),
                Subtitle = $"Assigned {Dt(e.AssignedAt)}" + (e.CompletedByName is null ? "" : $" · Completed by {e.CompletedByName}"),
                HeadlineValue = Humanize(e.Outcome),
                HeadlineUnit = Humanize(e.Status),
                Tone = e.Outcome == "NonConform" ? ReportTone.Danger : e.Outcome is null ? ReportTone.Neutral : ReportTone.Positive,
                FooterLeft = e.CompletedAt is null ? "Evaluation in progress" : $"Completed {Dt(e.CompletedAt)}",
                FooterRight = Humanize(e.Outcome ?? e.Status)
            };

            foreach (var c in e.Challenges)
            {
                var detail = new List<string>();
                if (c.RecoveryPercent is not null) detail.Add($"Recovery {c.RecoveryPercent}% ({c.OldMediaCount} to {c.NewMediaCount})");
                if (c.GrowthObserved is not null) detail.Add(c.GrowthObserved.Value ? "Growth observed" : "No growth");
                if (c.ObservedDescription is not null) detail.Add($"Observed: {c.ObservedDescription}");
                if (c.IsTurbid is not null) detail.Add(c.IsTurbid.Value ? "Turbid" : "Clear");
                if (c.ReadByName is not null) detail.Add($"{c.ReadByName} · {Dt(c.ReadAt)}");

                card.Rows.Add((
                    c.OrganismName + (c.ChallengeRole is null ? "" : $" ({Humanize(c.ChallengeRole)})"),
                    $"{Humanize(c.Outcome)}" + (detail.Count > 0 ? "  |  " + string.Join("  |  ", detail) : "")
                ));
            }
            if (e.Challenges.Count == 0)
                card.Rows.Add(("No challenge organisms configured", "The lot cannot conform until challenge specs exist"));

            doc.Blocks.Add(card);
        }

        doc.Blocks.Add(new HeadingBlock { Text = "lifecycle timeline" });
        doc.Blocks.Add(ToTimelineBlock(s.Timeline));

        if (s.Signatures.Count > 0)
        {
            doc.Blocks.Add(new HeadingBlock { Text = "electronic signatures" });
            doc.Blocks.Add(ToSignatureBlock(s.Signatures));
        }

        return doc;
    }

    public static ReportDocument ForCryovial(CryovialSummaryDto s)
    {
        var tone = s.IsDestroyed || s.ApprovalStatus == "Rejected" ? ReportTone.Danger
            : s.ApprovalStatus == "Approved" ? ReportTone.Positive
            : ReportTone.Warning;

        var badge = s.ApprovalStatus == "Rejected" ? "Rejected"
            : s.IsDestroyed ? "Destroyed"
            : s.ApprovalStatus == "Approved" ? "Approved for use"
            : "Awaiting approval";

        var doc = new ReportDocument
        {
            Kind = "Cryovial batch record",
            DocumentId = s.Code,
            Subtitle = $"{s.OrganismName} · {s.MaterialName}",
            BadgeText = badge,
            BadgeTone = tone,
            HeaderNote = $"Prepared {Dt(s.PreparedAt)}"
        };

        var stock = new List<(string, string)>
        {
            ("Vials prepared", s.NumberOfVialsPrepared.ToString()),
            ("Vials remaining", s.VialsRemaining + (s.VialsRemaining == 0 ? " (depleted)" : "")),
            ("Storage", string.IsNullOrEmpty(s.StorageCondition) ? "-" : s.StorageCondition),
            ("Approval", Humanize(s.ApprovalStatus)),
            ("Prepared by", s.PreparedByName)
        };
        if (s.ApprovedByName is not null) stock.Add(("Decided by", s.ApprovedByName));
        if (s.ApprovedAt is not null) stock.Add(("Decided at", Dt(s.ApprovedAt)));

        doc.Blocks.Add(new TwoColumnBlock
        {
            LeftLabel = "batch identity",
            Left = new()
            {
                ("Code", s.Code),
                ("Organism", s.OrganismName),
                ("Source material", s.MaterialName),
                ("Material batch", string.IsNullOrEmpty(s.MaterialBatchNumber) ? "-" : s.MaterialBatchNumber),
                ("Manufacturer", string.IsNullOrEmpty(s.ManufacturerName) ? "-" : s.ManufacturerName),
                ("Expires", D(s.ExpiryDate))
            },
            RightLabel = "stock & approval", Right = stock
        });

        doc.Blocks.Add(new HeadingBlock { Text = "identity confirmation panel" });
        var panel = new CardBlock
        {
            Title = "Identity confirmation",
            Subtitle = $"{s.IdentityConfirmations.Count} media row(s) · the only place batch identity is confirmed",
            Tone = s.IdentityConfirmations.Count == 0 ? ReportTone.Neutral : ReportTone.Positive,
            FooterLeft = string.IsNullOrEmpty(s.PhysicalCheckText) ? "No physical check recorded" : $"Physical check: {s.PhysicalCheckText}",
            FooterRight = s.IdentityConfirmations.Count == 0 ? "Not confirmed" : "Confirmed"
        };
        foreach (var i in s.IdentityConfirmations)
            panel.Rows.Add((
                $"{i.MediaLotNumber ?? "-"}" + (i.IncubatorName is null ? "" : $" · {i.IncubatorName}"),
                $"{i.ObservationText}  |  {D(i.IncubationStart)} to {D(i.IncubationEnd)}"));
        if (s.IdentityConfirmations.Count == 0)
            panel.Rows.Add(("No panel rows recorded", "-"));
        doc.Blocks.Add(panel);

        if (s.ThawHistory.Count > 0)
        {
            doc.Blocks.Add(new HeadingBlock { Text = "thaw history" });
            doc.Blocks.Add(new ListBlock
            {
                Label = "thaw history",
                Rows = s.ThawHistory.Select(t => (
                    Left: "Vial thawed",
                    Right: $"{t.ThawedByName}  |  {Dt(t.ThawedAt)}" + (string.IsNullOrWhiteSpace(t.Notes) ? "" : $"  |  {t.Notes}")
                )).ToList()
            });
        }

        doc.Blocks.Add(new HeadingBlock { Text = "lifecycle timeline" });
        doc.Blocks.Add(ToTimelineBlock(s.Timeline));

        if (s.Signatures.Count > 0)
        {
            doc.Blocks.Add(new HeadingBlock { Text = "electronic signatures" });
            doc.Blocks.Add(ToSignatureBlock(s.Signatures));
        }

        return doc;
    }
}
