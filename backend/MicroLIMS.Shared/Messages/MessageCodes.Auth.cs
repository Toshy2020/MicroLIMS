namespace MicroLIMS.Shared.Messages;

// Auth.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string AuthRoleDoesNotIncludeAction = "Auth.RoleDoesNotIncludeAction";

    private static Dictionary<string, string[]> AuthParameters() => new()
    {
        [AuthRoleDoesNotIncludeAction] = [],
    };
}
