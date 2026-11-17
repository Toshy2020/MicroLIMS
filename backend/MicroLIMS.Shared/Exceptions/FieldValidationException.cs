using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Shared.Exceptions;

/// <summary>
/// Thrown when validation fails with structured field-level (and optional row-level) errors.
/// </summary>
public class FieldValidationException : BusinessRuleException
{
    public IReadOnlyList<FieldError> FieldErrors { get; }

    public FieldValidationException(string message, IReadOnlyList<FieldError> fieldErrors)
        : base(message)
    {
        FieldErrors = fieldErrors ?? Array.Empty<FieldError>();
    }

    public FieldValidationException(
        string code,
        string message,
        IReadOnlyList<FieldError> fieldErrors,
        IReadOnlyDictionary<string, string>? parameters = null)
        : base(code, message, parameters)
    {
        FieldErrors = fieldErrors ?? Array.Empty<FieldError>();
    }
}
