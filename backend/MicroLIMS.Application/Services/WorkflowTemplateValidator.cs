using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

public record TemplateValidationError(int RuleNumber, string StepName, string Message);

// The structural rules a pathogen workflow step must satisfy before it
// can be saved (spec 3.1). Returns every violation rather than throwing
// on the first, so the Test Master screen can show them all at once.
public static class WorkflowTemplateValidator
{
    public static IReadOnlyList<TemplateValidationError> Validate(TestWorkflowStep step)
    {
        var errors = new List<TemplateValidationError>();
        var media = step.StepMedia;

        void Fail(int rule, string message) => errors.Add(new TemplateValidationError(rule, step.StepName, message));

        switch (step.StepType)
        {
            case StepType.BrothEnrichment:
            case StepType.SelectiveBroth:
                if (media.Count != 1 || !media[0].IsRequired)
                    Fail(1, "A broth step must have exactly one assigned medium, marked as required.");
                if (step.TargetOrganismId is not null)
                    Fail(1, "A broth step must not target an organism.");
                break;

            case StepType.SelectivePlating:
                if (media.Count != 1 || !media[0].IsRequired)
                    Fail(2, "A selective plating step must have exactly one assigned medium, marked as required.");
                if (step.TargetOrganismId is null)
                    Fail(2, "A selective plating step must target an organism.");
                break;

            case StepType.ConfirmatoryPlating:
                if (step.ConfirmatoryMediaCount < 1)
                    Fail(3, "Confirmatory media count must be at least 1.");
                if (media.Count < (step.ConfirmatoryMediaCount < 1 ? 1 : step.ConfirmatoryMediaCount))
                    Fail(3, $"A confirmatory plating step requires at least {step.ConfirmatoryMediaCount} permitted media, all analyst-selectable.");
                if (media.Any(m => m.IsRequired))
                    Fail(3, "A confirmatory plating step must have at least one permitted medium, all analyst-selectable.");
                if (step.TargetOrganismId is null)
                    Fail(3, "A confirmatory plating step must target an organism.");
                break;

            case StepType.BiochemicalTest:
                if (media.Count != 0)
                    Fail(4, "A biochemical test step must have no assigned media.");
                if (step.TargetOrganismId is not null)
                    Fail(4, "A biochemical test step must not target an organism.");
                if (step.MediaTypeId is not null)
                    Fail(4, "A biochemical test step must not have a media type assigned.");
                if (step.PhenotypicTestType is null)
                    Fail(4, "A biochemical test step must specify a phenotypic test type.");
                break;
        }

        if (step.StepType != StepType.BiochemicalTest)
        {
            if (step.MediaTypeId is null)
                Fail(8, "A media type is required for this step type.");
            if (step.PhenotypicTestType is not null)
                Fail(8, "Only a biochemical test step may specify a phenotypic test type.");
        }

        foreach (var medium in media.Where(m => m.TempMin >= m.TempMax))
            Fail(5, $"Medium {medium.MaterialId}: the minimum temperature must be below the maximum.");

        foreach (var duplicate in media.GroupBy(m => m.MaterialId).Where(g => g.Count() > 1))
            Fail(6, $"Medium {duplicate.Key} is assigned to this step more than once.");

        if (step.StepType == StepType.PlateCount && step.RequiresIncubationTransfer)
        {
            var stage2 = step.IncubationStages.FirstOrDefault(s => s.StageNumber == 2);
            if (stage2 is null)
            {
                Fail(7, "A step requiring incubation transfer must define stage 2's temperature and incubation-hours range.");
            }
            else
            {
                if (stage2.TempMin >= stage2.TempMax)
                    Fail(7, "Stage 2's minimum temperature must be below its maximum.");
                if (stage2.IncubationMinHours <= 0 || stage2.IncubationMaxHours < stage2.IncubationMinHours)
                    Fail(7, "Stage 2's incubation-hours range must have a positive minimum and a maximum no less than the minimum.");
            }
        }
        else if (step.IncubationStages.Count > 0)
        {
            Fail(7, "Only a PlateCount step with incubation transfer enabled may define a second incubation stage.");
        }

        return errors;
    }
}
