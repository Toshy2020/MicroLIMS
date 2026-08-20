namespace MicroLIMS.Domain.Enums;

// Classifies a BiochemicalTest step's phenotypic confirmation kind. Null
// for every non-BiochemicalTest StepType - see WorkflowTemplateValidator
// rules 4 and 8 for the mutual-exclusivity enforcement against MediaTypeId.
public enum PhenotypicTestType
{
    Gram,
    Catalase,
    Oxidase,
    Coagulase,
    Antibiogram,
    IdentificationKit
}
