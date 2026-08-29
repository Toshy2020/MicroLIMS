using System.Text.Json.Serialization;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// The Media Evaluation record for one prepared Media lot - auto-created
// by MediaPreparationService.PrepareAsync, EvaluationType derived from
// the lot's MediaType.Class. Completing with Outcome == Conform is the
// only path that sets Media.IsReleasedForUse = true (see
// MediaEvaluationEngine.RecordResultAsync).
public class MediaEvaluation
{
    public int Id { get; set; }
    public int MediaId { get; set; }
    public Media? Media { get; set; }
    public EvaluationType EvaluationType { get; set; }
    public MediaEvaluationStatus Status { get; set; } = MediaEvaluationStatus.Assigned;
    public EvaluationOutcome? Outcome { get; set; } // null until Completed
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int? CompletedByUserId { get; set; }

    public List<MediaEvaluationChallenge> Challenges { get; set; } = new();
}

// One organism challenge within a MediaEvaluation - auto-assigned from a
// matching MediaChallengeSpec row at preparation time. Which result
// fields are meaningful depends on the parent evaluation's EvaluationType
// (and, for IndicationInhibition, this row's ChallengeRole).
public class MediaEvaluationChallenge
{
    public int Id { get; set; }
    public int MediaEvaluationId { get; set; }
    [JsonIgnore]
    public MediaEvaluation? MediaEvaluation { get; set; }

    public int OrganismId { get; set; }
    public Organism? Organism { get; set; }
    public int? CryovialId { get; set; } // analyst-chosen, null until picked
    public Cryovial? Cryovial { get; set; }

    // Alternative organism source to a prepared Cryovial - the raw
    // LyophilizedMicroorganism Material itself, used directly (e.g. to
    // keep passage count minimal for a reference organism). Mutually
    // exclusive with CryovialId - selecting one clears the other (see
    // MediaEvaluationEngine.SelectCryovialAsync/SelectLyophilizedDiskAsync).
    // Unlike Cryovial, a disk Material carries no approval-gate workflow
    // of its own - organism match/expiry/quantity-remaining is the only
    // check.
    public int? LyophilizedDiskId { get; set; }
    public Material? LyophilizedDisk { get; set; }

    public ChallengeRole? ChallengeRole { get; set; }
    public string InitialInoculum { get; set; } = string.Empty;
    public int? IncubationId { get; set; }
    public Incubation? Incubation { get; set; }

    // GrowthPromotion only
    public decimal? OldMediaCount { get; set; }
    public decimal? NewMediaCount { get; set; }
    public decimal? RecoveryPercent { get; set; }

    // Which prior lot OldMediaCount was read from - a real two-state field,
    // not a single FK forced to cover both cases: ReferenceMediaId links to
    // an actual prior Media row when one exists in the system;
    // ReferenceMediaLabel is free text for when it doesn't (first lot of a
    // new material, or historical data predating this field). Exactly one
    // is populated for a recorded GrowthPromotion result. Set once at
    // RecordResultAsync and never re-derived afterward, same snapshot
    // discipline as InitialInoculum above.
    public int? ReferenceMediaId { get; set; }
    public Media? ReferenceMedia { get; set; }
    public string? ReferenceMediaLabel { get; set; }

    // IndicationInhibition / Inhibition role only
    public bool? GrowthObserved { get; set; }

    // IndicationInhibition / Indication role only - ExpectedDescription is
    // copied from the MediaChallengeSpec at assignment time; Conform/
    // NonConform for Indication is a manual analyst judgment (there is no
    // automated string comparison - see MediaEvaluationEngine).
    public string? ObservedDescription { get; set; }
    public string? ExpectedDescription { get; set; }

    // EnrichmentCharacteristics only
    public bool? IsTurbid { get; set; }

    public EvaluationOutcome? Outcome { get; set; }
    public DateTime? ReadAt { get; set; }
    public int? ReadByUserId { get; set; }
}
