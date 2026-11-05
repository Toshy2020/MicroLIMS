using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Test Preparation step - Product/RM/PM/Water only, once per Sample
// (shared across all its TestOrders, since dilution/neutralization
// happens once even though results split across TAMC/TYMC/pathogens).
public class SamplePreparation
{
    public int Id { get; set; }
    public int SampleId { get; set; }
    public Sample? Sample { get; set; }

    public decimal Amount { get; set; }
    // No Unit here - see ItemPreparationConfiguration.

    public string Technique { get; set; } = string.Empty; // "PourPlate" or "Filtration"
    public decimal? FiltrationVolume { get; set; }
    public decimal? WashingVolume { get; set; }

    // Display name - see ItemPreparationConfiguration.
    public string Diluent { get; set; } = string.Empty;
    public string Neutralizer { get; set; } = string.Empty;

    // The lots actually used, picked at confirmation. Which FK is set follows
    // the source snapshot; all null for ContainedInMedium and for preparations
    // confirmed on a legacy (unmapped) configuration or before this existed.
    // Nothing is deducted from the lot: picking it is the record.
    public PreparationMaterialSource? DiluentSource { get; set; }
    public int? DiluentMediaId { get; set; }
    public Media? DiluentMedia { get; set; }
    public int? DiluentSolutionPreparationId { get; set; }
    public SolutionPreparation? DiluentSolutionPreparation { get; set; }
    public int? DiluentMaterialId { get; set; }
    public Material? DiluentMaterial { get; set; }
    public string? DiluentLotCode { get; set; }           // snapshot: Media.LotNumber, SolutionPreparation.Code or Material.StockId

    public PreparationMaterialSource? NeutralizerSource { get; set; }
    public int? NeutralizerMediaId { get; set; }
    public Media? NeutralizerMedia { get; set; }
    public int? NeutralizerSolutionPreparationId { get; set; }
    public SolutionPreparation? NeutralizerSolutionPreparation { get; set; }
    public int? NeutralizerMaterialId { get; set; }
    public Material? NeutralizerMaterial { get; set; }
    public string? NeutralizerLotCode { get; set; }

    public int PreparedByUserId { get; set; }
    public DateTime PreparedAt { get; set; } = DateTime.UtcNow;

    // The values above are the snapshot: copied from the Item's configuration
    // at confirmation time so editing that config never rewrites history.
    // Null SourceConfigurationId means this row predates the config feature.
    public int? SourceConfigurationId { get; set; }
    public ItemPreparationConfiguration? SourceConfiguration { get; set; }
    public bool WasConfirmedFromConfig { get; set; }
}
