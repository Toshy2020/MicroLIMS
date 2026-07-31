namespace MicroLIMS.Shared.Extensions;

public static class StringExtensions
{
    public static bool IsNullOrBlank(this string? value) => string.IsNullOrWhiteSpace(value);
}
