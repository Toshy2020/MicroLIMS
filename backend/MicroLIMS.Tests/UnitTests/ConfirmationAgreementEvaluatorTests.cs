using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class ConfirmationAgreementEvaluatorTests
{
    private readonly ConfirmationAgreementEvaluator _evaluator = new();

    [Fact]
    public void EvaluateAgreement_AllPlatesConforming_ReturnsDetected()
    {
        var plates = new List<ConfirmatoryPlateObservation>
        {
            new() { MediumIndex = 0, MaterialId = 1, Observation = GrowthObservation.GrowthConforming },
            new() { MediumIndex = 1, MaterialId = 2, Observation = GrowthObservation.GrowthConforming }
        };

        var result = _evaluator.EvaluateAgreement(plates, requiredMediaCount: 2);

        Assert.Equal(ConfirmationResult.Detected, result);
    }

    [Fact]
    public void EvaluateAgreement_AllPlatesNoGrowth_ReturnsNotDetected()
    {
        var plates = new List<ConfirmatoryPlateObservation>
        {
            new() { MediumIndex = 0, MaterialId = 1, Observation = GrowthObservation.NoGrowth },
            new() { MediumIndex = 1, MaterialId = 2, Observation = GrowthObservation.NoGrowth }
        };

        var result = _evaluator.EvaluateAgreement(plates, requiredMediaCount: 2);

        Assert.Equal(ConfirmationResult.NotDetected, result);
    }

    [Fact]
    public void EvaluateAgreement_AllPlatesNonConforming_ReturnsNotDetected()
    {
        var plates = new List<ConfirmatoryPlateObservation>
        {
            new() { MediumIndex = 0, MaterialId = 1, Observation = GrowthObservation.GrowthNonConforming },
            new() { MediumIndex = 1, MaterialId = 2, Observation = GrowthObservation.GrowthNonConforming }
        };

        var result = _evaluator.EvaluateAgreement(plates, requiredMediaCount: 2);

        Assert.Equal(ConfirmationResult.NotDetected, result);
    }

    [Fact]
    public void EvaluateAgreement_DisagreementBetweenConformingAndNonConforming_ReturnsInconclusive()
    {
        // Media 1 grew conforming colony (e.g. XLD black colony), Media 2 grew non-conforming (e.g. TSI no H2S)
        var plates = new List<ConfirmatoryPlateObservation>
        {
            new() { MediumIndex = 0, MaterialId = 1, Observation = GrowthObservation.GrowthConforming },
            new() { MediumIndex = 1, MaterialId = 2, Observation = GrowthObservation.GrowthNonConforming }
        };

        var result = _evaluator.EvaluateAgreement(plates, requiredMediaCount: 2);

        Assert.Equal(ConfirmationResult.Inconclusive, result);
    }

    [Fact]
    public void EvaluateAgreement_DisagreementBetweenConformingAndNoGrowth_ReturnsInconclusive()
    {
        var plates = new List<ConfirmatoryPlateObservation>
        {
            new() { MediumIndex = 0, MaterialId = 1, Observation = GrowthObservation.GrowthConforming },
            new() { MediumIndex = 1, MaterialId = 2, Observation = GrowthObservation.NoGrowth }
        };

        var result = _evaluator.EvaluateAgreement(plates, requiredMediaCount: 2);

        Assert.Equal(ConfirmationResult.Inconclusive, result);
    }

    [Fact]
    public void EvaluateAgreement_SingleMediaConforming_ReturnsDetected()
    {
        var plates = new List<ConfirmatoryPlateObservation>
        {
            new() { MediumIndex = 0, MaterialId = 1, Observation = GrowthObservation.GrowthConforming }
        };

        var result = _evaluator.EvaluateAgreement(plates, requiredMediaCount: 1);

        Assert.Equal(ConfirmationResult.Detected, result);
    }

    [Fact]
    public void EvaluateAgreement_CountMismatch_ThrowsInvalidOperationException()
    {
        var plates = new List<ConfirmatoryPlateObservation>
        {
            new() { MediumIndex = 0, MaterialId = 1, Observation = GrowthObservation.GrowthConforming }
        };

        Assert.Throws<InvalidOperationException>(() => _evaluator.EvaluateAgreement(plates, requiredMediaCount: 2));
    }
}
