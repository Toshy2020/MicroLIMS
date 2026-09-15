using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Evaluation configuration for a dehydrated media product (MediaProduct).
// There is exactly one configuration per product (unique on MediaProductId), defining
// its evaluation type, recovery percentages (for GrowthPromotion), and challenge organisms.
// Time and temperature are not stored directly on this row; instead, the configuration
// links to one chosen MediaIncubationCondition belonging to the same product.
public class MediaConfiguration
{
    public int Id { get; set; }

    public int MediaProductId { get; set; }
    public MediaProduct? MediaProduct { get; set; }

    // Display copy of MediaProduct.Name kept in sync by the Application layer - never match on it.
    public string Name { get; set; } = string.Empty;

    public EvaluationType EvaluationType { get; set; }

    public int MediaIncubationConditionId { get; set; }
    public MediaIncubationCondition? IncubationCondition { get; set; }

    // GrowthPromotion only - null for other EvaluationTypes, mirroring
    // how MediaType.RecoveryPercentMin/Max is used today.
    public decimal? RecoveryPercentMin { get; set; }
    public decimal? RecoveryPercentMax { get; set; }

    public List<MediaConfigurationChallenge> Challenges { get; set; } = new();
}

// One organism challenge within a MediaConfiguration - the Phase 1
// replacement shape for MediaChallengeSpec, FK'd to the parent instead of
// matched by MaterialName+EvaluationType string lookup. ChallengeRole/
// ExpectedDescription are only meaningful when the parent's EvaluationType
// is IndicationInhibition.
public class MediaConfigurationChallenge
{
    public int Id { get; set; }

    public int MediaConfigurationId { get; set; }
    public MediaConfiguration? MediaConfiguration { get; set; }

    public int OrganismId { get; set; }
    public Organism? Organism { get; set; }

    public ChallengeRole? ChallengeRole { get; set; }
    public string? ExpectedDescription { get; set; }

    // Free-text target inoculum for this organism on this media (e.g.
    // "10^2", "<=100", ">=1000") - shown on every row regardless of
    // EvaluationType/ChallengeRole, unlike ExpectedDescription. Snapshotted
    // onto MediaEvaluationChallenge.InitialInoculum at prep time (see
    // MediaPreparationService), the same pattern already used for
    // ChallengeRole/ExpectedDescription above.
    public string? InitialInoculum { get; set; }
}
