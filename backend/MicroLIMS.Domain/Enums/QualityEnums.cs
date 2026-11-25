namespace MicroLIMS.Domain.Enums;

public enum QualityRecordType
{
    Deviation = 0,
    Capa = 1,
    ChangeControl = 2,
    Complaint = 3
}

public enum QualityDeviationStatus
{
    Raised = 0,
    Investigation = 1,
    LabConcluded = 2,
    Approved = 3,
    Closed = 4,
    Rejected = 5,
    Cancelled = 6
}

public enum CapaStatus
{
    Plan = 0,
    PlanApproved = 1,
    CompletionVerified = 2,
    Closed = 3,
    Cancelled = 4
}

public enum CapaEffectiveness
{
    Effective = 0,
    NotEffective = 1
}

public enum ChangeControlStatus
{
    Proposed = 0,
    Assessed = 1,
    ApprovedForImplementation = 2,
    Closed = 3,
    Rejected = 4,
    Cancelled = 5
}

public enum ComplaintStatus
{
    Received = 0,
    Investigation = 1,
    LabReported = 2,
    Concluded = 3,
    Closed = 4,
    Cancelled = 5
}

public enum ComplaintOutcome
{
    Justified = 0,
    NotJustified = 1,
    Inconclusive = 2
}

public enum QualityActionKind
{
    Corrective = 0,
    Preventive = 1,
    ChangeTask = 2
}

public enum QualityLinkTargetType
{
    Sample = 0,
    TestOrder = 1,
    OosInvestigation = 2,
    MonitoringDeviation = 3,
    Equipment = 4,
    MediaLot = 5,
    StockLot = 6,
    Item = 7,
    Specification = 8,
    TestDefinition = 9,
    DocumentMaster = 10,
    DocumentRevision = 11,
    AuditTrailReviewFinding = 12,
    QualityRecord = 13
}

public enum QualityStepKind
{
    DeviationAssessed = 0,
    DeviationRejected = 1,
    DeviationLabConclusion = 2,
    DeviationApproved = 3,
    DeviationClosed = 4,
    CapaPlanApproved = 5,
    CapaCompletionVerified = 6,
    CapaEffectivenessChecked = 7,
    ChangeImpactAssessed = 8,
    ChangeApproved = 9,
    ChangeRejected = 10,
    ChangeClosed = 11,
    ComplaintLabReport = 12,
    ComplaintConcluded = 13,
    ComplaintClosed = 14,
    QualityCancelled = 15
}

public enum QualityExtensionDecision
{
    Approved = 0,
    Refused = 1
}

public enum QualityChecklistAnswerValue
{
    Yes = 0,
    No = 1,
    NotApplicable = 2
}
