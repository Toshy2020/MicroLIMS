using MicroLIMS.Application.Labels;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class LabelPayloadTests
{
    [Theory]
    [InlineData(LabelEntityType.Sample, "FP1026001", "S:FP1026001")]
    [InlineData(LabelEntityType.MediaLot, "TSA/03/26", "MED:TSA/03/26")]
    [InlineData(LabelEntityType.SolutionPreparation, "MP-VIT-C 05/11/2026", "SP:MP-VIT-C 05/11/2026")]
    [InlineData(LabelEntityType.Cryovial, "ECOLI/02/26", "CRY:ECOLI/02/26")]
    [InlineData(LabelEntityType.MaterialStockLot, "STK-2026-00123", "MAT:STK-2026-00123")]
    [InlineData(LabelEntityType.EquipmentAsset, "INC-F-ML-F-01-003", "EQ:INC-F-ML-F-01-003")]
    public void Build_then_parse_round_trips(LabelEntityType type, string code, string payload)
    {
        var built = LabelPayload.Build(type, code);
        Assert.Equal(payload, built);

        var ok = LabelPayload.TryParse(built, out var parsedType, out var parsedCode);
        Assert.True(ok);
        Assert.Equal(type, parsedType);
        Assert.Equal(code, parsedCode);
    }

    [Fact]
    public void Parse_splits_on_first_colon_only()
    {
        var ok = LabelPayload.TryParse("SP:MP-A:B 05/11/2026", out var type, out var code);

        Assert.True(ok);
        Assert.Equal(LabelEntityType.SolutionPreparation, type);
        Assert.Equal("MP-A:B 05/11/2026", code);
    }

    [Theory]
    [InlineData("XX:ABC")]
    [InlineData("MP-A:B")]
    [InlineData("S:")]
    [InlineData("S:   ")]
    [InlineData(":FP1026001")]
    [InlineData("FP1026001")]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_rejects_unknown_prefix(string input)
    {
        var ok = LabelPayload.TryParse(input, out var type, out var code);

        Assert.False(ok);
        Assert.Equal(default, type);
        Assert.Equal(string.Empty, code);
    }

    [Theory]
    [InlineData("med:TSA/03/26", LabelEntityType.MediaLot, "TSA/03/26")]
    [InlineData("s:FP1026001", LabelEntityType.Sample, "FP1026001")]
    [InlineData("Eq:INC-01", LabelEntityType.EquipmentAsset, "INC-01")]
    public void Parse_is_case_insensitive_on_prefix(string input, LabelEntityType expectedType, string expectedCode)
    {
        var ok = LabelPayload.TryParse(input, out var type, out var code);

        Assert.True(ok);
        Assert.Equal(expectedType, type);
        Assert.Equal(expectedCode, code);
    }

    [Fact]
    public void Sample_and_equipment_use_code128_others_datamatrix()
    {
        Assert.Equal(LabelSymbology.Code128, LabelPayload.SymbologyOf(LabelEntityType.Sample));
        Assert.Equal(LabelSymbology.Code128, LabelPayload.SymbologyOf(LabelEntityType.EquipmentAsset));
        Assert.Equal(LabelSymbology.DataMatrix, LabelPayload.SymbologyOf(LabelEntityType.MediaLot));
        Assert.Equal(LabelSymbology.DataMatrix, LabelPayload.SymbologyOf(LabelEntityType.SolutionPreparation));
        Assert.Equal(LabelSymbology.DataMatrix, LabelPayload.SymbologyOf(LabelEntityType.Cryovial));
        Assert.Equal(LabelSymbology.DataMatrix, LabelPayload.SymbologyOf(LabelEntityType.MaterialStockLot));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_throws_for_null_or_blank_code(string? code)
    {
        Assert.ThrowsAny<ArgumentException>(() => LabelPayload.Build(LabelEntityType.Sample, code!));
    }

    [Fact]
    public void Enum_numbers_are_pinned()
    {
        Assert.Equal(6, Enum.GetValues<LabelEntityType>().Length);
        Assert.Equal(1, (int)LabelEntityType.Sample);
        Assert.Equal(2, (int)LabelEntityType.MediaLot);
        Assert.Equal(3, (int)LabelEntityType.SolutionPreparation);
        Assert.Equal(4, (int)LabelEntityType.Cryovial);
        Assert.Equal(5, (int)LabelEntityType.MaterialStockLot);
        Assert.Equal(6, (int)LabelEntityType.EquipmentAsset);

        Assert.Equal(2, Enum.GetValues<LabelSymbology>().Length);
        Assert.Equal(1, (int)LabelSymbology.Code128);
        Assert.Equal(2, (int)LabelSymbology.DataMatrix);

        Assert.Equal(2, Enum.GetValues<LabelLayout>().Length);
        Assert.Equal(1, (int)LabelLayout.Standard);
        Assert.Equal(2, (int)LabelLayout.Small);
    }
}
