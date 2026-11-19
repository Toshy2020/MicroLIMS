using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Shared.Exceptions;

// A permission, section-scope or segregation-of-duties refusal that carries a
// message code. Derives from UnauthorizedAccessException so every existing
// catch and the middleware's 403 answer apply unchanged.
public class CodedAccessDeniedException : UnauthorizedAccessException, ICodedMessage
{
    public string Code { get; }

    public IReadOnlyDictionary<string, string>? Parameters { get; }

    public CodedAccessDeniedException(string code, string englishMessage, IReadOnlyDictionary<string, string>? parameters = null)
        : base(englishMessage)
    {
        Code = code;
        Parameters = parameters;
    }
}
