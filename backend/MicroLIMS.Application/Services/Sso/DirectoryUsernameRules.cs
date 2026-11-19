using MicroLIMS.Domain.Entities;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.Sso;

/// <summary>
/// Shape rules for a company (directory) username, shared by the user form and the link import.
/// </summary>
public static class DirectoryUsernameRules
{
    // Matches the column length (UserConfiguration: DirectoryUsername HasMaxLength(256)).
    public const int MaxLength = 256;

    /// <summary>
    /// Normalises a company username. Returns false with a plain-English reason when it is blank,
    /// too long, or contains spaces or control characters.
    /// </summary>
    public static bool TryNormalize(string? raw, out string normalized, out string error)
    {
        normalized = string.Empty;
        var value = User.NormalizeDirectoryUsername(raw);
        if (value is null)
        {
            error = "The company username is blank.";
            return false;
        }
        if (value.Length > MaxLength)
        {
            error = $"The company username is longer than {MaxLength} characters.";
            return false;
        }
        if (value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            error = "The company username contains spaces or control characters.";
            return false;
        }

        normalized = value;
        error = string.Empty;
        return true;
    }

    public static string NormalizeOrThrow(string? raw)
    {
        if (!TryNormalize(raw, out var normalized, out var error))
            throw new BusinessRuleException(error);
        return normalized;
    }
}
