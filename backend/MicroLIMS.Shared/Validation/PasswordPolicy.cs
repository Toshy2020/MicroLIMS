namespace MicroLIMS.Shared.Validation;

// GMP password policy: minimum length + character-class mix. Returns
// every failure (not just the first) so the UI can show the user
// exactly what's still missing.
public static class PasswordPolicy
{
    public const int MinimumLength = 8;

    // minimumLength comes from the settings registry (security.password.minimumLength);
    // the default is the built-in value for callers with no registry.
    public static List<string> Validate(string? password, int minimumLength = MinimumLength)
    {
        var failures = new List<string>();
        password ??= string.Empty;

        if (password.Length < minimumLength)
            failures.Add($"Password must be at least {minimumLength} characters long.");
        if (!password.Any(char.IsUpper))
            failures.Add("Password must contain at least one uppercase letter.");
        if (!password.Any(char.IsLower))
            failures.Add("Password must contain at least one lowercase letter.");
        if (!password.Any(char.IsDigit))
            failures.Add("Password must contain at least one digit.");
        if (!password.Any(c => !char.IsLetterOrDigit(c)))
            failures.Add("Password must contain at least one special (non-alphanumeric) character.");

        return failures;
    }

    public static bool IsValid(string? password) => Validate(password).Count == 0;
}
