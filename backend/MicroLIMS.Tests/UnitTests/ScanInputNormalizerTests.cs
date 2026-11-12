using MicroLIMS.Application.Labels;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class ScanInputNormalizerTests
{
    [Fact]
    public void Normalize_trims_ends_and_keeps_inner_spaces()
    {
        var result = ScanInputNormalizer.Normalize("  SP:MP-X 05/11/2026\r\n");

        Assert.Equal("SP:MP-X 05/11/2026", result.Value);
        Assert.False(result.LayoutCorrected);
    }

    [Fact]
    public void Normalize_converts_arabic_layout()
    {
        var single = ScanInputNormalizer.Normalize("ض");
        Assert.Equal("q", single.Value);
        Assert.True(single.LayoutCorrected);

        var fullPayload = ScanInputNormalizer.Normalize("سح:ةح-رهف-ؤ ٠٥ظ١١ظ٢٠٢٦");
        Assert.Equal("sp:mp-vit-c 05/11/2026", fullPayload.Value);
        Assert.True(fullPayload.LayoutCorrected);

        var withLamAlef = ScanInputNormalizer.Normalize("سح:لا-٠١");
        Assert.Equal("sp:b-01", withLamAlef.Value);
        Assert.True(withLamAlef.LayoutCorrected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \r\n\t")]
    public void Normalize_null_or_blank_returns_empty(string? raw)
    {
        var result = ScanInputNormalizer.Normalize(raw);

        Assert.Equal(string.Empty, result.Value);
        Assert.False(result.LayoutCorrected);
    }

    [Theory]
    [InlineData("ض", "q")]
    [InlineData("ص", "w")]
    [InlineData("ث", "e")]
    [InlineData("ق", "r")]
    [InlineData("ف", "t")]
    [InlineData("غ", "y")]
    [InlineData("ع", "u")]
    [InlineData("ه", "i")]
    [InlineData("خ", "o")]
    [InlineData("ح", "p")]
    [InlineData("ش", "a")]
    [InlineData("س", "s")]
    [InlineData("ي", "d")]
    [InlineData("ب", "f")]
    [InlineData("ل", "g")]
    [InlineData("ا", "h")]
    [InlineData("ت", "j")]
    [InlineData("ن", "k")]
    [InlineData("م", "l")]
    [InlineData("ئ", "z")]
    [InlineData("ء", "x")]
    [InlineData("ؤ", "c")]
    [InlineData("ر", "v")]
    [InlineData("لا", "b")]
    [InlineData("ى", "n")]
    [InlineData("ة", "m")]
    [InlineData("ج", "[")]
    [InlineData("د", "]")]
    [InlineData("ك", ";")]
    [InlineData("ط", "'")]
    [InlineData("و", ",")]
    [InlineData("ز", ".")]
    [InlineData("ظ", "/")]
    [InlineData("ذ", "`")]
    [InlineData("\u0660", "0")]
    [InlineData("\u0661", "1")]
    [InlineData("\u0662", "2")]
    [InlineData("\u0663", "3")]
    [InlineData("\u0664", "4")]
    [InlineData("\u0665", "5")]
    [InlineData("\u0666", "6")]
    [InlineData("\u0667", "7")]
    [InlineData("\u0668", "8")]
    [InlineData("\u0669", "9")]
    public void Arabic_101_key_maps_to_latin(string arabic, string latin)
    {
        var result = ScanInputNormalizer.Normalize(arabic);

        Assert.Equal(latin, result.Value);
        Assert.True(result.LayoutCorrected);
    }

    [Theory]
    [InlineData("S:FP1026001")]
    [InlineData("SP:MP-VIT-C 05/11/2026")]
    [InlineData("MED:TSA/03/26")]
    [InlineData("MAT:STK-2026-00123")]
    [InlineData("EQ:INC-F-ML-F-01-003")]
    [InlineData("- . : /")]
    public void Latin_input_is_unchanged_and_not_flagged(string raw)
    {
        var result = ScanInputNormalizer.Normalize(raw);

        Assert.Equal(raw, result.Value);
        Assert.False(result.LayoutCorrected);
    }
}
