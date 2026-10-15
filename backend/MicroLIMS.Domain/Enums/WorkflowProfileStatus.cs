namespace MicroLIMS.Domain.Enums;

// Stored as int. A Draft has no effect on any calculation until it is signed.
public enum WorkflowProfileStatus
{
    Draft = 0,
    Effective = 1
}
