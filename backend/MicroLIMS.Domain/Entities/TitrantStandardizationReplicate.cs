namespace MicroLIMS.Domain.Entities;

// One replicate titration within a TitrantStandardization. Either the
// PrimaryStandard fields (StandardMaterialId/StandardWeightMg) or the
// AgainstVolumetricSolution fields (ReferencePreparationId/ReferenceVolumeMl)
// are populated, per the parent's Mode - never both.
public class TitrantStandardizationReplicate
{
    public int Id { get; set; }
    public int TitrantStandardizationId { get; set; }
    public TitrantStandardization? TitrantStandardization { get; set; }
    public int ReplicateNo { get; set; }
    public int? StandardMaterialId { get; set; }          // PrimaryStandard: lot
    public Material? StandardMaterial { get; set; }
    public decimal? StandardWeightMg { get; set; }
    public decimal? StandardPurityPercent { get; set; }   // snapshot from the lot
    public int? ReferencePreparationId { get; set; }      // AgainstVolumetricSolution
    public SolutionPreparation? ReferencePreparation { get; set; }
    public decimal? ReferenceVolumeMl { get; set; }
    public decimal? ReferenceFactor { get; set; }         // snapshot of the reference's current factor
    public decimal TitrantVolumeMl { get; set; }
    public decimal? BlankMl { get; set; }
    public decimal Factor { get; set; }
}
