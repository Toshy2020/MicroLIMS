namespace MicroLIMS.Domain.Enums;

// Only meaningful when EvaluationType is IndicationInhibition - each
// organism challenged under that type plays one of these two roles.
public enum ChallengeRole
{
    Inhibition,
    Indication
}
