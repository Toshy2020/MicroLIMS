using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Section Head managed list of Production Stage values (e.g. B, IP,
// F.P) offered on the Finished Product receiving form. Sample.
// ProductionStage stores the chosen value as plain text, not an FK -
// removing a stage here never affects historical records.
//
// Role is the fixed, code-meaningful classification of this row - code
// keys on Role, never on Name. Name/IsActive stay freely renameable by
// the Section Head; Role is what FP Standard-Comparison Assay (and
// anything else that needs to know "is this sample's stage Bulk /
// In-Process / Finished / Stability") actually reads.
public class ProductionStage
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ProductionStageRole Role { get; set; } = ProductionStageRole.Other;
}
