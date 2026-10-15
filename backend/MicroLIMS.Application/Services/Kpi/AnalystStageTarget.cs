namespace MicroLIMS.Application.Services.Kpi;

public sealed record AnalystStageTargetResult(decimal Days, int TestsOnDefault, int TestsTotal);

// Spec 4.3: the analyst-stage deadline is AssignedAt + the LONGEST target of
// the sample's assigned tests. A test with no resolvable target falls back to
// the old 7-day constant and is counted so the tile can show a gap chip.
// `at` is the approval time (D4): a profile added later never moves it.
public static class AnalystStageTarget
{
    public static AnalystStageTargetResult DaysFor(IEnumerable<string> testCodes, DateTime at, WorkflowProfileResolver resolver)
    {
        decimal max = 0m;
        int onDefault = 0, total = 0;
        foreach (var code in testCodes)
        {
            total++;
            var target = resolver.Resolve(code, at).TargetAnalystDays;
            if (target is null) onDefault++;
            max = Math.Max(max, target ?? KpiConstants.DefaultTargetDays);
        }
        return new AnalystStageTargetResult(total == 0 ? KpiConstants.DefaultTargetDays : max, onDefault, total);
    }
}
