using System.Text;

namespace MicroLIMS.Application.Labels;

public readonly record struct NormalizedScan(string Value, bool LayoutCorrected);

public static class ScanInputNormalizer
{
    private const char Lam = '\u0644';
    private const char Alef = '\u0627';

    // Maps characters typed under the Windows "Arabic (101)" keyboard layout back to the
    // corresponding US-Latin key.
    // Source: Microsoft "Arabic (101)" keyboard layout reference (kbda1.dll, KLID 00000401).
    // Note: '-' (VK_OEM_MINUS, unshifted) and ':' (VK_OEM_1, shifted) already emit ASCII '-'
    // and ':' on kbda1.dll and therefore pass through unchanged without setting LayoutCorrected.
    // The two-character sequence "\u0644\u0627" (lam + alef, Latin 'b') is handled before this map.
    private static readonly Dictionary<char, char> ArabicToLatin = new()
    {
        // Top letter row (Q..P)
        ['\u0636'] = 'q', // ض
        ['\u0635'] = 'w', // ص
        ['\u062B'] = 'e', // ث
        ['\u0642'] = 'r', // ق
        ['\u0641'] = 't', // ف
        ['\u063A'] = 'y', // غ
        ['\u0639'] = 'u', // ع
        ['\u0647'] = 'i', // ه
        ['\u062E'] = 'o', // خ
        ['\u062D'] = 'p', // ح

        // Home letter row (A..L)
        ['\u0634'] = 'a', // ش
        ['\u0633'] = 's', // س
        ['\u064A'] = 'd', // ي
        ['\u0628'] = 'f', // ب
        ['\u0644'] = 'g', // ل
        ['\u0627'] = 'h', // ا
        ['\u062A'] = 'j', // ت
        ['\u0646'] = 'k', // ن
        ['\u0645'] = 'l', // م

        // Bottom letter row (Z..M; 'b' is handled as the 2-char lam+alef sequence above)
        ['\u0626'] = 'z', // ئ
        ['\u0621'] = 'x', // ء
        ['\u0624'] = 'c', // ؤ
        ['\u0631'] = 'v', // ر
        ['\u0649'] = 'n', // ى
        ['\u0629'] = 'm', // ة

        // Punctuation and remaining Arabic letter keys on kbda1.dll
        ['\u062C'] = '[',  // ج
        ['\u062F'] = ']',  // د
        ['\u0643'] = ';',  // ك
        ['\u0637'] = '\'', // ط
        ['\u0648'] = ',',  // و
        ['\u0632'] = '.',  // ز
        ['\u0638'] = '/',  // ظ
        ['\u0630'] = '`',  // ذ

        // Arabic-Indic digits (U+0660..U+0669)
        ['\u0660'] = '0', // ٠
        ['\u0661'] = '1', // ١
        ['\u0662'] = '2', // ٢
        ['\u0663'] = '3', // ٣
        ['\u0664'] = '4', // ٤
        ['\u0665'] = '5', // ٥
        ['\u0666'] = '6', // ٦
        ['\u0667'] = '7', // ٧
        ['\u0668'] = '8', // ٨
        ['\u0669'] = '9'  // ٩
    };

    public static NormalizedScan Normalize(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return new NormalizedScan(string.Empty, false);
        }

        var trimmed = TrimEdges(raw);
        if (trimmed.Length == 0)
        {
            return new NormalizedScan(string.Empty, false);
        }

        var builder = new StringBuilder(trimmed.Length);
        var corrected = false;

        for (var i = 0; i < trimmed.Length; i++)
        {
            if (i + 1 < trimmed.Length && trimmed[i] == Lam && trimmed[i + 1] == Alef)
            {
                builder.Append('b');
                corrected = true;
                i++;
                continue;
            }

            var current = trimmed[i];
            if (ArabicToLatin.TryGetValue(current, out var latin))
            {
                builder.Append(latin);
                corrected = true;
            }
            else
            {
                builder.Append(current);
            }
        }

        return new NormalizedScan(builder.ToString(), corrected);
    }

    private static ReadOnlySpan<char> TrimEdges(string value)
    {
        var start = 0;
        while (start < value.Length && IsEdgeTrimChar(value[start]))
        {
            start++;
        }

        var end = value.Length - 1;
        while (end >= start && IsEdgeTrimChar(value[end]))
        {
            end--;
        }

        return start > end ? ReadOnlySpan<char>.Empty : value.AsSpan(start, end - start + 1);
    }

    private static bool IsEdgeTrimChar(char c) => char.IsWhiteSpace(c) || char.IsControl(c);
}
