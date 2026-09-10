namespace MicroLIMS.Domain.Enums;

// Whether the security event represents an action that succeeded or one
// that was refused. Deliberately only two values: a security reviewer
// scanning the trail is asking "did this work or was it blocked", and a
// richer taxonomy belongs in Reason rather than in the outcome column.
public enum SecurityEventOutcome
{
    Success,
    Failure
}
