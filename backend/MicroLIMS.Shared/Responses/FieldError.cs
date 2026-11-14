namespace MicroLIMS.Shared.Responses;

/// <summary>
/// Represents a validation error tied to a specific request field and optional 0-based row index.
/// </summary>
public sealed record FieldError(string Field, string Message, int? Row = null);
