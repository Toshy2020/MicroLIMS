using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Abstractions.Pdf;

namespace MicroLIMS.Application.Services;

// Maps each summary DTO onto the renderer-agnostic ReportDocument model.
// Kept in one place so the three archived documents share a vocabulary
// (same section order, same wording) rather than each inventing its own.
public static class ReportDocumentMapper
{
    private static string Dt(DateTime? v) => v is null ? "-" : v.Value.ToString("dd-MMM-yyyy HH:mm");
    private static string D(DateTime? v) => v is null ? "-" : v.Value.ToString("dd-MMM-yyyy");
    private static string FormatLimit(string? limit) => string.IsNullOrWhiteSpace(limit) ? "-" : limit;

    private static string Humanize(ResultStatus? v) => Humanize(v?.ToString());
    private static string Humanize(string? v) => ReportVocabulary.Humanize(v);

    // Humanize would render these as "No growth" / "Growth non
    // conforming" / "Growth conforming", which reads as a grading of the
    // growth rather than a statement about the target organism. A
    // reviewer has to be able to tell at a glance that growth occurred
    // but was not the organism under test, so each state gets its own
    // sentence instead.
    private static string ObservationText(string? observation) => observation switch
    {
        "NoGrowth" => "No growth",
        "GrowthNonConforming" => "Growth observed - does not match target organism",
        "GrowthConforming" => "Growth observed - matches target organism",
        _ => Humanize(observation)
    };

    private static SignatureBlock ToSignatureBlock(List<SignatureDto> signatures) => new()
    {
        Signatures = signatures.Select(s => new SignatureEntry
        {
            PrintedName = s.PrintedName,
            Username = s.Username,
            Role = Humanize(s.Role),
            SignedAt = Dt(s.SignedAt),
            Meaning = s.MeaningText,
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
            BadgeText = s.StatusDisplay,
            BadgeTone = tone,
            HeaderNote = $"Received {Dt(s.ReceivedAt)}"
        };

        // After Cleaning samples are traced to the product made before the
        // clean, so they show that product and its batch in place of a
        // batch of their own.
        var afterCleaning = s.Category == "AfterCleaning";
        var identity = new List<(string, string)>
        {
            ("Reference", s.ReferenceNumber),
            ("Category", Humanize(s.Category)),
            ("Item", string.IsNullOrEmpty(s.DisplayName) ? "-" : s.DisplayName)
        };
        if (afterCleaning)
        {
            identity.Add(("Prev. product", s.PreviousProductName ?? "-"));
            identity.Add(("Prev. batch", s.PreviousProductBatchNumber ?? s.BatchNumber ?? "-"));
        }
        if (s.ProductionStage is not null) identity.Add(("Stage", s.ProductionStage));
        if (!afterCleaning) identity.Add(("Batch", s.BatchNumber ?? "-"));
        identity.Add(("Control", s.ControlNumber));
        identity.Add(("Cause", s.CauseOfTesting));
        identity.Add(("Quantity", s.SampleQuantity ?? "-"));
        if (s.WaterSamplingPointCode is not null)
            identity.Add(("Sampling point", $"{s.WaterSamplingPointCode}, {s.WaterSamplingPointLocation}"));
        if (s.StorageCondition is not null)
            identity.Add(("Storage", s.StorageCondition == "Refrigerator" ? $"Refrigerator ({s.StorageTimeHours?.ToString() ?? "?"}h)" : s.StorageCondition));

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
                    ("Amount", $"{p.Amount}", null),
                    ("Technique", Humanize(p.Technique), p.Technique == "Filtration" ? $"Filt {p.FiltrationVolume} / Wash {p.WashingVolume}" : null),
                    ("Diluent", string.IsNullOrEmpty(p.Diluent) ? "-" : p.Diluent, null),
                    ("Neutralizer", string.IsNullOrEmpty(p.Neutralizer) ? "-" : p.Neutralizer, null),
                    ("Prepared by", p.PreparedByName, Dt(p.PreparedAt))
                }
            });
        }

        var matrix = ToResultsMatrix(s.TestOrders);
        if (matrix is not null) doc.Blocks.Add(matrix);

        doc.Blocks.Add(new HeadingBlock { Text = "test results" });
        foreach (var t in s.TestOrders)
        {
            doc.Blocks.Add(ToTestCard(t));
            doc.Blocks.AddRange(ToReadingsTables(t));
        }
        if (s.TestOrders.Count == 0)
            doc.Blocks.Add(new ListBlock { Label = "test results", Rows = new(), EmptyText = "No tests recorded." });

        doc.Blocks.Add(new HeadingBlock { Text = "lifecycle timeline" });
        doc.Blocks.Add(ToSampleTimelineBlock(s));

        if (s.Signatures.Count > 0)
        {
            doc.Blocks.Add(new HeadingBlock { Text = "electronic signatures" });
            doc.Blocks.Add(ToSignatureBlock(s.Signatures));
        }

        return doc;
    }

    // The sample's whole life in one dated list: receipt, the first
    // incubation (testing started), then every recorded workflow event
    // with who did it and any comment - in time order.
    private static ListBlock ToSampleTimelineBlock(SampleSummaryDto s)
    {
        var entries = new List<(DateTime At, string Left, string Right)>
        {
            (s.ReceivedAt, "Received", $"{s.ReceivedByName}  |  {Dt(s.ReceivedAt)}")
        };

        var firstIncubation = s.TestOrders.SelectMany(t => t.Incubations).OrderBy(i => i.StartedAt).FirstOrDefault();
        if (firstIncubation is not null)
            entries.Add((firstIncubation.StartedAt, "Testing started",
                (firstIncubation.StartedByName is null ? "" : $"{firstIncubation.StartedByName}  |  ") + Dt(firstIncubation.StartedAt)));

        foreach (var e in s.Timeline)
            entries.Add((e.Timestamp,
                Humanize(e.EventType) + (e.Decision is null ? "" : $" - {Humanize(e.Decision)}"),
                $"{e.PerformedByName}  |  {Dt(e.Timestamp)}" + (string.IsNullOrWhiteSpace(e.Comment) ? "" : $"  |  \"{e.Comment}\"")));

        return new ListBlock
        {
            Label = "lifecycle timeline",
            EmptyText = "No lifecycle events recorded.",
            Rows = entries.OrderBy(e => e.At).Select(e => (e.Left, e.Right)).ToList()
        };
    }

    // A CFU count is a whole-number concept; averages that don't divide
    // evenly are rounded for display only. The reported result is the
    // value of record and is printed untouched.
    private static string Cfu(decimal? v) =>
        v is null ? "—" : Math.Round(v.Value, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);

    private static string Num(decimal v) => v.ToString("0.############", CultureInfo.InvariantCulture);

    private static readonly HashSet<ResultStatus> ConformingStatuses = new() { ResultStatus.WithinLimits, ResultStatus.Absent };

    private static bool IsQuantitative(TestOrderSummaryDetailDto t) =>
        t.CountTestReadings.Count > 0 || t.Locations.Any(l => l.CFUResult is not null);

    // Every located test against every sampling location - the one-glance
    // overview. Rows are keyed by LocationKey, never the free-text name,
    // which can repeat across distinct points. Superseded rounds and tests
    // without locations are left to their own cards below.
    private static TableBlock? ToResultsMatrix(List<TestOrderSummaryDetailDto> testOrders)
    {
        var located = testOrders.Where(t => t.Locations.Count > 0 && !t.IsSuperseded).ToList();
        if (located.Count == 0) return null;

        var keys = new List<string>();
        var names = new Dictionary<string, string>();
        foreach (var l in located.SelectMany(t => t.Locations))
        {
            if (names.ContainsKey(l.LocationKey)) continue;
            names[l.LocationKey] = l.LocationName;
            keys.Add(l.LocationKey);
        }

        var total = 0;
        var exceptions = 0;
        var rows = new List<List<string>>();
        var tones = new List<List<ReportTone?>>();
        foreach (var key in keys)
        {
            var row = new List<string> { names[key] };
            var rowTones = new List<ReportTone?> { null };
            foreach (var t in located)
            {
                var loc = t.Locations.FirstOrDefault(l => l.LocationKey == key);
                if (loc is null) { row.Add("—"); rowTones.Add(null); continue; }

                total++;
                var conform = loc.Status is null || ConformingStatuses.Contains(loc.Status.Value);
                if (!conform) exceptions++;
                var value = loc.CFUResult ?? loc.CalculatedResult;
                row.Add(IsQuantitative(t)
                    ? (value is null ? "—" : Num(value.Value)) + (conform ? "" : $"\n{Humanize(loc.Status)}")
                    : conform ? "ND" : Humanize(loc.Status));
                rowTones.Add(conform ? ReportTone.Positive : ReportTone.Danger);
            }
            rows.Add(row);
            tones.Add(rowTones);
        }

        var testWidth = 0.72 / located.Count;
        var columns = new List<TableColumn> { new("Location", 0.28) };
        columns.AddRange(located.Select(t =>
            new TableColumn(t.TestCode + (IsQuantitative(t) ? $" ({t.Locations.FirstOrDefault(l => l.Unit is not null)?.Unit ?? "CFU"})" : ""), testWidth)));

        return new TableBlock
        {
            Label = "results summary: all tests × all locations",
            Summary = $"{located.Count} test{(located.Count == 1 ? "" : "s")} · {keys.Count} sampling location{(keys.Count == 1 ? "" : "s")} · {total} result{(total == 1 ? "" : "s")}"
                + (exceptions > 0 ? $", {exceptions} exception{(exceptions == 1 ? "" : "s")}" : ""),
            BadgeText = exceptions > 0 ? $"{exceptions} exception{(exceptions == 1 ? "" : "s")}" : "All conform",
            BadgeTone = exceptions > 0 ? ReportTone.Danger : ReportTone.Positive,
            Table = new ReportTable { Columns = columns, Rows = rows, CellTones = tones },
            Legend = "ND = not detected / conforms. Full detail for each test follows."
        };
    }

    private static CardBlock ToTestCard(TestOrderSummaryDetailDto t)
    {
        if (t.Analysis is not null) return ToAnalysisCard(t, t.Analysis);

        var reading = t.CountTestReadings.LastOrDefault();
        var incubation = t.Incubations.LastOrDefault();
        var lastResult = t.Results.LastOrDefault();
        // Only conforming growth is the target organism. NoGrowth and
        // GrowthNonConforming are both "absent" - something growing that
        // is not the organism under test is not a detection, and reading
        // it as one puts a false positive on a released GMP record.
        // When biochemical identification was performed, its explicit
        // Detected/Absent call is authoritative and overrides the
        // selective-plating morphology alone - a chain can show
        // GrowthConforming at selective plating and still resolve to
        // absent once biochemical testing rules the organism out (the
        // scenario that motivated this override: an Inconclusive
        // confirmatory result followed by a biochemical result indicating
        // absence was previously still archived as "Detected").
        var qualitative = TestOrderConformance.Qualitative(t);
        var detected = qualitative.Result == nameof(ResultStatus.Detected);

        var tone = CardTone(t);

        // The headline slot is a compact single-value stat (a CFU number,
        // "Detected"/"Absent") - the full "X locations: Y conform..."
        // sentence belongs in the table summary below, not here, or it
        // starves the title column of width and wraps it word-by-word.
        ResultStatus? worstLocationStatus = null;
        if (t.Locations.Count > 0)
        {
            var severity = new[] { ResultStatus.WithinLimits, ResultStatus.Absent, ResultStatus.PendingConfirmation, ResultStatus.LimitsNotConfigured, ResultStatus.AlertLimitExceeded, ResultStatus.ActionLimitExceeded, ResultStatus.RequiresReview, ResultStatus.OutOfSpecification, ResultStatus.Detected };
            worstLocationStatus = ResultStatus.WithinLimits;
            foreach (var loc in t.Locations)
            {
                if (loc.Status is null) continue;
                if (Array.IndexOf(severity, loc.Status.Value) > Array.IndexOf(severity, worstLocationStatus.Value))
                    worstLocationStatus = loc.Status;
            }
        }

        var headline = worstLocationStatus is not null ? Humanize(worstLocationStatus)
            : reading?.ReportedResult
            ?? (t.PathogenObservations.Count > 0 ? (detected ? nameof(ResultStatus.Detected) : nameof(ResultStatus.Absent)) : null)
            ?? lastResult?.InterpretedValue ?? lastResult?.RawValue ?? "-";

        var step = incubation?.StepName ?? t.CurrentStep;
        var card = new CardBlock
        {
            Title = $"{t.TestCode} - {t.TestDisplayName}",
            Subtitle = CardSubtitle(t, (string.IsNullOrWhiteSpace(step) ? "" : $"Step: {Humanize(step)}")
                + (incubation?.MediaLotNumber is null ? "" : $" · Media lot: {incubation.MediaLotNumber}")),
            HeadlineValue = headline,
            HeadlineUnit = t.Locations.Count > 0 ? $"{t.Locations.Count} location{(t.Locations.Count == 1 ? "" : "s")}"
                : reading is not null ? $"CFU · {Humanize(reading.Status)}" : Humanize(t.Status),
            Tone = tone
        };

        AddSpecificationLine(card, t);

        if (t.Incubations.Count > 0)
            card.Tables.Add(ToIncubationTable(t.Incubations));

        if (reading is not null)
        {
            card.Stats = new()
            {
                ("Plate count", string.IsNullOrEmpty(reading.PlateReadings) ? "—" : reading.PlateReadings),
                ("Dilution", Num(reading.DilutionFactor)),
                ("Average", Cfu(reading.Average)),
                ("Calculated", Cfu(reading.CalculatedResult))
            };
            card.MetaLines.Add($"Reported: {reading.ReportedResult}    Limits (alert/action/spec): {FormatLimit(reading.AlertLimit)} / {FormatLimit(reading.ActionLimit)} / {FormatLimit(reading.SpecLimit)}");
            card.MetaLines.Add($"Entered by: {reading.EnteredByName}    Entered at: {Dt(reading.EnteredAt)}");
        }

        foreach (var p in t.PathogenObservations.OrderBy(p => p.StepOrder))
            card.Rows.Add((Humanize(p.StepName), $"{ObservationText(p.Observation)}  |  {p.ObservedByName}  |  {Dt(p.ObservedAt)}"));

        foreach (var b in t.BiochemicalResults)
        {
            var call = b.OrganismDetected is true ? nameof(ResultStatus.Detected) : b.OrganismDetected is false ? "Not Detected" : "Undetermined";
            card.Rows.Add((Humanize(b.StepName), $"{call}: {b.BiochemicalResultText}  |  {b.SubmittedByName}  |  {Dt(b.SubmittedAt)}"));
        }

        // EM/After Cleaning batch results - a genuine table, one row per
        // location, mirroring SampleSummaryDialog's LocationResultsTable
        // columns exactly (Location / Limits / CFU / Reported / Status / Entered By).
        if (t.Locations.Count > 0)
        {
            // Unit comes from the location data (TestWorkflowEngine.
            // DeriveBatchLocationUnit) - EM/After Cleaning/Water mix
            // CFU/plate/4 hours, CFU/25 cm2, and CFU/mL depending on
            // sampling method; never assume one unit for the whole report.
            var unit = t.Locations.FirstOrDefault(l => l.Unit is not null)?.Unit ?? "CFU";
            card.TableColumns = new()
            {
                new("Location", 0.18),
                new("Limits (Alert/Action/Spec)", 0.18),
                new(unit, 0.13),
                new("Reported", 0.11),
                new("Status", 0.18),
                new("Entered By", 0.22)
            };
            card.TableStatusColumnIndex = 4;
            foreach (var loc in t.Locations)
            {
                var label = loc.GradeClassification is null ? loc.LocationName : $"{loc.LocationName} ({loc.GradeClassification})";
                card.TableRows.Add(new List<string>
                {
                    label,
                    $"{FormatLimit(loc.AlertLimit)}/{FormatLimit(loc.ActionLimit)}/{FormatLimit(loc.SpecLimit)}",
                    loc.CFUResult?.ToString() ?? "-",
                    loc.ReportedResult ?? "-",
                    loc.Status?.ToString() ?? "-",
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
            : t.Locations.Count > 0 ? TestOrderConformance.Overall(t) switch
            {
                ResultConformance.Conforms => "All locations within spec",
                ResultConformance.DoesNotConform => "Non-conforming location(s)",
                _ => "Not all locations can be certified"
            }
            : Humanize(reading?.Status.ToString() ?? (t.PathogenObservations.Count > 0 ? (detected ? nameof(ResultStatus.Detected) : nameof(ResultStatus.Absent)) : t.Status));

        return card;
    }

    // The same verdict as the Certificate of Analysis: a failure is red,
    // and a missing result or one with no limits cannot be shown as a pass.
    private static ReportTone CardTone(TestOrderSummaryDetailDto t) =>
        t.IsSuperseded ? ReportTone.Neutral
            : TestOrderConformance.Overall(t) switch
            {
                ResultConformance.Conforms => ReportTone.Positive,
                ResultConformance.DoesNotConform => ReportTone.Danger,
                _ => ReportTone.Warning
            };

    // Superseded rounds, results pulled through from a retest sample, and
    // tests closed when another lab rejected the sample all say so up
    // front - a reader must not take them for this sample's live result.
    private static string CardSubtitle(TestOrderSummaryDetailDto t, string rest) =>
        string.Join(" · ", new[]
        {
            t.IsSuperseded ? "SUPERSEDED BY RETEST" : null,
            t.SourceSampleReferenceNumber is null ? null : $"Result from retest sample {t.SourceSampleReferenceNumber}",
            t.CancelledAtStep is null ? null : $"Closed at {Humanize(t.CancelledAtStep)}{(t.CancelledAtStage is null ? "" : $" (stage {t.CancelledAtStage})")}",
            rest
        }.Where(p => !string.IsNullOrEmpty(p)));

    private static void AddSpecificationLine(CardBlock card, TestOrderSummaryDetailDto t)
    {
        if (!string.IsNullOrWhiteSpace(t.SpecificationText))
            card.MetaLines.Add($"Specification: {t.SpecificationText}");
    }

    // One row per incubation, every value in full. The stage is named only
    // when the step really was split into two incubation stages; separate
    // steps (TSB, RVS, XLD) are told apart by their step name.
    private static ReportTable ToIncubationTable(List<IncubationDetailDto> incubations)
    {
        var hasOutcome = incubations.Any(i => !string.IsNullOrWhiteSpace(i.Outcome));
        var columns = new List<TableColumn>
        {
            new("Step", hasOutcome ? 0.14 : 0.16),
            new("Media lot", hasOutcome ? 0.15 : 0.18),
            new("Incubator", hasOutcome ? 0.11 : 0.13),
            new("Temperature", hasOutcome ? 0.11 : 0.12),
            new("Duration", hasOutcome ? 0.10 : 0.11),
            new("Started", hasOutcome ? 0.14 : 0.15),
            new("Completed / transferred", hasOutcome ? 0.14 : 0.15)
        };
        if (hasOutcome) columns.Add(new("Outcome", 0.11));

        var rows = new List<List<string>>();
        foreach (var inc in incubations)
        {
            var split = inc.StageNumber > 1 || inc.TransferredAt is not null || inc.TransferredByName is not null
                || incubations.Any(o => !ReferenceEquals(o, inc) && o.StepName == inc.StepName);
            var transferred = inc.TransferredAt is not null || inc.TransferredByName is not null;
            var endAt = transferred ? inc.TransferredAt ?? inc.CompletedAt : inc.CompletedAt;
            var endBy = transferred ? inc.TransferredByName : inc.CompletedByName;

            var row = new List<string>
            {
                Humanize(inc.StepName) + (split ? $"\nStage {inc.StageNumber}" : ""),
                inc.MediaLotNumber is null ? "-" : inc.MediaLotNumber + (inc.MediaMaterialName is null ? "" : $"\n{inc.MediaMaterialName}"),
                inc.IncubatorName ?? "-",
                inc.Temperature ?? "-",
                inc.Duration ?? "-",
                Dt(inc.StartedAt) + (inc.StartedByName is null ? "" : $"\n{inc.StartedByName}"),
                endAt is null ? "-" : Dt(endAt) + (endBy is null ? "" : $"\n{endBy}") + (transferred ? "\n(transferred)" : "")
            };
            if (hasOutcome) row.Add(inc.Outcome ?? "-");
            rows.Add(row);
        }

        return new ReportTable { Caption = "Incubation", Columns = columns, Rows = rows };
    }

    // Generic TestAnalysis (physico-chemical) results: the parameter table
    // with its verdicts, and the run details. Raw readings follow the card
    // as their own tables (ToReadingsTables) so a long run can page-break.
    private static CardBlock ToAnalysisCard(TestOrderSummaryDetailDto t, AnalysisDetailDto a)
    {
        var exception = a.ParameterResults.Any(p => !ConformingStatuses.Contains(p.ComparisonStatus));
        var single = a.ParameterResults.Count == 1 ? a.ParameterResults[0] : null;
        var verdict = t.IsSuperseded ? "Superseded" : exception ? "Out of limits" : "Within limits";

        var card = new CardBlock
        {
            Title = $"{t.TestCode} - {t.TestDisplayName}",
            Subtitle = CardSubtitle(t, (a.EquipmentCode is null ? "" : $"Instrument: {a.EquipmentCode} · ") + $"Analysed: {Dt(a.AnalysedAt)}"),
            HeadlineValue = single is not null ? single.ReportedDisplay
                : $"{a.ParameterResults.Count} parameter{(a.ParameterResults.Count == 1 ? "" : "s")}",
            HeadlineUnit = verdict,
            Tone = CardTone(t)
        };

        AddSpecificationLine(card, t);

        var instrument = a.EquipmentCode is null ? a.EquipmentName ?? "-"
            : a.EquipmentCode + (a.EquipmentName is null ? "" : $" ({a.EquipmentName})");
        card.MetaLines.Add($"Instrument: {instrument}    Analysed: {Dt(a.AnalysedAt)}"
            + (a.UnitAmount is null ? "" : $"    Unit amount: {Num(a.UnitAmount.Value)}"));
        var conditions = a.AnalysisType == WorkflowType.Titration ? TitrationConditions(a.ConditionsJson) : GenericConditions(a.ConditionsJson);
        if (conditions is not null) card.MetaLines.Add($"Conditions: {conditions}");
        if (!string.IsNullOrWhiteSpace(a.Comment)) card.MetaLines.Add($"Comment: {a.Comment}");
        card.MetaLines.Add($"Entered by: {a.EnteredByName ?? "-"}    Entered at: {Dt(a.EnteredAt)}");

        card.TableColumns = new()
        {
            new("Parameter", 0.30),
            new("Reported", 0.20),
            new("Unit", 0.12),
            new("Spec limit", 0.20),
            new("Status", 0.18)
        };
        card.TableStatusColumnIndex = 4;
        foreach (var p in a.ParameterResults)
        {
            var flags = string.Join(", ", new[] { p.OverRange ? ">Range" : null, p.BelowLoq ? "<LOQ" : null }.Where(f => f is not null));
            card.TableRows.Add(new List<string>
            {
                p.ParameterName,
                WithoutUnit(p.ReportedDisplay, p.Unit) + (flags.Length > 0 ? $" [{flags}]" : ""),
                p.Unit ?? "-",
                p.SpecLimit ?? "-",
                p.ComparisonStatus.ToString()
            });
        }

        card.FooterLeft = a.EnteredByName is null ? "No result recorded yet" : $"Entered by {a.EnteredByName} · {Dt(a.EnteredAt)}";
        card.FooterRight = verdict;
        return card;
    }

    // The reported text without a trailing unit the Unit column already shows.
    private static string WithoutUnit(string display, string? unit)
    {
        var text = display.Trim();
        var u = unit?.Trim();
        return !string.IsNullOrEmpty(u) && text.EndsWith($" {u}", StringComparison.Ordinal) ? text[..^(u.Length + 1)] : text;
    }

    private static string? GenericConditions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var parsed = JsonDocument.Parse(json);
            if (parsed.RootElement.ValueKind != JsonValueKind.Object) return parsed.RootElement.GetRawText();
            var parts = parsed.RootElement.EnumerateObject().Select(p => $"{p.Name}: {JsonText(p.Value)}").ToList();
            return parts.Count == 0 ? null : string.Join(" · ", parts);
        }
        catch (JsonException)
        {
            return json;
        }
    }

    // The TitrationSnapshot TitrationRecorder stores on ConditionsJson:
    // titrant details nested under "titrant", keys matched case-insensitively.
    private static string? TitrationConditions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var parsed = JsonDocument.Parse(json);
            var root = parsed.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            var titrant = Pick(root, "titrant") is { ValueKind: JsonValueKind.Object } nested ? nested : root;

            var parts = new List<string>();
            if (Pick(titrant, "code", "titrantPreparationCode", "titrantCode") is { } code) parts.Add($"Titrant: {JsonText(code)}");
            if (Pick(titrant, "factorUsed", "factor") is { } factor && decimal.TryParse(JsonText(factor), NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
            {
                var state = Pick(titrant, "factorState");
                parts.Add($"Factor used: {f.ToString("0.0000", CultureInfo.InvariantCulture)}" + (state is null ? "" : $" ({Humanize(JsonText(state.Value))})"));
            }
            if (Pick(root, "warnings") is { ValueKind: JsonValueKind.Array } warnings && warnings.GetArrayLength() > 0)
                parts.Add($"Warnings: {string.Join("; ", warnings.EnumerateArray().Select(JsonText))}");
            if (Pick(root, "dueTitrantAcknowledgement") is { ValueKind: JsonValueKind.Object } ack && Pick(ack, "justification") is { } justification)
                parts.Add($"Due titrant acknowledged: {JsonText(justification)}");
            return parts.Count == 0 ? null : string.Join(" · ", parts);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonElement? Pick(JsonElement obj, params string[] names)
    {
        foreach (var name in names)
            foreach (var p in obj.EnumerateObject())
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)
                    && p.Value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
                    && !(p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() == ""))
                    return p.Value;
        return null;
    }

    private static string JsonText(JsonElement e) => e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : e.GetRawText();

    private static bool IsIcpCalculation(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var parsed = JsonDocument.Parse(json);
            return parsed.RootElement.ValueKind == JsonValueKind.Object
                && Pick(parsed.RootElement, "quantity") is { ValueKind: JsonValueKind.String } q
                && (q.GetString() ?? "").StartsWith("Icp", StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return json.Contains("\"quantity\"") && json.Contains("\"Icp");
        }
    }

    // Raw readings for each analysis parameter, one table per parameter,
    // with the columns named for the analysis type (vessel peak areas,
    // disintegration times, capsule gross/shell weights, titrant volumes).
    // HPLC and ICP keep their calculation in their own review records.
    private static IEnumerable<TableBlock> ToReadingsTables(TestOrderSummaryDetailDto t)
    {
        var a = t.Analysis;
        if (a is null || a.AnalysisType is WorkflowType.HplcMethodAssay or WorkflowType.IcpMethodAssay) yield break;

        foreach (var p in a.ParameterResults)
        {
            if (p.Readings.Count == 0 || IsIcpCalculation(p.CalculationJson)) continue;

            var isVessel = p.Readings.Any(r => r.Kind == ReadingKind.Vessel);
            var isDisintegration = a.AnalysisType == WorkflowType.Disintegration;
            var isWeightVariation = a.AnalysisType == WorkflowType.WeightVariation;
            var isTitration = a.AnalysisType == WorkflowType.Titration;
            var isTablet = isWeightVariation && p.Readings.All(r => r.Value2 is null);
            var hasStage = p.Readings.Any(r => r.Stage is not null);
            var hasTimePoint = !isDisintegration && !isWeightVariation && p.Readings.Any(r => r.TimePointMinutes is not null);
            var hasValue1 = p.Readings.Any(r => r.Value1 is not null || (isDisintegration && !string.IsNullOrEmpty(r.Text)));
            var hasValue2 = !isTablet && p.Readings.Any(r => r.Value2 is not null);
            var hasValue3 = p.Readings.Any(r => r.Value3 is not null);
            var hasText = !isDisintegration && !isWeightVariation && p.Readings.Any(r => !string.IsNullOrEmpty(r.Text));
            var hasComputed = !isDisintegration && !isTablet && p.Readings.Any(r => r.ComputedValue is not null);
            var hasPassed = p.Readings.Any(r => r.Passed is not null);
            var decimals = DecimalsOf(p.ReportedDisplay);
            var stagePrefix = isVessel || isDisintegration || isWeightVariation ? "S" : "";

            var columns = new List<(string Header, Func<ResultReadingDetailDto, string> Cell)>
            {
                ("#", r => r.Index.ToString(CultureInfo.InvariantCulture))
            };
            if (hasStage) columns.Add(("Stage", r => r.Stage is null ? "—" : $"{stagePrefix}{r.Stage}"));
            if (hasTimePoint) columns.Add(("Time point (min)", r => r.TimePointMinutes is null ? "—" : Num(r.TimePointMinutes.Value)));
            if (hasValue1) columns.Add((
                isVessel ? "Peak area" : isDisintegration ? "Time (min)" : isWeightVariation ? (isTablet ? "Weight (mg)" : "Gross (mg)") : isTitration ? "Sample weight (mg)" : "Value 1",
                r => r.Value1 is not null ? Num(r.Value1.Value) : isDisintegration && !string.IsNullOrEmpty(r.Text) ? r.Text! : "—"));
            if (hasValue2) columns.Add((
                isWeightVariation ? "Shell (mg)" : isTitration ? "Titrant volume (mL)" : "Value 2",
                r => r.Value2 is null ? "—" : Num(r.Value2.Value)));
            if (hasValue3) columns.Add((
                isWeightVariation ? "Deviation %" : isTitration ? "Corrected volume (mL)" : "Value 3",
                r => r.Value3 is null ? "—" : isWeightVariation ? $"{r.Value3.Value.ToString("0.00", CultureInfo.InvariantCulture)} %" : Num(r.Value3.Value)));
            if (hasText) columns.Add(("Text", r => r.Text ?? "—"));
            if (hasComputed) columns.Add((
                isVessel ? "% Dissolved" : isWeightVariation ? "Net (mg)" : isTitration ? "Result" : "Computed",
                r => r.ComputedValue is null ? "—"
                    : isVessel ? $"{Num(r.ComputedValue.Value)} %"
                    : isTitration ? r.ComputedValue.Value.ToString("F" + decimals, CultureInfo.InvariantCulture)
                    : Num(r.ComputedValue.Value)));
            if (hasPassed) columns.Add(("Passed", r => r.Passed is null ? "—" : r.Passed.Value ? "Pass" : "Fail"));

            // The index column is narrow; the rest share the width evenly.
            var rest = (1.0 - 0.06) / (columns.Count - 1);
            yield return new TableBlock
            {
                Label = $"{t.TestCode} readings · {p.ParameterName}",
                Table = new ReportTable
                {
                    Columns = columns.Select((c, i) => new TableColumn(c.Header, i == 0 ? 0.06 : rest)).ToList(),
                    Rows = p.Readings.Select(r => columns.Select(c => c.Cell(r)).ToList()).ToList()
                }
            };
        }
    }

    // Decimal places of the first number in a reported value ("99.88 %" -> 2); 2 when none.
    private static int DecimalsOf(string? display)
    {
        var m = Regex.Match(display ?? "", @"-?\d+(?:\.(\d+))?");
        return m.Success ? m.Groups[1].Success ? m.Groups[1].Value.Length : 0 : 2;
    }

    public static ReportDocument ForMedia(MediaSummaryDto s)
    {
        var tone = s.Status == "OutOfStock" ? ReportTone.Neutral
            : s.IsReleasedForUse ? ReportTone.Positive
            : s.ApprovalStatus == "Rejected" || s.Status == "QuarantineFailed" ? ReportTone.Danger
            : s.Evaluation?.Outcome == "Conform" ? ReportTone.Warning
            : ReportTone.Neutral;

        var badge = s.Status == "OutOfStock" ? "Out of Stock"
            : s.IsReleasedForUse ? "Released for use"
            : s.ApprovalStatus == "Rejected" || s.Status == "QuarantineFailed" ? "Rejected"
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
                var sourceLabel = c.CryovialCode ?? c.LyophilizedDiskLabel;
                if (sourceLabel is not null) detail.Add($"Source: {sourceLabel}");
                if (!string.IsNullOrEmpty(c.InitialInoculum)) detail.Add($"Initial Inoculum: {c.InitialInoculum}");
                if (c.RecoveryPercent is not null) detail.Add($"Recovery {c.RecoveryPercent}% ({c.OldMediaCount} to {c.NewMediaCount})");
                if (c.ReferenceMediaLabel is not null) detail.Add($"Reference Lot: {c.ReferenceMediaLabel}");
                if (c.GrowthObserved is not null) detail.Add(c.GrowthObserved.Value ? "Growth observed" : "No growth");
                if (c.ObservedDescription is not null) detail.Add($"Observed: {c.ObservedDescription}");
                if (c.ExpectedDescription is not null) detail.Add($"Expected: {c.ExpectedDescription}");
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
            FooterLeft = s.PhysicalCheckConfirmed
                ? (string.IsNullOrEmpty(s.PhysicalCheckText)
                    ? "Physical check: Confirmed against reference description"
                    : $"Physical check: Confirmed against reference description ({s.PhysicalCheckText})")
                : (string.IsNullOrEmpty(s.PhysicalCheckText)
                    ? "No physical check recorded"
                    : $"Physical check: {s.PhysicalCheckText}"),
            FooterRight = s.IdentityConfirmations.Count == 0 ? "Not confirmed" : "Confirmed"
        };
        foreach (var i in s.IdentityConfirmations)
            panel.Rows.Add((
                $"{i.MediaLotNumber ?? "-"}" + (i.IncubatorName is null ? "" : $" · {i.IncubatorName}"),
                $"{i.ObservationText}  |  {D(i.IncubationStart)} to {D(i.IncubationEnd)}"));
        if (s.IdentityConfirmations.Count == 0)
            panel.Rows.Add(("No panel rows recorded", "-"));
        doc.Blocks.Add(panel);

        if (s.UnthawedVialsDestruction is not null)
        {
            doc.Blocks.Add(new HeadingBlock { Text = "unthawed vials destroyed" });
            doc.Blocks.Add(new ListBlock
            {
                Label = "unthawed vials destroyed",
                Rows = new() { (Left: "Biological waste destruction", Right: s.UnthawedVialsDestruction) }
            });
        }

        if (s.ThawHistory.Count > 0)
        {
            doc.Blocks.Add(new HeadingBlock { Text = "thaw history" });
            doc.Blocks.Add(new ListBlock
            {
                Label = "thaw history",
                Rows = s.ThawHistory.Select(t => (
                    Left: "Vial thawed",
                    Right: $"{t.ThawedByName}  |  {Dt(t.ThawedAt)}" + (string.IsNullOrWhiteSpace(t.Notes) ? "" : $"  |  {t.Notes}")
                        + (t.UsedInMediaLots.Count == 0 ? "" : $"  |  Used in: {string.Join(", ", t.UsedInMediaLots)}")
                        + $"  |  Destroyed: {t.Destruction ?? "not yet"}"
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
