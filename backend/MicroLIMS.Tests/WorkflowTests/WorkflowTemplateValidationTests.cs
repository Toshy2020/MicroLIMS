using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// The six structural rules a workflow step template must satisfy before
// it can be saved (spec 3.1). Pure function - no database.
public class WorkflowTemplateValidationTests
{
    // MediaTypeId/PhenotypicTestType default to whichever value the new
    // Rule 4/Rule 8 mutual-exclusivity check requires for `type`, so every
    // pre-existing call site here keeps testing only what it was testing
    // (media/organism shape) without tripping the newer rules incidentally.
    private static TestWorkflowStep Step(StepType type, int? organismId, params TestWorkflowStepMedia[] media)
    {
        var step = new TestWorkflowStep
        {
            StepName = "S", StepType = type, TargetOrganismId = organismId,
            MediaTypeId = type == StepType.BiochemicalTest ? (int?)null : 1,
            PhenotypicTestType = type == StepType.BiochemicalTest ? PhenotypicTestType.IdentificationKit : null
        };
        step.StepMedia.AddRange(media);
        return step;
    }

    private static TestWorkflowStep StepWithMediaAndPhenotype(
        StepType type, int? mediaTypeId, PhenotypicTestType? phenotypicTestType,
        int? organismId = null, decimal tempMin = 35, decimal tempMax = 37,
        int incubationMinHours = 0, int incubationMaxHours = 0) =>
        new()
        {
            StepName = "S", StepType = type, TargetOrganismId = organismId,
            MediaTypeId = mediaTypeId, PhenotypicTestType = phenotypicTestType,
            TemperatureMin = tempMin, TemperatureMax = tempMax,
            IncubationMinHours = incubationMinHours, IncubationMaxHours = incubationMaxHours
        };

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

    [Fact]
    public void Rule4_BiochemicalTest_WithPhenotypicTestTypeAndNoMediaType_IsValid()
    {
        var step = StepWithMediaAndPhenotype(StepType.BiochemicalTest, mediaTypeId: null, phenotypicTestType: PhenotypicTestType.Catalase);
        var errors = WorkflowTemplateValidator.Validate(step);
        Assert.Empty(errors);
    }

    [Fact]
    public void Rule4_BiochemicalTest_WithMediaTypeId_FailsRule4()
    {
        var step = StepWithMediaAndPhenotype(StepType.BiochemicalTest, mediaTypeId: 3, phenotypicTestType: PhenotypicTestType.Catalase);
        var errors = WorkflowTemplateValidator.Validate(step);
        Assert.Contains(errors, e => e.RuleNumber == 4);
    }

    [Fact]
    public void Rule4_BiochemicalTest_WithoutPhenotypicTestType_FailsRule4()
    {
        var step = StepWithMediaAndPhenotype(StepType.BiochemicalTest, mediaTypeId: null, phenotypicTestType: null);
        var errors = WorkflowTemplateValidator.Validate(step);
        Assert.Contains(errors, e => e.RuleNumber == 4);
    }

    [Fact]
    public void Rule4_BiochemicalTest_Antibiogram_WithRealIncubationWindow_IsValid()
    {
        // Antibiogram is the one phenotypic type with a real incubation stage
        // (16-18h per SOP) - confirms the validator doesn't reject non-zero
        // Incubation/Temp values on a BiochemicalTest step, since those
        // fields are otherwise unvalidated/inert for this StepType.
        var step = StepWithMediaAndPhenotype(
            StepType.BiochemicalTest, mediaTypeId: null, phenotypicTestType: PhenotypicTestType.Antibiogram,
            tempMin: 35, tempMax: 37, incubationMinHours: 16, incubationMaxHours: 18);
        var errors = WorkflowTemplateValidator.Validate(step);
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(StepType.PlateCount)]
    [InlineData(StepType.BrothEnrichment)]
    [InlineData(StepType.SelectiveBroth)]
    [InlineData(StepType.SelectivePlating)]
    [InlineData(StepType.ConfirmatoryPlating)]
    public void Rule8_NonBiochemical_WithoutMediaTypeId_FailsRule8(StepType type)
    {
        var step = StepWithMediaAndPhenotype(type, mediaTypeId: null, phenotypicTestType: null,
            organismId: type is StepType.SelectivePlating or StepType.ConfirmatoryPlating ? 7 : null);
        var errors = WorkflowTemplateValidator.Validate(step);
        Assert.Contains(errors, e => e.RuleNumber == 8);
    }

    [Fact]
    public void Rule8_NonBiochemical_WithPhenotypicTestType_FailsRule8()
    {
        var step = StepWithMediaAndPhenotype(StepType.PlateCount, mediaTypeId: 3, phenotypicTestType: PhenotypicTestType.Gram);
        var errors = WorkflowTemplateValidator.Validate(step);
        Assert.Contains(errors, e => e.RuleNumber == 8);
    }

    [Fact]
    public void Rule8_NonBiochemical_WithMediaTypeIdAndNoPhenotypicTestType_IsValid()
    {
        var step = StepWithMediaAndPhenotype(StepType.PlateCount, mediaTypeId: 3, phenotypicTestType: null);
        var errors = WorkflowTemplateValidator.Validate(step);
        Assert.Empty(errors);
    }

    private static TestWorkflowStepIncubationStage Stage2(decimal tempMin = 30, decimal tempMax = 35, int minHours = 24, int maxHours = 48) =>
        new() { StageNumber = 2, TempMin = tempMin, TempMax = tempMax, IncubationMinHours = minHours, IncubationMaxHours = maxHours };

    [Fact]
    public void Rule7_TransferEnabledPlateCount_WithStage2Configured_IsValid()
    {
        var step = Step(StepType.PlateCount, null);
        step.RequiresIncubationTransfer = true;
        step.IncubationStages.Add(Stage2());

        var errors = WorkflowTemplateValidator.Validate(step);
        Assert.Empty(errors);
    }

    [Fact]
    public void Rule7_TransferEnabledPlateCount_WithNoStage2_FailsRule7()
    {
        var step = Step(StepType.PlateCount, null);
        step.RequiresIncubationTransfer = true;

        var errors = WorkflowTemplateValidator.Validate(step);
        Assert.Contains(errors, e => e.RuleNumber == 7);
    }

    [Fact]
    public void Rule7_TransferEnabledPlateCount_WithInvertedStage2Temperature_FailsRule7()
    {
        var step = Step(StepType.PlateCount, null);
        step.RequiresIncubationTransfer = true;
        step.IncubationStages.Add(Stage2(tempMin: 40, tempMax: 30));

        var errors = WorkflowTemplateValidator.Validate(step);
        Assert.Contains(errors, e => e.RuleNumber == 7);
    }

    [Fact]
    public void Rule7_TransferEnabledPlateCount_WithZeroMinHours_FailsRule7()
    {
        var step = Step(StepType.PlateCount, null);
        step.RequiresIncubationTransfer = true;
        step.IncubationStages.Add(Stage2(minHours: 0, maxHours: 24));

        var errors = WorkflowTemplateValidator.Validate(step);
        Assert.Contains(errors, e => e.RuleNumber == 7);
    }

    [Fact]
    public void Rule7_NonTransferPlateCount_WithStage2Defined_FailsRule7()
    {
        var step = Step(StepType.PlateCount, null);
        step.RequiresIncubationTransfer = false;
        step.IncubationStages.Add(Stage2());

        var errors = WorkflowTemplateValidator.Validate(step);
        Assert.Contains(errors, e => e.RuleNumber == 7);
    }

    [Fact]
    public void Rule7_NonTransferPlateCount_WithNoStage2_IsValid()
    {
        var step = Step(StepType.PlateCount, null);
        var errors = WorkflowTemplateValidator.Validate(step);
        Assert.Empty(errors);
    }
}
