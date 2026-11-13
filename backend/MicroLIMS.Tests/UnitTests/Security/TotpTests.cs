using System.Text;
using MicroLIMS.Application.Security;
using Xunit;

namespace MicroLIMS.Tests.UnitTests.Security;

public class TotpTests
{
    // RFC 6238 Appendix B test secret: ASCII "12345678901234567890" (20 bytes).
    private static readonly byte[] Rfc6238Sha1Secret = Encoding.ASCII.GetBytes("12345678901234567890");

    private static DateTime UtcAt(long unixSeconds) => DateTime.UnixEpoch.AddSeconds(unixSeconds);

    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    [InlineData(20000000000L, "353130")]
    public void Generate_MatchesRfc6238Sha1Vectors(long unixSeconds, string expectedSixDigits)
    {
        var step = Totp.StepAt(UtcAt(unixSeconds), 30);

        var code = Totp.Generate(Rfc6238Sha1Secret, step);

        Assert.Equal(expectedSixDigits, code);
    }

    [Fact]
    public void Verify_AcceptsPreviousAndNextStep_RejectsTwoStepsAway()
    {
        const int stepSeconds = 30;
        var nowUtc = UtcAt(1_700_000_010); // step = 56,666,667
        var currentStep = Totp.StepAt(nowUtc, stepSeconds);

        var prevCode = Totp.Generate(Rfc6238Sha1Secret, currentStep - 1);
        var currCode = Totp.Generate(Rfc6238Sha1Secret, currentStep);
        var nextCode = Totp.Generate(Rfc6238Sha1Secret, currentStep + 1);
        var twoBackCode = Totp.Generate(Rfc6238Sha1Secret, currentStep - 2);
        var twoForwardCode = Totp.Generate(Rfc6238Sha1Secret, currentStep + 2);

        Assert.Equal(currentStep - 1, Totp.Verify(Rfc6238Sha1Secret, prevCode, nowUtc, stepSeconds, null));
        Assert.Equal(currentStep, Totp.Verify(Rfc6238Sha1Secret, currCode, nowUtc, stepSeconds, null));
        Assert.Equal(currentStep + 1, Totp.Verify(Rfc6238Sha1Secret, nextCode, nowUtc, stepSeconds, null));

        // Guard against accidental collisions in test vector space before asserting rejection.
        if (twoBackCode != prevCode && twoBackCode != currCode && twoBackCode != nextCode)
            Assert.Null(Totp.Verify(Rfc6238Sha1Secret, twoBackCode, nowUtc, stepSeconds, null));

        if (twoForwardCode != prevCode && twoForwardCode != currCode && twoForwardCode != nextCode)
            Assert.Null(Totp.Verify(Rfc6238Sha1Secret, twoForwardCode, nowUtc, stepSeconds, null));
    }

    [Fact]
    public void Verify_RejectsStepAlreadyAccepted_AndEarlier()
    {
        const int stepSeconds = 30;
        var nowUtc = UtcAt(1_700_000_010);
        var currentStep = Totp.StepAt(nowUtc, stepSeconds);

        var prevCode = Totp.Generate(Rfc6238Sha1Secret, currentStep - 1);
        var currCode = Totp.Generate(Rfc6238Sha1Secret, currentStep);
        var nextCode = Totp.Generate(Rfc6238Sha1Secret, currentStep + 1);

        // Matched step equals lastAcceptedStep -> rejected.
        Assert.Null(Totp.Verify(Rfc6238Sha1Secret, currCode, nowUtc, stepSeconds, currentStep));
        Assert.Null(Totp.Verify(Rfc6238Sha1Secret, nextCode, nowUtc, stepSeconds, currentStep + 1));

        // Code for step - 1 when lastAcceptedStep is currentStep -> rejected.
        Assert.Null(Totp.Verify(Rfc6238Sha1Secret, prevCode, nowUtc, stepSeconds, currentStep));

        // Next step is strictly greater than currentStep -> accepted.
        Assert.Equal(currentStep + 1, Totp.Verify(Rfc6238Sha1Secret, nextCode, nowUtc, stepSeconds, currentStep));
    }

    [Fact]
    public void Verify_ReturnsTheMatchedStep_NotTheCurrentStep()
    {
        const int stepSeconds = 30;
        var nowUtc = UtcAt(1_700_000_010);
        var currentStep = Totp.StepAt(nowUtc, stepSeconds);
        var previousStep = currentStep - 1;
        var previousCode = Totp.Generate(Rfc6238Sha1Secret, previousStep);

        var matched = Totp.Verify(Rfc6238Sha1Secret, previousCode, nowUtc, stepSeconds, null);

        Assert.Equal(previousStep, matched);
        Assert.NotEqual(currentStep, matched);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12a456")]
    [InlineData(" 123456")]
    [InlineData("١٢٣٤٥٦")] // Arabic-Indic digits
    public void Verify_RejectsNonDigits_WrongLength_Empty(string? code)
    {
        var nowUtc = UtcAt(1_700_000_010);

        var matched = Totp.Verify(Rfc6238Sha1Secret, code, nowUtc, 30, null);

        Assert.Null(matched);
    }

    [Fact]
    public void Verify_SixtySecondStep_UsesSixtySecondWindow()
    {
        const int stepSeconds = 60;
        var nowUtc = UtcAt(1_700_000_040); // 1700000040 / 60 = 28333334
        var currentStep = Totp.StepAt(nowUtc, stepSeconds);
        Assert.Equal(1_700_000_040L / 60, currentStep);

        var code60 = Totp.Generate(Rfc6238Sha1Secret, currentStep);
        var codePrev60 = Totp.Generate(Rfc6238Sha1Secret, currentStep - 1);
        var codeNext60 = Totp.Generate(Rfc6238Sha1Secret, currentStep + 1);

        Assert.Equal(currentStep, Totp.Verify(Rfc6238Sha1Secret, code60, nowUtc, stepSeconds, null));
        Assert.Equal(currentStep - 1, Totp.Verify(Rfc6238Sha1Secret, codePrev60, nowUtc, stepSeconds, null));
        Assert.Equal(currentStep + 1, Totp.Verify(Rfc6238Sha1Secret, codeNext60, nowUtc, stepSeconds, null));

        // 30-second step produces a completely different step counter at the same timestamp.
        var step30 = Totp.StepAt(nowUtc, 30);
        Assert.NotEqual(currentStep, step30);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(29)]
    [InlineData(45)]
    [InlineData(120)]
    [InlineData(-30)]
    public void StepAt_RejectsOtherStepLengths_AndLocalTime(int invalidStepSeconds)
    {
        var nowUtc = UtcAt(1_700_000_000);

        Assert.Throws<ArgumentOutOfRangeException>(() => Totp.StepAt(nowUtc, invalidStepSeconds));
    }

    [Fact]
    public void StepAt_RejectsLocalTime_AndTreatsUnspecifiedAsUtc()
    {
        var localTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Local);
        var unspecifiedTime = new DateTime(2025, 1, 1, 0, 0, 30, DateTimeKind.Unspecified);
        var utcTime = new DateTime(2025, 1, 1, 0, 0, 30, DateTimeKind.Utc);

        Assert.Throws<ArgumentException>(() => Totp.StepAt(localTime, 30));
        Assert.Equal(Totp.StepAt(utcTime, 30), Totp.StepAt(unspecifiedTime, 30));
    }

    [Fact]
    public void StepAt_FloorsNegativeUnixTimestamps()
    {
        var oneSecondBeforeEpoch = UtcAt(-1);
        var thirtySecondsBeforeEpoch = UtcAt(-30);
        var thirtyOneSecondsBeforeEpoch = UtcAt(-31);

        Assert.Equal(-1, Totp.StepAt(oneSecondBeforeEpoch, 30));
        Assert.Equal(-1, Totp.StepAt(thirtySecondsBeforeEpoch, 30));
        Assert.Equal(-2, Totp.StepAt(thirtyOneSecondsBeforeEpoch, 30));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("f", "MY")]
    [InlineData("fo", "MZXQ")]
    [InlineData("foo", "MZXW6")]
    [InlineData("foob", "MZXW6YQ")]
    [InlineData("fooba", "MZXW6YTB")]
    [InlineData("foobar", "MZXW6YTBOI")]
    public void Base32_RoundTrips_AndIgnoresSpacesAndCase(string ascii, string expectedBase32)
    {
        var bytes = Encoding.ASCII.GetBytes(ascii);

        var encoded = Totp.ToBase32(bytes);
        Assert.Equal(expectedBase32, encoded);

        var decoded = Totp.FromBase32(encoded);
        Assert.Equal(bytes, decoded);

        if (encoded.Length >= 2)
        {
            var spacedLower = $"{encoded[..1].ToLowerInvariant()} {encoded[1..].ToLowerInvariant()} ";
            Assert.Equal(bytes, Totp.FromBase32(spacedLower));
        }
    }

    [Theory]
    [InlineData("MZXW1")]
    [InlineData("MZXW0")]
    [InlineData("MZXW8")]
    [InlineData("MZXW9")]
    [InlineData("MZXW!")]
    [InlineData("MY=MY")]
    [InlineData("A")]
    public void FromBase32_InvalidCharacter_Throws(string invalid)
    {
        Assert.Throws<FormatException>(() => Totp.FromBase32(invalid));
    }

    [Theory]
    [InlineData("MY======", "f")]
    [InlineData("MZXQ====", "fo")]
    [InlineData("MZXW6===", "foo")]
    [InlineData("MZXW6YQ=", "foob")]
    [InlineData("mzxw 6ytb oi==", "foobar")]
    public void FromBase32_AcceptsPadding(string padded, string expectedAscii)
    {
        var decoded = Totp.FromBase32(padded);

        Assert.Equal(Encoding.ASCII.GetBytes(expectedAscii), decoded);
    }

    [Fact]
    public void NewSecret_Is20Bytes_AndDiffersBetweenCalls()
    {
        var first = Totp.NewSecret();
        var second = Totp.NewSecret();

        Assert.Equal(20, first.Length);
        Assert.Equal(20, second.Length);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void OtpAuthUri_EscapesIssuerAndUsername_AndIncludesPeriod()
    {
        var uri30 = Totp.OtpAuthUri("MicroLIMS", "a@b", "MZXW6YTBOI", 30);
        var uri60 = Totp.OtpAuthUri("MicroLIMS Lab", "a+b@example.com", "MZXW6YTBOI", 60);

        Assert.Equal(
            "otpauth://totp/MicroLIMS:a%40b?secret=MZXW6YTBOI&issuer=MicroLIMS&algorithm=SHA1&digits=6&period=30",
            uri30);
        Assert.Equal(
            "otpauth://totp/MicroLIMS%20Lab:a%2Bb%40example.com?secret=MZXW6YTBOI&issuer=MicroLIMS%20Lab&algorithm=SHA1&digits=6&period=60",
            uri60);
    }

    [Fact]
    public void FromHex_DecodesIgnoringSpacesAndCase_AndRejectsInvalidInput()
    {
        var expected = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01 };
        Assert.Equal(expected, Totp.FromHex("deadbeef01"));
        Assert.Equal(expected, Totp.FromHex("DE AD BE EF 01"));
        Assert.Equal([], Totp.FromHex("   "));
        Assert.Throws<FormatException>(() => Totp.FromHex("abc"));
        Assert.Throws<FormatException>(() => Totp.FromHex("zzzz"));
    }
}
