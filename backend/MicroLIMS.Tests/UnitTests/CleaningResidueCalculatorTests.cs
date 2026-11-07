using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class CleaningResidueCalculatorTests
{
    [Fact]
    public void Calculate_UgPerSwab_CalculatesCorrectly()
    {
        // Formula: (sample response / std mean response) * std conc (µg/mL) * extraction vol (mL)
        // 1000 / 2000 * 10 * 5 = 0.5 * 10 * 5 = 25 µg/swab
        var reps = new List<CleaningResidueReplicateInput>
        {
            new(1, 1000m),
            new(2, 1200m) // 1200 / 2000 * 10 * 5 = 0.6 * 10 * 5 = 30 µg/swab
        };

        var result = CleaningResidueCalculator.Calculate(
            standardConcentrationUgPerMl: 10m,
            sampleSolutionVolumeMl: 5m,
            swabAreaCm2: null,
            macoUnit: MacoUnit.UgPerSwab,
            standardMeanResponse: 2000m,
            reps: reps);

        Assert.Equal(2, result.Replicates.Count);
        Assert.Equal(25m, result.Replicates[0].Value);
        Assert.Equal(30m, result.Replicates[1].Value);
        Assert.Equal(27.5m, result.MeanValue);
    }

    [Fact]
    public void Calculate_UgPerCm2_DividesBySwabArea()
    {
        // 1000 / 2000 * 10 * 5 = 25 µg/swab. Swab area = 25 cm² => 1.00 µg/cm²
        var reps = new List<CleaningResidueReplicateInput>
        {
            new(1, 1000m)
        };

        var result = CleaningResidueCalculator.Calculate(
            standardConcentrationUgPerMl: 10m,
            sampleSolutionVolumeMl: 5m,
            swabAreaCm2: 25m,
            macoUnit: MacoUnit.UgPerCm2,
            standardMeanResponse: 2000m,
            reps: reps);

        Assert.Single(result.Replicates);
        Assert.Equal(1.00m, result.Replicates[0].Value);
        Assert.Equal(1.00m, result.MeanValue);
    }

    [Fact]
    public void IsNotDetected_ReturnsTrue_WhenAllResponsesZero()
    {
        var spec = new Specification
        {
            Id = 0,
            LimitType = LimitType.NotMoreThan,
            UpperLimit = 50m,
            Unit = "µg/swab"
        };

        var inputAllZero = new HplcCleaningResidueInput(
            AnalyteId: 1,
            Name: "Paracetamol",
            StandardConcentrationUgPerMl: 10m,
            SampleSolutionVolumeMl: 5m,
            SwabAreaCm2: null,
            MacoLimit: 50m,
            MacoUnit: MacoUnit.UgPerSwab,
            StandardMeanResponse: 2000m,
            Reps: new List<CleaningResidueReplicateInput> { new(1, 0m), new(2, 0m) },
            Spec: spec);

        Assert.True(CleaningResidueCalculator.IsNotDetected(inputAllZero));

        var inputNonZero = new HplcCleaningResidueInput(
            AnalyteId: 1,
            Name: "Paracetamol",
            StandardConcentrationUgPerMl: 10m,
            SampleSolutionVolumeMl: 5m,
            SwabAreaCm2: null,
            MacoLimit: 50m,
            MacoUnit: MacoUnit.UgPerSwab,
            StandardMeanResponse: 2000m,
            Reps: new List<CleaningResidueReplicateInput> { new(1, 0m), new(2, 10m) },
            Spec: spec);

        Assert.False(CleaningResidueCalculator.IsNotDetected(inputNonZero));
    }

    [Fact]
    public void FormatValue_FormatsCorrectly()
    {
        Assert.Equal("12.35 µg/swab", CleaningResidueCalculator.FormatValue(12.3456m, MacoUnit.UgPerSwab));
        Assert.Equal("0.50 µg/cm²", CleaningResidueCalculator.FormatValue(0.5m, MacoUnit.UgPerCm2));
    }

    [Fact]
    public void Calculate_ValidatesInputs()
    {
        var validReps = new List<CleaningResidueReplicateInput> { new(1, 100m) };

        // Std conc <= 0
        Assert.Throws<InvalidOperationException>(() =>
            CleaningResidueCalculator.Calculate(0m, 5m, null, MacoUnit.UgPerSwab, 2000m, validReps));

        // Extraction vol <= 0
        Assert.Throws<InvalidOperationException>(() =>
            CleaningResidueCalculator.Calculate(10m, 0m, null, MacoUnit.UgPerSwab, 2000m, validReps));

        // Std mean response <= 0
        Assert.Throws<InvalidOperationException>(() =>
            CleaningResidueCalculator.Calculate(10m, 5m, null, MacoUnit.UgPerSwab, 0m, validReps));

        // UgPerCm2 without area
        Assert.Throws<InvalidOperationException>(() =>
            CleaningResidueCalculator.Calculate(10m, 5m, null, MacoUnit.UgPerCm2, 2000m, validReps));
        Assert.Throws<InvalidOperationException>(() =>
            CleaningResidueCalculator.Calculate(10m, 5m, 0m, MacoUnit.UgPerCm2, 2000m, validReps));

        // Negative response
        Assert.Throws<InvalidOperationException>(() =>
            CleaningResidueCalculator.Calculate(10m, 5m, null, MacoUnit.UgPerSwab, 2000m, new List<CleaningResidueReplicateInput> { new(1, -5m) }));

        // Empty reps
        Assert.Throws<InvalidOperationException>(() =>
            CleaningResidueCalculator.Calculate(10m, 5m, null, MacoUnit.UgPerSwab, 2000m, new List<CleaningResidueReplicateInput>()));
    }

    [Fact]
    public void Evaluate_ReturnsJudgedRows_WithinLimits_AndOutOfSpecification()
    {
        var spec = new Specification
        {
            Id = 0,
            LimitType = LimitType.NotMoreThan,
            UpperLimit = 30m,
            Unit = "µg/swab"
        };

        // Pass case: mean = 25 <= 30
        var passInput = new HplcCleaningResidueInput(
            AnalyteId: 1,
            Name: "Analyte A",
            StandardConcentrationUgPerMl: 10m,
            SampleSolutionVolumeMl: 5m,
            SwabAreaCm2: null,
            MacoLimit: 30m,
            MacoUnit: MacoUnit.UgPerSwab,
            StandardMeanResponse: 2000m,
            Reps: new List<CleaningResidueReplicateInput> { new(1, 1000m) }, // 25 µg/swab
            Spec: spec);

        var passRows = CleaningResidueCalculator.Evaluate(new[] { passInput });
        Assert.Single(passRows);
        Assert.Equal(ResultStatus.WithinLimits, passRows[0].Status);
        Assert.Equal("25.00 µg/swab", passRows[0].Display);

        // Fail case: mean = 50 > 30
        var failInput = new HplcCleaningResidueInput(
            AnalyteId: 1,
            Name: "Analyte A",
            StandardConcentrationUgPerMl: 10m,
            SampleSolutionVolumeMl: 5m,
            SwabAreaCm2: null,
            MacoLimit: 30m,
            MacoUnit: MacoUnit.UgPerSwab,
            StandardMeanResponse: 2000m,
            Reps: new List<CleaningResidueReplicateInput> { new(1, 2000m) }, // 50 µg/swab
            Spec: spec);

        var failRows = CleaningResidueCalculator.Evaluate(new[] { failInput });
        Assert.Single(failRows);
        Assert.Equal(ResultStatus.OutOfSpecification, failRows[0].Status);
        Assert.Equal("50.00 µg/swab", failRows[0].Display);

        // Zero case: "Not detected", WithinLimits
        var zeroInput = new HplcCleaningResidueInput(
            AnalyteId: 1,
            Name: "Analyte A",
            StandardConcentrationUgPerMl: 10m,
            SampleSolutionVolumeMl: 5m,
            SwabAreaCm2: null,
            MacoLimit: 30m,
            MacoUnit: MacoUnit.UgPerSwab,
            StandardMeanResponse: 2000m,
            Reps: new List<CleaningResidueReplicateInput> { new(1, 0m) },
            Spec: spec);

        var zeroRows = CleaningResidueCalculator.Evaluate(new[] { zeroInput });
        Assert.Single(zeroRows);
        Assert.Equal(ResultStatus.WithinLimits, zeroRows[0].Status);
        Assert.Equal("Not detected", zeroRows[0].Display);
    }
}
