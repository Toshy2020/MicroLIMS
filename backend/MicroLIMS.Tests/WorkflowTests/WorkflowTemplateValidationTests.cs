using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// The six structural rules a workflow step template must satisfy before
// it can be saved (spec 3.1). Pure function - no database.
public class WorkflowTemplateValidationTests
{
    private static TestWorkflowStep Step(StepType type, int? organismId, params TestWorkflowStepMedia[] media)
    {
        var step = new TestWorkflowStep { StepName = "S", StepType = type, TargetOrganismId = organismId };
        step.StepMedia.AddRange(media);
        return step;
    }

    private static TestWorkflowStepMedia Medium(int materialId, bool isRequired, decimal tempMin = 35, decimal tempMax = 37) =>
        new() { MaterialId = materialId, IsRequired = isRequired, TempMin = tempMin, TempMax = tempMax };

    [Theory]
    [InlineData(StepType.BrothEnrichment)]
    [InlineData(StepType.SelectiveBroth)]
    public void Rule1_BrothStep_WithExactlyOneRequiredMedium_IsValid(StepType type)
    {
        var errors = WorkflowTemplateValidator.Validate(Step(type, null, Medium(1, isRequired: true)));
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(StepType.BrothEnrichment)]
    [InlineData(StepType.SelectiveBroth)]
    public void Rule1_BrothStep_WithTwoMedia_FailsRule1(StepType type)
    {
        var errors = WorkflowTemplateValidator.Validate(Step(type, null, Medium(1, true), Medium(2, true)));
        Assert.Contains(errors, e => e.RuleNumber == 1);
    }

    [Fact]
    public void Rule1_BrothStep_WithOptionalMedium_FailsRule1()
    {
        var errors = WorkflowTemplateValidator.Validate(Step(StepType.BrothEnrichment, null, Medium(1, isRequired: false)));
        Assert.Contains(errors, e => e.RuleNumber == 1);
    }

    [Fact]
    public void Rule2_SelectivePlating_WithOneRequiredMediumAndOrganism_IsValid()
    {
        var errors = WorkflowTemplateValidator.Validate(Step(StepType.SelectivePlating, organismId: 7, Medium(1, true)));
        Assert.Empty(errors);
    }

    [Fact]
    public void Rule2_SelectivePlating_WithoutOrganism_FailsRule2()
    {
        var errors = WorkflowTemplateValidator.Validate(Step(StepType.SelectivePlating, organismId: null, Medium(1, true)));
        Assert.Contains(errors, e => e.RuleNumber == 2);
    }

    [Fact]
    public void Rule3_ConfirmatoryPlating_WithTwoOptionalMediaAndOrganism_IsValid()
    {
        var errors = WorkflowTemplateValidator.Validate(
            Step(StepType.ConfirmatoryPlating, organismId: 7, Medium(1, false), Medium(2, false)));
        Assert.Empty(errors);
    }

    [Fact]
    public void Rule3_ConfirmatoryPlating_WithNoMedia_FailsRule3()
    {
        var errors = WorkflowTemplateValidator.Validate(Step(StepType.ConfirmatoryPlating, organismId: 7));
        Assert.Contains(errors, e => e.RuleNumber == 3);
    }

    [Fact]
    public void Rule3_ConfirmatoryPlating_WithRequiredMedium_FailsRule3()
    {
        var errors = WorkflowTemplateValidator.Validate(
            Step(StepType.ConfirmatoryPlating, organismId: 7, Medium(1, isRequired: true)));
        Assert.Contains(errors, e => e.RuleNumber == 3);
    }

    [Fact]
    public void Rule4_BiochemicalTest_WithNoMediaAndNoOrganism_IsValid()
    {
        var errors = WorkflowTemplateValidator.Validate(Step(StepType.BiochemicalTest, null));
        Assert.Empty(errors);
    }

    [Fact]
    public void Rule4_BiochemicalTest_WithMedium_FailsRule4()
    {
        var errors = WorkflowTemplateValidator.Validate(Step(StepType.BiochemicalTest, null, Medium(1, true)));
        Assert.Contains(errors, e => e.RuleNumber == 4);
    }

    [Fact]
    public void Rule4_BiochemicalTest_WithOrganism_FailsRule4()
    {
        var errors = WorkflowTemplateValidator.Validate(Step(StepType.BiochemicalTest, organismId: 7));
        Assert.Contains(errors, e => e.RuleNumber == 4);
    }

    [Fact]
    public void Rule5_TempMinNotBelowTempMax_FailsRule5()
    {
        var errors = WorkflowTemplateValidator.Validate(
            Step(StepType.BrothEnrichment, null, Medium(1, true, tempMin: 37, tempMax: 35)));
        Assert.Contains(errors, e => e.RuleNumber == 5);
    }

    [Fact]
    public void Rule6_DuplicateMaterial_FailsRule6()
    {
        var errors = WorkflowTemplateValidator.Validate(
            Step(StepType.ConfirmatoryPlating, organismId: 7, Medium(1, false), Medium(1, false)));
        Assert.Contains(errors, e => e.RuleNumber == 6);
    }

    [Fact]
    public void PlateCountStep_IsNotSubjectToPathogenRules()
    {
        var errors = WorkflowTemplateValidator.Validate(Step(StepType.PlateCount, null));
        Assert.Empty(errors);
    }
}
