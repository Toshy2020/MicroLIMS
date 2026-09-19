namespace MicroLIMS.Domain.Enums;

public enum LimitType
{
    Range = 0,
    NotMoreThan = 1,
    NotLessThan = 2,
    TargetWithTolerance = 3,
    CountTiered = 4,
    Qualitative = 5,
    PresenceAbsence = 6,
    MultiStage = 7,
    DissolutionQ = 8
}
