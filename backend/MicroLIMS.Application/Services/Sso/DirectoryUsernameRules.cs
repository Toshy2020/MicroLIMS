using System.Globalization;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;

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
    public static bool TryNormalize(string? raw, out string normalized, out string error) =>
        TryNormalizeWithCode(raw, out normalized, out _, out error, out _);

    private static bool TryNormalizeWithCode(
        string? raw,
        out string normalized,
        out string code,
        out string error,
        out Dictionary<string, string>? parameters)
    {
        normalized = string.Empty;
        parameters = null;
        var value = User.NormalizeDirectoryUsername(raw);
        if (value is null)
        {
            code = MessageCodes.AuthDirectoryUsernameBlank;
            error = "The company username is blank.";
            return false;
        }
        if (value.Length > MaxLength)
        {
            code = MessageCodes.AuthDirectoryUsernameTooLong;
            error = $"The company username is longer than {MaxLength} characters.";
            parameters = new Dictionary<string, string> { ["maxLength"] = MaxLength.ToString(CultureInfo.InvariantCulture) };
            return false;
        }
        if (value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            code = MessageCodes.AuthDirectoryUsernameInvalidCharacters;
            error = "The company username contains spaces or control characters.";
            return false;
        }

        normalized = value;
        code = string.Empty;
        error = string.Empty;
        return true;
    }

    public static string NormalizeOrThrow(string? raw)
    {
        if (!TryNormalizeWithCode(raw, out var normalized, out var code, out var error, out var parameters))
            throw new BusinessRuleException(code, error, parameters);
        return normalized;
    }
}
