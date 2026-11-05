using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// An optional additive (e.g. a neutralizer) added to a culture medium while
// it is prepared. A StockLot additive is deducted from the lot's stock; a
// PreparedSolution additive is only recorded - picking the bottle is enough.
// Name and Code are snapshots so the lot record stays readable if the source changes.
public class MediaAdditive
{
    public int Id { get; set; }
    public int MediaId { get; set; }
    public Media? Media { get; set; }
    public int Order { get; set; }
    public MediaAdditiveKind Kind { get; set; }

    public int? MaterialId { get; set; }                  // StockLot only
    public Material? Material { get; set; }
    public int? SolutionPreparationId { get; set; }       // PreparedSolution only
    public SolutionPreparation? SolutionPreparation { get; set; }

    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;      // free text, e.g. "mL", "g"
    public string Name { get; set; } = string.Empty;      // snapshot
    public string Code { get; set; } = string.Empty;      // snapshot: lot code / stock ID, or the preparation code
}
