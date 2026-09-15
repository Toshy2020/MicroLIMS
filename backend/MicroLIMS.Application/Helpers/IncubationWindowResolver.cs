using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Helpers;

// The operative incubation window of one medium on one test's step.
public sealed record IncubationWindow(int StepMediaId, int MinHours, int MaxHours, decimal TempMin, decimal TempMax)
{
    public DateTime MinReadyAt(DateTime startUtc) => startUtc.AddHours(MinHours);
    public DateTime EndAt(DateTime startUtc) => startUtc.AddHours(MaxHours);
    public string DurationText => $"{MinHours}-{MaxHours} hours";
    public string TemperatureText => $"{TempMin}-{TempMax} °C";
}

// One source of truth for every incubation window: the test's own medium as
// selected in Test Master (TestWorkflowStepMedia, whose hours and
// temperatures come from its media configuration). Never the step-level
// TestWorkflowStep hours/temperatures and never a hardcoded default - a
// window that isn't configured blocks rather than being guessed. Each test
// order resolves its own window; a shared step (shared TSB, grouped actions)
// only triggers the start. Stage 2 transfer windows are not a medium's and
// stay on TestWorkflowStepIncubationStage.
public static class IncubationWindowResolver
{
    public static bool IsConfigured(TestWorkflowStepMedia medium) =>
        medium.IncubationMinHours > 0
        && medium.IncubationMaxHours >= medium.IncubationMinHours
        && medium.TempMax > 0
        && medium.TempMax >= medium.TempMin;

    public static IncubationWindow? TryGet(TestWorkflowStepMedia? medium) =>
        medium is not null && IsConfigured(medium)
            ? new IncubationWindow(medium.Id, medium.IncubationMinHours, medium.IncubationMaxHours, medium.TempMin, medium.TempMax)
            : null;

    public static IncubationWindow Require(TestWorkflowStepMedia medium, string? stepName = null) =>
        TryGet(medium) ?? throw NotConfigured(medium, stepName);

    public static WorkflowStepException NotConfigured(TestWorkflowStepMedia? medium, string? stepName)
    {
        var what = medium?.Material?.MaterialName is { Length: > 0 } name ? $"medium \"{name}\"" : "this medium";
        var where = stepName is null ? string.Empty : $" on step \"{stepName}\"";
        return new WorkflowStepException(WorkflowErrorCodes.IncubationWindowNotConfigured,
            $"The incubation window for {what}{where} is not configured - set its incubation hours and temperature in Test Master.");
    }

    // Exact batch first, then the same media product - the match
    // SelectMediaAsync uses to accept a lot for a step.
    public static TestWorkflowStepMedia? MatchStepMedium(IEnumerable<TestWorkflowStepMedia>? stepMedia, int lotMaterialId, int? lotProductId)
    {
        if (stepMedia is null) return null;
        var list = stepMedia as IReadOnlyCollection<TestWorkflowStepMedia> ?? stepMedia.ToList();
        return list.FirstOrDefault(m => m.MaterialId == lotMaterialId)
            ?? list.FirstOrDefault(m => StepMediumMatcher.Matches(m, lotMaterialId, lotProductId));
    }

    // The step medium a started incubation actually used, resolved from its
    // lot. Null when the lot is unknown or matches none of the step's media -
    // never the step's first medium.
    public static TestWorkflowStepMedia? MediumForIncubation(
        TestWorkflowStep step,
        Incubation incubation,
        IReadOnlyDictionary<int, (int MaterialId, int? MediaProductId)>? mediaLookup = null)
    {
        if (incubation.MediaId is not int lotId) return null;
        if (mediaLookup != null && mediaLookup.TryGetValue(lotId, out var lot))
            return MatchStepMedium(step.StepMedia, lot.MaterialId, lot.MediaProductId);
        if (incubation.Media != null)
            return MatchStepMedium(step.StepMedia, incubation.Media.MaterialId, incubation.Media.Material?.MediaProductId);
        return null;
    }

    public static IncubationWindow? ForIncubation(
        TestWorkflowStep step,
        Incubation incubation,
        IReadOnlyDictionary<int, (int MaterialId, int? MediaProductId)>? mediaLookup = null) =>
        TryGet(MediumForIncubation(step, incubation, mediaLookup));

    // ForIncubation when the incubation's lot isn't loaded: one lookup of
    // the lot's material and media product.
    public static async Task<IncubationWindow?> ForIncubationAsync(
        MicroLimsDbContext db, TestWorkflowStep step, Incubation incubation, CancellationToken ct = default)
    {
        if (incubation.MediaId is not int lotId) return null;
        var lot = await db.Media.Where(m => m.Id == lotId)
            .Select(m => new { m.MaterialId, m.Material!.MediaProductId })
            .FirstOrDefaultAsync(ct);
        if (lot is null) return null;
        var lookup = new Dictionary<int, (int MaterialId, int? MediaProductId)> { [lotId] = (lot.MaterialId, lot.MediaProductId) };
        return ForIncubation(step, incubation, lookup);
    }

    // Every configured window among a step's media, in display order.
    public static List<IncubationWindow> ConfiguredWindows(IEnumerable<TestWorkflowStepMedia>? stepMedia) =>
        stepMedia?.OrderBy(m => m.DisplayOrder)
            .Select(m => TryGet(m))
            .OfType<IncubationWindow>()
            .ToList()
        ?? new List<IncubationWindow>();

    // A step's window before a lot is chosen: its one configured medium, or
    // the overall range of several permitted media. Null when none of its
    // media has a configured window. For display and grouping only - a
    // started incubation is always timed by ForIncubation.
    public static IncubationWindow? ForStep(IEnumerable<TestWorkflowStepMedia>? stepMedia)
    {
        var windows = ConfiguredWindows(stepMedia);
        if (windows.Count == 0) return null;
        if (windows.Count == 1) return windows[0];
        return new IncubationWindow(0,
            windows.Min(w => w.MinHours), windows.Max(w => w.MaxHours),
            windows.Min(w => w.TempMin), windows.Max(w => w.TempMax));
    }
}
