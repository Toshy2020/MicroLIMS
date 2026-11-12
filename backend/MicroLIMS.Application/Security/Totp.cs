using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MicroLIMS.Application.Security;

/// <summary>
/// RFC 6238 TOTP (over RFC 4226 HOTP with HMAC-SHA1, 6 digits) and RFC 4648 Base32 helpers.
/// </summary>
public static class Totp
{
    private const int Digits = 6;
    private const int Modulo = 1_000_000;
    private const int SecretLengthBytes = 20;
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static long StepAt(DateTime utc, int stepSeconds)
    {
        ValidateStepSeconds(stepSeconds);

        if (utc.Kind == DateTimeKind.Local)
            throw new ArgumentException("Timestamp must be UTC or Unspecified, not Local.", nameof(utc));

        // Treat DateTimeKind.Unspecified as UTC.
        long unixSeconds = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();
        long quotient = unixSeconds / stepSeconds;
        long remainder = unixSeconds % stepSeconds;
        if (remainder < 0)
            quotient--;

        return quotient;
    }

    public static string Generate(ReadOnlySpan<byte> secret, long step)
    {
        Span<byte> digits = stackalloc byte[Digits];
        WriteHotpDigits(secret, step, digits);
        return Encoding.ASCII.GetString(digits);
    }

    // Returns the matched step, or null. Rejects any step <= lastAcceptedStep.
    public static long? Verify(ReadOnlySpan<byte> secret, string? code, DateTime nowUtc, int stepSeconds, long? lastAcceptedStep)
    {
        long currentStep = StepAt(nowUtc, stepSeconds);

        if (string.IsNullOrEmpty(code) || code.Length != Digits)
            return null;

        Span<byte> codeBytes = stackalloc byte[Digits];
        for (int i = 0; i < Digits; i++)
        {
            char ch = code[i];
            if (!char.IsAsciiDigit(ch))
                return null;

            codeBytes[i] = (byte)ch;
        }

        Span<byte> candidateBytes = stackalloc byte[Digits];
        long? matchedStep = null;

        // Evaluate all three window steps without early exit between candidates.
        for (int delta = -1; delta <= 1; delta++)
        {
            long candidateStep = currentStep + delta;
            WriteHotpDigits(secret, candidateStep, candidateBytes);
            if (CryptographicOperations.FixedTimeEquals(candidateBytes, codeBytes))
                matchedStep = candidateStep;
        }

        if (matchedStep is null || (lastAcceptedStep.HasValue && matchedStep.Value <= lastAcceptedStep.Value))
            return null;

        return matchedStep;
    }

    public static string ToBase32(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
            return string.Empty;

        int outputLength = (bytes.Length * 8 + 4) / 5;
        char[] chars = new char[outputLength];
        int buffer = 0;
        int bitsLeft = 0;
        int index = 0;

        foreach (byte b in bytes)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                chars[index++] = Base32Alphabet[(buffer >> bitsLeft) & 0x1F];
            }
        }

        if (bitsLeft > 0)
            chars[index++] = Base32Alphabet[(buffer << (5 - bitsLeft)) & 0x1F];

        return new string(chars, 0, index);
    }

    public static byte[] FromBase32(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        int symbolCount = 0;
        bool seenPadding = false;

        foreach (char ch in text)
        {
            if (char.IsWhiteSpace(ch))
                continue;

            if (ch == '=')
            {
                seenPadding = true;
                continue;
            }

            if (seenPadding || DecodeBase32Char(ch) < 0)
                throw new FormatException("Invalid Base32 input.");

            symbolCount++;
        }

        if (symbolCount == 0)
            return [];

        if (symbolCount % 8 is 1 or 3 or 6)
            throw new FormatException("Invalid Base32 input.");

        byte[] output = new byte[symbolCount * 5 / 8];
        int buffer = 0;
        int bitsLeft = 0;
        int outputIndex = 0;

        foreach (char ch in text)
        {
            if (char.IsWhiteSpace(ch) || ch == '=')
                continue;

            int value = DecodeBase32Char(ch);
            buffer = (buffer << 5) | value;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                output[outputIndex++] = (byte)((buffer >> bitsLeft) & 0xFF);
            }
        }

        return output;
    }

    public static byte[] NewSecret() => RandomNumberGenerator.GetBytes(SecretLengthBytes);

    public static string OtpAuthUri(string issuer, string username, string base32Secret, int stepSeconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(base32Secret);
        ValidateStepSeconds(stepSeconds);

        string escapedIssuer = Uri.EscapeDataString(issuer);
        string escapedUsername = Uri.EscapeDataString(username);
        string escapedSecret = Uri.EscapeDataString(base32Secret);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"otpauth://totp/{escapedIssuer}:{escapedUsername}?secret={escapedSecret}&issuer={escapedIssuer}&algorithm=SHA1&digits={Digits}&period={stepSeconds}");
    }

    private static void ValidateStepSeconds(int stepSeconds)
    {
        if (stepSeconds is not (30 or 60))
            throw new ArgumentOutOfRangeException(nameof(stepSeconds), stepSeconds, "TOTP step must be 30 or 60 seconds.");
    }

    private static void WriteHotpDigits(ReadOnlySpan<byte> secret, long step, Span<byte> destination)
    {
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);

        Span<byte> hash = stackalloc byte[HMACSHA1.HashSizeInBytes];
        HMACSHA1.HashData(secret, counter, hash);

        int offset = hash[^1] & 0x0F;
        int binaryCode =
            ((hash[offset] & 0x7F) << 24)
            | (hash[offset + 1] << 16)
            | (hash[offset + 2] << 8)
            | hash[offset + 3];

        int otp = binaryCode % Modulo;
        for (int i = Digits - 1; i >= 0; i--)
        {
            destination[i] = (byte)('0' + (otp % 10));
            otp /= 10;
        }
    }

    private static int DecodeBase32Char(char ch) => ch switch
    {
        >= 'A' and <= 'Z' => ch - 'A',
        >= 'a' and <= 'z' => ch - 'a',
        >= '2' and <= '7' => ch - '2' + 26,
        _ => -1,
    };
}
