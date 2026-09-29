using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// One recipe line of a SolutionPreparation, snapshotted from the master's
// SolutionComponent at start time so a later edit to the master does not
// change a preparation already in progress. MaterialId is the stock lot the
// analyst picked; QuantityUsed is entered in the lot's own unit (no unit
// conversion, per the plan's simplification).
public class SolutionPreparationComponent
{
    public int Id { get; set; }
    public int SolutionPreparationId { get; set; }
    public SolutionPreparation? SolutionPreparation { get; set; }
    public int Order { get; set; }
    public int MaterialMasterEntryId { get; set; }
    public string EntryCode { get; set; } = string.Empty;   // snapshot
    public string EntryName { get; set; } = string.Empty;   // snapshot
    public decimal RecipeQuantity { get; set; }
    public SolutionComponentUnit RecipeUnit { get; set; }
    public int? MaterialId { get; set; }                    // chosen lot
    public Material? Material { get; set; }
    public decimal? QuantityUsed { get; set; }              // in the lot's own unit
}
