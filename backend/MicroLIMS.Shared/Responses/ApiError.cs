using System.Text.Json.Serialization;

namespace MicroLIMS.Shared.Responses;

/// <summary>
/// A refusal with its message code: the interface translates Code (with Params) into the user's language and
/// shows Message, the English sentence, when it has no template for the code.
/// </summary>
public sealed record ApiError(
    string Code,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, string>? Params,
    string Message);
