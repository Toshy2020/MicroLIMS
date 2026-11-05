using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Ordered recipe line of a SolutionMaster. Kind Material references a
// Material master entry (no free text) with a quantity/ratio and unit; kind
// PreparedSolution references another recipe whose prepared bottle is picked
// at preparation; kind Text is a printed instruction only.
public class SolutionComponent
{
    public int Id { get; set; }
    public int SolutionMasterId { get; set; }
    public SolutionMaster? SolutionMaster { get; set; }
    public int Order { get; set; }
    public SolutionComponentKind Kind { get; set; } = SolutionComponentKind.Material;
    public int? MaterialMasterEntryId { get; set; }       // Material only
    public MaterialMasterEntry? MaterialMasterEntry { get; set; }
    public int? SourceSolutionMasterId { get; set; }      // PreparedSolution only
    public SolutionMaster? SourceSolutionMaster { get; set; }
    public string? Text { get; set; }                     // Text only
    public decimal? Quantity { get; set; }                // null for Text
    public SolutionComponentUnit? Unit { get; set; }      // null for Text
}
