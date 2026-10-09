using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

// One test's workflow steps as the session shows them. Pure.
public static class AssignedTestStepBuilder
{
    public static List<SessionWorkflowStepDto> Build(
        IReadOnlyList<TestWorkflowStep> steps,
        IReadOnlyList<Incubation> toIncubations,
        IReadOnlyList<WorkflowStepResult> toStepResults,
        bool requiresTsb,
        bool ownTsbComplete,
        IReadOnlyDictionary<int, (int MaterialId, int? MediaProductId)> mediaLookup)
    {
        var stepDtos = new List<SessionWorkflowStepDto>();
        foreach (var s in steps)
        {
            var inc = toIncubations.FirstOrDefault(i => i.StepName == s.StepName || (requiresTsb && s.StepOrder == 1 && i.StepName.Contains("TSB")));
            var res = toStepResults.FirstOrDefault(r => r.StepName == s.StepName);
            var isDone = (inc != null && inc.CompletedAt.HasValue) || res != null || (requiresTsb && s.StepOrder == 1 && ownTsbComplete);
            var outcome = inc?.Outcome ?? (res != null ? "Complete" : (isDone ? "Complete" : null));

            // The medium its incubation actually used; before a lot is
            // chosen, the step media's range - never step-level hours.
            var stepWindow = inc != null
                ? IncubationWindowResolver.ForIncubation(s, inc, mediaLookup)
                : IncubationWindowResolver.ForStep(s.StepMedia);
            stepDtos.Add(new SessionWorkflowStepDto(
                s.StepOrder,
                s.StepName,
                s.StepType.ToString(),
                null,
                s.StepType == StepType.BiochemicalTest ? null : s.StepType.ToString(),
                stepWindow?.MinHours ?? 0,
                stepWindow?.MaxHours ?? 0,
                stepWindow?.TempMin ?? 0,
                stepWindow?.TempMax ?? 0,
                isDone,
                outcome,
                inc?.CompletedAt ?? res?.SubmittedAtUtc));
        }

        return stepDtos;
    }
}
