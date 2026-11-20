using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Shared.Exceptions;

// Thrown when a frozen business rule or workflow-order rule is violated
// (e.g. approving a test order that has not been reviewed yet).
// ExceptionMiddleware turns it into 400 with the message shown to the user.
//
// Application code throws this, never a plain InvalidOperationException, for
// a rule the user broke (enforced by BusinessRuleThrowTests). That keeps a
// rule violation distinguishable from a framework InvalidOperationException
// such as EF Core's "Sequence contains no elements", which is a bug.
//
// Derives from InvalidOperationException, as NotFoundException does, so the
// code that already catches that type - batch actions reporting a skipped
// item, recorders prefixing a replicate number - keeps catching it. The
// message is nullable for the same reason: it matches the base constructor
// every converted throw site was written against.
//
// A refusal can also carry a message code (MessageCodes) and its parameters
// beside the English sentence; the middleware sends them as
// ApiResponse.ErrorDetails so the interface can translate the sentence.
public class BusinessRuleException : InvalidOperationException, ICodedMessage
{
    public string? Code { get; }

    public IReadOnlyDictionary<string, string>? Parameters { get; }

    public BusinessRuleException(string? message) : base(message) { }

    public BusinessRuleException(string? code, string? englishMessage, IReadOnlyDictionary<string, string>? parameters = null)
        : base(englishMessage)
    {
        Code = code;
        Parameters = parameters;
    }
}
