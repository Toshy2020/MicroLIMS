namespace MicroLIMS.Shared.Messages;

// TestDefinition.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string TestDefinitionCodeAlreadyExists = "TestDefinition.CodeAlreadyExists";
    public const string TestDefinitionCodeRequired = "TestDefinition.CodeRequired";
    public const string TestDefinitionConditionFieldsTooLong = "TestDefinition.ConditionFieldsTooLong";
    public const string TestDefinitionDisintegrationNoSystemSuitability = "TestDefinition.DisintegrationNoSystemSuitability";
    public const string TestDefinitionDisplayNameRequired = "TestDefinition.DisplayNameRequired";
    public const string TestDefinitionDissolutionRequiresSystemSuitability = "TestDefinition.DissolutionRequiresSystemSuitability";
    public const string TestDefinitionElementalAssayRetired = "TestDefinition.ElementalAssayRetired";
    public const string TestDefinitionEquationMustBeDisintegration = "TestDefinition.EquationMustBeDisintegration";
    public const string TestDefinitionEquationMustBeDissolution = "TestDefinition.EquationMustBeDissolution";
    public const string TestDefinitionEquationMustBeGravimetric = "TestDefinition.EquationMustBeGravimetric";
    public const string TestDefinitionEquationMustBeMeasurement = "TestDefinition.EquationMustBeMeasurement";
    public const string TestDefinitionEquationMustBeQualitative = "TestDefinition.EquationMustBeQualitative";
    public const string TestDefinitionEquationMustBeUvSpectrophotometry = "TestDefinition.EquationMustBeUvSpectrophotometry";
    public const string TestDefinitionEquationMustBeWeightVariation = "TestDefinition.EquationMustBeWeightVariation";
    public const string TestDefinitionLegacyHplcRetired = "TestDefinition.LegacyHplcRetired";
    public const string TestDefinitionMeasurementEvaluationBasisRequired = "TestDefinition.MeasurementEvaluationBasisRequired";
    public const string TestDefinitionMeasurementReplicateCountRange = "TestDefinition.MeasurementReplicateCountRange";
    public const string TestDefinitionMethodAbbreviationInvalid = "TestDefinition.MethodAbbreviationInvalid";
    public const string TestDefinitionMethodAbbreviationRequired = "TestDefinition.MethodAbbreviationRequired";
    public const string TestDefinitionReplicateCountRangeForEquation = "TestDefinition.ReplicateCountRangeForEquation";
    public const string TestDefinitionUnknownUvCalculationMode = "TestDefinition.UnknownUvCalculationMode";
    public const string TestDefinitionUvFieldsOnlyForUv = "TestDefinition.UvFieldsOnlyForUv";
    public const string TestDefinitionWeightVariationNoSystemSuitability = "TestDefinition.WeightVariationNoSystemSuitability";
    public const string TestDefinitionWorkflowMustBeDisintegration = "TestDefinition.WorkflowMustBeDisintegration";
    public const string TestDefinitionWorkflowMustBeDissolution = "TestDefinition.WorkflowMustBeDissolution";
    public const string TestDefinitionWorkflowMustBeGravimetric = "TestDefinition.WorkflowMustBeGravimetric";
    public const string TestDefinitionWorkflowMustBeMeasurement = "TestDefinition.WorkflowMustBeMeasurement";
    public const string TestDefinitionWorkflowMustBeQualitative = "TestDefinition.WorkflowMustBeQualitative";
    public const string TestDefinitionWorkflowMustBeUvSpectrophotometry = "TestDefinition.WorkflowMustBeUvSpectrophotometry";
    public const string TestDefinitionWorkflowMustBeWeightVariation = "TestDefinition.WorkflowMustBeWeightVariation";

    private static Dictionary<string, string[]> TestDefinitionParameters() => new()
    {
        [TestDefinitionCodeAlreadyExists] = ["code"],
        [TestDefinitionCodeRequired] = [],
        [TestDefinitionConditionFieldsTooLong] = [],
        [TestDefinitionDisintegrationNoSystemSuitability] = [],
        [TestDefinitionDisplayNameRequired] = [],
        [TestDefinitionDissolutionRequiresSystemSuitability] = [],
        [TestDefinitionElementalAssayRetired] = [],
        [TestDefinitionEquationMustBeDisintegration] = [],
        [TestDefinitionEquationMustBeDissolution] = [],
        [TestDefinitionEquationMustBeGravimetric] = [],
        [TestDefinitionEquationMustBeMeasurement] = [],
        [TestDefinitionEquationMustBeQualitative] = [],
        [TestDefinitionEquationMustBeUvSpectrophotometry] = [],
        [TestDefinitionEquationMustBeWeightVariation] = [],
        [TestDefinitionLegacyHplcRetired] = [],
        [TestDefinitionMeasurementEvaluationBasisRequired] = [],
        [TestDefinitionMeasurementReplicateCountRange] = [],
        [TestDefinitionMethodAbbreviationInvalid] = [],
        [TestDefinitionMethodAbbreviationRequired] = [],
        [TestDefinitionReplicateCountRangeForEquation] = ["equationType"],
        [TestDefinitionUnknownUvCalculationMode] = [],
        [TestDefinitionUvFieldsOnlyForUv] = [],
        [TestDefinitionWeightVariationNoSystemSuitability] = [],
        [TestDefinitionWorkflowMustBeDisintegration] = [],
        [TestDefinitionWorkflowMustBeDissolution] = [],
        [TestDefinitionWorkflowMustBeGravimetric] = ["equationType"],
        [TestDefinitionWorkflowMustBeMeasurement] = [],
        [TestDefinitionWorkflowMustBeQualitative] = [],
        [TestDefinitionWorkflowMustBeUvSpectrophotometry] = [],
        [TestDefinitionWorkflowMustBeWeightVariation] = [],
    };
}
