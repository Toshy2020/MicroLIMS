using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Phase 1 of the MediaType/MediaChallengeSpec retirement (see the
// Media Configuration Migration plan). One row per configured usage of a
// dehydrated media product - Name is deliberately NOT unique, because the
// same product can be used under more than one incubation/temperature
// profile (e.g. Tryptic Soy Agar's "Standard" 1-2h use vs. its
// "Extended Transfer" 24-72h use). No separate disambiguating label - the
// row's own Incubation/Temperature fields already distinguish it from any
// other row sharing its Name (enforced by the unique index on all five
// together), so anything displaying these rows formats them from that
// data directly rather than maintaining a redundant free-text field.
//
// No Class field either. MediaType.Class only ever existed to derive
// EvaluationType (see the switch in MediaPreparationService.cs) and to
// distinguish broth-vs-agar physical form for a handful of unrelated
// broth-enrichment-detection call sites (TestingWorkspaceService,
// PathogenSessionService, SampleSummaryService) - those read the OLD
// MediaType.Class via TestWorkflowStep.MediaTypeId, a separate object
// graph this migration doesn't touch until Phase 4d, and are already
// slated to move to StepType independently of this migration (see the
// original planning prompt's context on SampleSummaryService). Since
// EvaluationType is now captured directly per row instead of derived,
// Class has no remaining purpose here. Not yet read or written by any
// other code - additive only.
public class MediaConfiguration
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public EvaluationType EvaluationType { get; set; }

    public int IncubationMinHours { get; set; }
    public int IncubationMaxHours { get; set; }
    public decimal TemperatureMin { get; set; }
    public decimal TemperatureMax { get; set; }

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
