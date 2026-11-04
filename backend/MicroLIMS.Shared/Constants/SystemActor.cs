namespace MicroLIMS.Shared.Constants;

// How a record that was changed automatically (no user behind it) is shown.
// Such records store a null user id; they must never be attributed to a
// guessed user.
public static class SystemActor
{
    public const string DisplayName = "System (automatic)";
}
