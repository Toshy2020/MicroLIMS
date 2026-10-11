using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Application.Services;

// What block E of GetSessionAsync works out: the shared TSB enrichment state
// of a sample, plus the per-test TSB facts the assigned-test DTOs need.
public sealed record SharedTsbResult(
    SharedTsbStateDto Dto,
    List<string> ApplicableCodes,
    Dictionary<int, (Incubation Incubation, IncubationWindow? Window, bool IsComplete)> OwnTsbByOrder,
    bool IsIncubating,
    bool IsCompleted);

// Pure: no database access, no clock - "now" is passed in.
public static class SharedTsbStateBuilder
{
    public static SharedTsbResult Build(
        Sample sample,
        PathogenSessionData data,
        IReadOnlyDictionary<int, (int MaterialId, int? MediaProductId)> mediaLookup,
        int locationCount,
        DateTime utcNow)
    {
        var testDefs = data.TestDefs;
        var incubations = data.Incubations;
        var UserName = data.UserName;

        // 1. Tests requiring TSB, each timed by its own TSB medium from Test
        // Master - never step-level hours/temperatures or 18-24 h / 30-35 °C
        // defaults. A shared start only triggers each test's own incubation.
        var tsbStepByOrder = new Dictionary<int, TestWorkflowStep>();
        foreach (var to in sample.TestOrders)
        {
            if (testDefs.TryGetValue(to.TestCode, out var def)
                && def.Steps.OrderBy(s => s.StepOrder).FirstOrDefault(TsbDetectionHelper.IsSharedTsbStep) is { } tsbStep)
                tsbStepByOrder[to.Id] = tsbStep;
        }
        var tsbApplicableCodes = sample.TestOrders.Where(t => tsbStepByOrder.ContainsKey(t.Id)).Select(t => t.TestCode).ToList();

        // Each test's own TSB incubation, its window (null when unresolved)
        // and whether its own minimum has elapsed.
        var ownTsbByOrder = new Dictionary<int, (Incubation Incubation, IncubationWindow? Window, bool IsComplete)>();
        foreach (var (orderId, tsbStep) in tsbStepByOrder)
        {
            var ownTsb = TsbDetectionHelper.FindSharedTsbIncubation(incubations.Where(i => i.TestOrderId == orderId));
            if (ownTsb == null) continue;
            var window = IncubationWindowResolver.ForIncubation(tsbStep, ownTsb, mediaLookup);
            ownTsbByOrder[orderId] = (ownTsb, window, window != null && TsbDetectionHelper.IsTsbComplete(ownTsb, window.MinHours, utcNow));
        }

        // Shared TSB State. The tests that joined it share its lot and start
        // but keep their own windows, so it is complete once all of them are.
        var sharedTsbIncubation = TsbDetectionHelper.FindSharedTsbIncubation(incubations);
        DateTime? tsbStart = sharedTsbIncubation?.IncubationStartUtc ?? sharedTsbIncubation?.StartedAt;
        var joined = sample.TestOrders
            .Where(t => sharedTsbIncubation != null
                && ownTsbByOrder.TryGetValue(t.Id, out var own)
                && own.Incubation.MediaId == sharedTsbIncubation.MediaId
                && (own.Incubation.IncubationStartUtc ?? own.Incubation.StartedAt) == tsbStart)
            .Select(t => (t.TestCode, Own: ownTsbByOrder[t.Id]))
            .ToList();

        bool tsbWindowNotConfigured = sharedTsbIncubation != null && (joined.Count == 0 || joined.Any(j => j.Own.Window == null));
        bool tsbCompleted = sharedTsbIncubation != null && !tsbWindowNotConfigured && joined.All(j => j.Own.IsComplete);
        bool tsbIncubating = sharedTsbIncubation != null && !tsbCompleted;
        DateTime? tsbMinReadyAt = sharedTsbIncubation == null || tsbWindowNotConfigured
            ? null
            : joined.Max(j => j.Own.Window!.MinReadyAt(j.Own.Incubation.IncubationStartUtc ?? j.Own.Incubation.StartedAt));

        var rangeTests = sharedTsbIncubation != null
            ? joined.Select(j => (j.TestCode, Windows: j.Own.Window == null ? new List<IncubationWindow>() : new List<IncubationWindow> { j.Own.Window })).ToList()
            : sample.TestOrders.Where(t => tsbStepByOrder.ContainsKey(t.Id))
                .Select(t => (t.TestCode, Windows: IncubationWindowResolver.ConfiguredWindows(tsbStepByOrder[t.Id].StepMedia))).ToList();
        var requiredTemperatureRange = DescribeTsbRanges(rangeTests, w => $"{w.TempMin:0.0} – {w.TempMax:0.0} °C");
        var requiredDurationRange = DescribeTsbRanges(rangeTests, w => $"{w.MinHours} – {w.MaxHours} h");

        SharedTsbStateDto sharedTsbDto;
        if (sharedTsbIncubation != null)
        {
            var expectedCompletionUtc = joined.Count > 0 ? joined.Max(j => j.Own.Incubation.IncubationEndUtc) : sharedTsbIncubation.IncubationEndUtc;
            var temperatures = joined.Select(j => j.Own.Incubation.Temperature).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToList();
            sharedTsbDto = new SharedTsbStateDto(
                IsStarted: true,
                IsIncubating: tsbIncubating,
                IsCompleted: tsbCompleted,
                IsLocked: true, // Once started, parameters are locked
                MediaLotId: sharedTsbIncubation.MediaId,
                MediaLotNumber: sharedTsbIncubation.Media?.LotNumber,
                MediaMaterialName: sharedTsbIncubation.Media?.Material?.MaterialName,
                GptStatus: sharedTsbIncubation.Media?.IsReleasedForUse == true ? "Passed (GPT Conform)" : "Pending",
                SterilityStatus: "Passed",
                IncubatorEquipmentId: sharedTsbIncubation.IncubatorEquipmentId,
                IncubatorCode: sharedTsbIncubation.IncubatorEquipment?.Code,
                RequiredTemperatureRange: requiredTemperatureRange,
                RequiredDurationRange: requiredDurationRange,
                Temperature: temperatures.Count > 0 ? string.Join("; ", temperatures) : sharedTsbIncubation.Temperature,
                IncubationDurationHours: expectedCompletionUtc.HasValue && tsbStart.HasValue
                    ? (int)(expectedCompletionUtc.Value - tsbStart.Value).TotalHours
                    : null,
                ActualStartUtc: tsbStart,
                MinReadyAt: tsbMinReadyAt,
                ExpectedCompletionUtc: expectedCompletionUtc,
                CompletedAtUtc: sharedTsbIncubation.CompletedAt,
                StartedByUserId: sharedTsbIncubation.StartedByUserId,
                StartedByUserName: UserName(sharedTsbIncubation.StartedByUserId),
                ApplicableTestCodes: joined.Select(j => j.TestCode).Distinct().ToList(),
                ApplicableLocationCount: locationCount,
                WindowNotConfigured: tsbWindowNotConfigured);
        }
        else
        {
            sharedTsbDto = new SharedTsbStateDto(
                IsStarted: false,
                IsIncubating: false,
                IsCompleted: false,
                IsLocked: false,
                MediaLotId: null,
                MediaLotNumber: null,
                MediaMaterialName: null,
                GptStatus: null,
                SterilityStatus: null,
                IncubatorEquipmentId: null,
                IncubatorCode: null,
                RequiredTemperatureRange: requiredTemperatureRange,
                RequiredDurationRange: requiredDurationRange,
                Temperature: null,
                IncubationDurationHours: null,
                ActualStartUtc: null,
                MinReadyAt: null,
                ExpectedCompletionUtc: null,
                CompletedAtUtc: null,
                StartedByUserId: null,
                StartedByUserName: null,
                ApplicableTestCodes: tsbApplicableCodes.Distinct().ToList(),
                ApplicableLocationCount: locationCount,
                WindowNotConfigured: rangeTests.Any(t => t.Windows.Count == 0));
        }

        return new SharedTsbResult(sharedTsbDto, tsbApplicableCodes, ownTsbByOrder, tsbIncubating, tsbCompleted);
    }

    // One range when every test agrees; otherwise each test's own, so a test
    // with a different window is never hidden behind another's.
    private static string? DescribeTsbRanges(IEnumerable<(string TestCode, List<IncubationWindow> Windows)> tests, Func<IncubationWindow, string> text)
    {
        var perTest = tests
            .Select(t => (t.TestCode, Text: t.Windows.Count == 0 ? "not configured" : string.Join(" / ", t.Windows.Select(text).Distinct())))
            .ToList();
        if (perTest.Count == 0) return null;

        var distinct = perTest.Select(p => p.Text).Distinct().ToList();
        return distinct.Count == 1 ? distinct[0] : string.Join("; ", perTest.Select(p => $"{p.TestCode}: {p.Text}"));
    }
}
