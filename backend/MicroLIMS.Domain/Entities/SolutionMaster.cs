using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Recipe for a mobile phase, diluent or titrant (HPLC chain S2). Edited in
// place with a reason; each preparation stores its own snapshot (D7).
public class SolutionMaster : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public int SectionId { get; set; }
    public DocumentSection? Section { get; set; }
    public string Name { get; set; } = string.Empty;
    public SolutionType Type { get; set; }
    public int ShelfLifeValue { get; set; }
    public ShelfLifeUnit ShelfLifeUnit { get; set; }
    public string StorageCondition { get; set; } = string.Empty;
    public decimal FinalVolumeMl { get; set; }
    public decimal? PhTarget { get; set; }
    public decimal? PhTolerance { get; set; }
    public int? PhAdjustingEntryId { get; set; }
    public MaterialMasterEntry? PhAdjustingEntry { get; set; }
    public string Instructions { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<SolutionComponent> Components { get; set; } = new();

    // Titrant only (USP Volumetric Solutions)
    public decimal? NominalStrength { get; set; }
    public TitrantStrengthUnit? StrengthUnit { get; set; }
    public StandardizationMode? StandardizationMode { get; set; }
    public int? StandardEntryId { get; set; }            // PrimaryStandard: the primary standard
    public MaterialMasterEntry? StandardEntry { get; set; }
    public decimal? EquivalenceMgPerMl { get; set; }     // PrimaryStandard: mg of standard per mL of nominal titrant
    public int? ReferenceSolutionId { get; set; }        // AgainstVolumetricSolution: the reference titrant
    public SolutionMaster? ReferenceSolution { get; set; }
    public bool BlankRequired { get; set; }
    public int? ReplicateCount { get; set; }
    public decimal? FactorMin { get; set; }
    public decimal? FactorMax { get; set; }
    public decimal? MaxRsdPercent { get; set; }
    public int? ValidityDays { get; set; }               // 0 = restandardize before each use

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int LastModifiedByUserId { get; set; }
    public DateTime LastModifiedAt { get; set; }
}
