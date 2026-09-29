using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Ordered recipe line of a SolutionMaster. References a Material master
// entry (no free text) with a quantity/ratio and unit.
public class SolutionComponent
{
    public int Id { get; set; }
    public int SolutionMasterId { get; set; }
    public SolutionMaster? SolutionMaster { get; set; }
    public int Order { get; set; }
    public int MaterialMasterEntryId { get; set; }
    public MaterialMasterEntry? MaterialMasterEntry { get; set; }
    public decimal Quantity { get; set; }
    public SolutionComponentUnit Unit { get; set; }
}
