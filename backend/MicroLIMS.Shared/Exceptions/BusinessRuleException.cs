namespace MicroLIMS.Shared.Exceptions;

// Thrown when a frozen business rule or workflow-order rule is violated
// (e.g. approving a test order that has not been reviewed yet).
public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message) { }
}
