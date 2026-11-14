namespace MicroLIMS.Domain.Enums;

public enum EscalationTrigger
{
    ReviewWaiting,
    ApprovalWaiting,
    ReturnedNotCorrected,
    IncubationNotRead,
    TestingNotStarted,
    DocumentTaskOverdue
}

public enum EmailMode
{
    Off,
    Immediate,
    Digest
}

public enum EmailOutboxKind
{
    Immediate,
    Digest,
    System
}
