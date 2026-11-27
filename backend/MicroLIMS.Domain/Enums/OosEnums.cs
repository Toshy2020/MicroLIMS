namespace MicroLIMS.Domain.Enums;

public enum OosInvestigationKind
{
    Oos,
    Excursion
}

public enum OosInvestigationStatus
{
    PhaseOne,
    AwaitingRetest,
    PhaseTwo,
    Concluded
}

public enum OosPhaseOneAssessment
{
    LaboratoryErrorConfirmed,
    NoLaboratoryErrorFound
}

public enum OosConclusion
{
    Confirmed,
    Invalidated,
    ExcursionClosed
}

public enum OosEntryKind
{
    AnalystStatement,
    ChecklistAnswer,
    OutsideReview,
    Hypothesis,
    HypothesisOutcome,
    Note
}

public enum OosChecklistAnswer
{
    Yes,
    No,
    NotApplicable
}

public enum OosRetestSource
{
    RetainedSample,
    NewSample
}

public enum OosDeviationRequestStatus
{
    Open,
    Addressed
}

public enum OosOutcomeClass
{
    Conforms,
    Fails,
    AlertLimitExceeded,
    ActionLimitExceeded
}
