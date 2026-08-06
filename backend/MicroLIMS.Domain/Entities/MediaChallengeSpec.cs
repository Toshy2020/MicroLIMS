using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Section Head master data: for a given dehydrated media product
// (MaterialName) + evaluation type, which organism(s) it must be
// challenged with and what the expected outcome looks like.
// MediaPreparationService.PrepareAsync matches on MaterialName +
// EvaluationType to auto-assign one MediaEvaluationChallenge per spec
// row when a lot is prepared. ChallengeRole/ExpectedDescription are
// only meaningful when EvaluationType is IndicationInhibition.
public class MediaChallengeSpec
{
    public int Id { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public EvaluationType EvaluationType { get; set; }
    public int OrganismId { get; set; }
    public Organism? Organism { get; set; }
    public ChallengeRole? ChallengeRole { get; set; }
    public string? ExpectedDescription { get; set; }
}
