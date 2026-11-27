using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Services;

// Which exception a refused action throws: a broken rule (BusinessRuleException, 400) or a permission / section-scope /
// segregation refusal the service reports as UnauthorizedAccessException (403).
public enum ActionRefusalKind
{
    Rule,
    Denied
}

// The outcome of an action's pre-condition checks. The action throws it (ThrowIfRefused) and the availability
// endpoint shows it (ActionAvailabilityService), so the sentence a user reads beside a disabled button is the
// sentence the server would refuse with. A check never changes anything.
//
// A refusal may carry a message code (MessageCodes) and its parameters; the thrown exception and the
// availability answer then both carry them, so the interface can show the sentence in the user's language.
// The English Reason stays the same either way.
public sealed record ActionCheck(
    bool Allowed,
    string? Reason,
    ActionRefusalKind Kind = ActionRefusalKind.Rule,
    string? Code = null,
    IReadOnlyDictionary<string, string>? Params = null,
    IReadOnlyList<ApiError>? Details = null)
{
    // Only for an action whose refusal has no sentence today: the endpoint's permission policy answers 403
    // with an empty body.
    public const string RoleDoesNotIncludeAction = "Your role does not include this action.";

    public static ActionCheck Ok { get; } = new(true, null);

    public static ActionCheck No(string reason) => new(false, reason);

    public static ActionCheck No(string code, string reason, IReadOnlyDictionary<string, string>? parameters = null,
        IReadOnlyList<ApiError>? details = null) =>
        new(false, reason, ActionRefusalKind.Rule, code, parameters, details);

    public static ActionCheck Denied(string reason) => new(false, reason, ActionRefusalKind.Denied);

    public static ActionCheck Denied(string code, string reason, IReadOnlyDictionary<string, string>? parameters = null) =>
        new(false, reason, ActionRefusalKind.Denied, code, parameters);

    public void ThrowIfRefused()
    {
        if (Allowed) return;
        if (Kind != ActionRefusalKind.Denied && Details is { Count: > 1 })
            throw BusinessRuleException.FromDetails(Details);
        if (Code is not null && Reason is not null)
        {
            if (Kind == ActionRefusalKind.Denied) throw new CodedAccessDeniedException(Code, Reason, Params);
            throw new BusinessRuleException(Code, Reason, Params);
        }
        if (Kind == ActionRefusalKind.Denied) throw new UnauthorizedAccessException(Reason);
        throw new BusinessRuleException(Code, Reason, Params);
    }

    // A guard that refuses by throwing (the section-scope guards the controllers call), read as a check with the
    // guard's own sentence. A missing record still throws NotFoundException.
    public static async Task<ActionCheck> FromGuardAsync(Func<Task> guard)
    {
        try
        {
            await guard();
            return Ok;
        }
        catch (CodedAccessDeniedException ex)
        {
            return Denied(ex.Code, ex.Message, ex.Parameters);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Denied(ex.Message);
        }
    }
}
