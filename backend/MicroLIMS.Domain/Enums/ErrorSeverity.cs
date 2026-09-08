namespace MicroLIMS.Domain.Enums;

// Technical severity of a captured ErrorLog entry. Ordered lowest to
// highest so Incident.Severity can be rolled up as the max of its
// children - do not renumber.
public enum ErrorSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2,
    Critical = 3
}
