using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Helpers;

public record WeightVariationConfig(
    int UnitCount = 20,
    decimal TabletBand1MaxMg = 130m,
    decimal TabletBand1Percent = 10m,
    decimal TabletBand2MaxMg = 324m,
    decimal TabletBand2Percent = 7.5m,
    decimal TabletBand3Percent = 5m,
    int TabletMaxOutside = 2,
    decimal CapsuleInnerPercent = 10m,
    decimal CapsuleOuterPercent = 25m,
    int CapsuleS1MaxOutside = 2,
    int CapsuleS1MaxForRetest = 6,
    int CapsuleS2ExtraUnits = 40,
    int CapsuleS2MaxOutside = 6);

public record WeightVariationUnitInput(
    decimal? WeightMg = null,
    decimal? GrossMg = null,
    decimal? ShellMg = null);

public record WeightVariationUnitEvaluation(
    int UnitIndex,
    decimal? WeightMg,
    decimal? GrossMg,
    decimal? ShellMg,
    decimal NetMg,
    decimal DeviationPercent,
    bool Passed);

public record WeightVariationEvaluationResult(
    int StageReached,
    DissolutionStageOutcome Outcome,
    decimal MeanWeightMg,
    decimal? MeanGrossMg,
    decimal? BandPercentUsed,
    bool? StepAPassed,
    IReadOnlyList<WeightVariationUnitEvaluation> Units,
    IReadOnlyList<string> Reasons);

public static class WeightVariationEvaluator
{
    public static WeightVariationEvaluationResult Evaluate(
        DosageForm dosageForm,
        IReadOnlyList<WeightVariationUnitInput> units,
        WeightVariationConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(units);
        config ??= new WeightVariationConfig();

        ValidateConfig(config);

        int count = units.Count;
        int s1Units = config.UnitCount;
        int s2Units = config.UnitCount + config.CapsuleS2ExtraUnits;

        if (count != s1Units && count != s2Units)
        {
            throw new InvalidOperationException(
                $"Weight variation evaluation requires {s1Units} (Stage 1) or {s2Units} (Stage 2) units, but received {count}.");
        }

        if (count == s2Units && dosageForm == DosageForm.Tablet)
        {
            throw new InvalidOperationException("Tablets do not support Stage 2 evaluation.");
        }

        ValidateUnitInputs(dosageForm, units);

        if (dosageForm == DosageForm.Tablet)
        {
            return EvaluateTablet(units, config);
        }

        if (count == s1Units)
        {
            return EvaluateCapsuleStage1(dosageForm, units, config);
        }

        return EvaluateCapsuleStage2(dosageForm, units, config);
    }

    private static void ValidateConfig(WeightVariationConfig config)
    {
        if (config.UnitCount < 1 || config.CapsuleS2ExtraUnits < 1)
            throw new InvalidOperationException("Weight variation unit count and extra units must be greater than or equal to 1.");
        if (config.TabletMaxOutside < 0 || config.CapsuleS1MaxOutside < 0 || config.CapsuleS2MaxOutside < 0)
            throw new InvalidOperationException("Weight variation maximum outside counts must be greater than or equal to 0.");
        if (config.TabletBand1MaxMg <= 0m || config.TabletBand2MaxMg <= 0m)
            throw new InvalidOperationException("Weight variation tablet band weight limits must be greater than zero.");
        if (config.TabletBand1Percent <= 0m || config.TabletBand2Percent <= 0m || config.TabletBand3Percent <= 0m ||
            config.CapsuleInnerPercent <= 0m || config.CapsuleOuterPercent <= 0m)
            throw new InvalidOperationException("Weight variation percentages must be greater than zero.");
        if (config.TabletBand1MaxMg >= config.TabletBand2MaxMg)
            throw new InvalidOperationException("Weight variation Tablet Band 1 Max Mg must be less than Band 2 Max Mg.");
        if (config.CapsuleInnerPercent >= config.CapsuleOuterPercent)
            throw new InvalidOperationException("Weight variation capsule inner percentage must be less than outer percentage.");
        if (config.CapsuleS1MaxOutside >= config.CapsuleS1MaxForRetest || config.CapsuleS1MaxForRetest > config.UnitCount)
            throw new InvalidOperationException("Weight variation capsule Stage 1 max outside must be less than Stage 1 max for retest, which must be less than or equal to unit count.");
        if (config.CapsuleS2MaxOutside >= config.UnitCount + config.CapsuleS2ExtraUnits)
            throw new InvalidOperationException($"Weight variation capsule Stage 2 max outside must be less than total units ({config.UnitCount + config.CapsuleS2ExtraUnits}).");
    }

    private static void ValidateUnitInputs(DosageForm dosageForm, IReadOnlyList<WeightVariationUnitInput> units)
    {
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (dosageForm == DosageForm.Tablet)
            {
                if (u.GrossMg.HasValue || u.ShellMg.HasValue)
                    throw new InvalidOperationException($"Unit {i + 1}: Tablet units must only contain WeightMg.");
                if (!u.WeightMg.HasValue || u.WeightMg.Value <= 0m)
                    throw new InvalidOperationException($"Unit {i + 1}: Tablet unit weight must be greater than zero.");
            }
            else
            {
                if (u.WeightMg.HasValue)
                    throw new InvalidOperationException($"Unit {i + 1}: Capsule units must only contain GrossMg and ShellMg.");
                if (!u.GrossMg.HasValue || !u.ShellMg.HasValue)
                    throw new InvalidOperationException($"Unit {i + 1}: Capsule units must contain both GrossMg and ShellMg.");
                if (u.ShellMg.Value <= 0m)
                    throw new InvalidOperationException($"Unit {i + 1}: Shell weight must be greater than zero.");
                if (u.ShellMg.Value >= u.GrossMg.Value)
                    throw new InvalidOperationException($"Unit {i + 1}: Shell weight must be less than gross weight.");
            }
        }
    }

    private static WeightVariationEvaluationResult EvaluateTablet(
        IReadOnlyList<WeightVariationUnitInput> units,
        WeightVariationConfig config)
    {
        decimal mean = units.Sum(u => u.WeightMg!.Value) / units.Count;

        decimal bandPercent;
        if (mean <= config.TabletBand1MaxMg)
            bandPercent = config.TabletBand1Percent;
        else if (mean <= config.TabletBand2MaxMg)
            bandPercent = config.TabletBand2Percent;
        else
            bandPercent = config.TabletBand3Percent;

        var evaluations = new List<WeightVariationUnitEvaluation>();
        int outsideCount = 0;
        int beyond2PCount = 0;

        for (int i = 0; i < units.Count; i++)
        {
            decimal weight = units[i].WeightMg!.Value;
            decimal dev = Math.Abs(weight - mean) * 100m / mean;
            bool passed = dev <= bandPercent;
            if (!passed) outsideCount++;
            if (dev > (2m * bandPercent)) beyond2PCount++;

            evaluations.Add(new WeightVariationUnitEvaluation(
                UnitIndex: i + 1,
                WeightMg: weight,
                GrossMg: null,
                ShellMg: null,
                NetMg: weight,
                DeviationPercent: dev,
                Passed: passed));
        }

        var reasons = new List<string>();
        DissolutionStageOutcome outcome;
        if (outsideCount <= config.TabletMaxOutside && beyond2PCount == 0)
        {
            outcome = DissolutionStageOutcome.Complies;
        }
        else
        {
            outcome = DissolutionStageOutcome.DoesNotComply;
            if (outsideCount > config.TabletMaxOutside && beyond2PCount > 0)
            {
                reasons.Add($"{outsideCount} unit(s) exceeded limit of {bandPercent} % (maximum allowed is {config.TabletMaxOutside}), and {beyond2PCount} unit(s) exceeded 2x limit of {2m * bandPercent} %.");
            }
            else if (outsideCount > config.TabletMaxOutside)
            {
                reasons.Add($"{outsideCount} unit(s) exceeded limit of {bandPercent} %, exceeding maximum allowed of {config.TabletMaxOutside}.");
            }
            else
            {
                reasons.Add($"{beyond2PCount} unit(s) exceeded 2x limit of {2m * bandPercent} %.");
            }
        }

        return new WeightVariationEvaluationResult(
            StageReached: 1,
            Outcome: outcome,
            MeanWeightMg: mean,
            MeanGrossMg: null,
            BandPercentUsed: bandPercent,
            StepAPassed: null,
            Units: evaluations,
            Reasons: reasons);
    }

    private static WeightVariationEvaluationResult EvaluateCapsuleStage1(
        DosageForm dosageForm,
        IReadOnlyList<WeightVariationUnitInput> units,
        WeightVariationConfig config)
    {
        decimal meanGross = units.Sum(u => u.GrossMg!.Value) / units.Count;
        decimal meanNet = units.Sum(u => u.GrossMg!.Value - u.ShellMg!.Value) / units.Count;

        // Step A check (only for HardCapsule)
        bool stepAPassed = false;
        if (dosageForm == DosageForm.HardCapsule)
        {
            stepAPassed = units.All(u =>
            {
                decimal grossDev = Math.Abs(u.GrossMg!.Value - meanGross) * 100m / meanGross;
                return grossDev <= config.CapsuleInnerPercent;
            });
        }

        var evaluations = new List<WeightVariationUnitEvaluation>();
        int innerOutsideCount = 0;
        int outerOutsideCount = 0;

        for (int i = 0; i < units.Count; i++)
        {
            decimal gross = units[i].GrossMg!.Value;
            decimal shell = units[i].ShellMg!.Value;
            decimal net = gross - shell;
            decimal dev = Math.Abs(net - meanNet) * 100m / meanNet;
            bool passed = dev <= config.CapsuleInnerPercent;
            if (!passed) innerOutsideCount++;
            if (dev > config.CapsuleOuterPercent) outerOutsideCount++;

            evaluations.Add(new WeightVariationUnitEvaluation(
                UnitIndex: i + 1,
                WeightMg: null,
                GrossMg: gross,
                ShellMg: shell,
                NetMg: net,
                DeviationPercent: dev,
                Passed: passed));
        }

        // If step A passed on intact capsules, Complies without net check. The per-unit
        // deviation and pass flag then describe the gross weights the decision was made on.
        if (stepAPassed)
        {
            evaluations = evaluations
                .Select(e =>
                {
                    decimal grossDev = Math.Abs(e.GrossMg!.Value - meanGross) * 100m / meanGross;
                    return e with { DeviationPercent = grossDev, Passed = true };
                })
                .ToList();

            return new WeightVariationEvaluationResult(
                StageReached: 1,
                Outcome: DissolutionStageOutcome.Complies,
                MeanWeightMg: meanNet,
                MeanGrossMg: meanGross,
                BandPercentUsed: config.CapsuleInnerPercent,
                StepAPassed: true,
                Units: evaluations,
                Reasons: Array.Empty<string>());
        }

        // Step B evaluation on net contents
        var reasons = new List<string>();
        DissolutionStageOutcome outcome;
        if (outerOutsideCount > 0)
        {
            outcome = DissolutionStageOutcome.DoesNotComply;
            reasons.Add($"{outerOutsideCount} unit(s) exceeded outer limit of {config.CapsuleOuterPercent} %.");
        }
        else if (innerOutsideCount <= config.CapsuleS1MaxOutside)
        {
            outcome = DissolutionStageOutcome.Complies;
        }
        else if (innerOutsideCount <= config.CapsuleS1MaxForRetest)
        {
            outcome = DissolutionStageOutcome.NextStageRequired;
            reasons.Add($"{innerOutsideCount} unit(s) exceeded inner limit of {config.CapsuleInnerPercent} %. Proceed to Stage 2.");
        }
        else
        {
            outcome = DissolutionStageOutcome.DoesNotComply;
            reasons.Add($"{innerOutsideCount} unit(s) exceeded inner limit of {config.CapsuleInnerPercent} %, exceeding maximum allowed for Stage 1 ({config.CapsuleS1MaxForRetest}).");
        }

        return new WeightVariationEvaluationResult(
            StageReached: 1,
            Outcome: outcome,
            MeanWeightMg: meanNet,
            MeanGrossMg: meanGross,
            BandPercentUsed: config.CapsuleInnerPercent,
            StepAPassed: dosageForm == DosageForm.HardCapsule ? false : null,
            Units: evaluations,
            Reasons: reasons);
    }

    private static WeightVariationEvaluationResult EvaluateCapsuleStage2(
        DosageForm dosageForm,
        IReadOnlyList<WeightVariationUnitInput> units,
        WeightVariationConfig config)
    {
        decimal meanGross = units.Sum(u => u.GrossMg!.Value) / units.Count;
        decimal meanNet = units.Sum(u => u.GrossMg!.Value - u.ShellMg!.Value) / units.Count;

        var evaluations = new List<WeightVariationUnitEvaluation>();
        int innerOutsideCount = 0;
        int outerOutsideCount = 0;

        for (int i = 0; i < units.Count; i++)
        {
            decimal gross = units[i].GrossMg!.Value;
            decimal shell = units[i].ShellMg!.Value;
            decimal net = gross - shell;
            decimal dev = Math.Abs(net - meanNet) * 100m / meanNet;
            bool passed = dev <= config.CapsuleInnerPercent;
            if (!passed) innerOutsideCount++;
            if (dev > config.CapsuleOuterPercent) outerOutsideCount++;

            evaluations.Add(new WeightVariationUnitEvaluation(
                UnitIndex: i + 1,
                WeightMg: null,
                GrossMg: gross,
                ShellMg: shell,
                NetMg: net,
                DeviationPercent: dev,
                Passed: passed));
        }

        var reasons = new List<string>();
        DissolutionStageOutcome outcome;
        if (outerOutsideCount > 0)
        {
            outcome = DissolutionStageOutcome.DoesNotComply;
            reasons.Add($"{outerOutsideCount} unit(s) exceeded outer limit of {config.CapsuleOuterPercent} %.");
        }
        else if (innerOutsideCount <= config.CapsuleS2MaxOutside)
        {
            outcome = DissolutionStageOutcome.Complies;
        }
        else
        {
            outcome = DissolutionStageOutcome.DoesNotComply;
            reasons.Add($"{innerOutsideCount} unit(s) exceeded inner limit of {config.CapsuleInnerPercent} %, exceeding maximum allowed for Stage 2 ({config.CapsuleS2MaxOutside}).");
        }

        return new WeightVariationEvaluationResult(
            StageReached: 2,
            Outcome: outcome,
            MeanWeightMg: meanNet,
            MeanGrossMg: meanGross,
            BandPercentUsed: config.CapsuleInnerPercent,
            StepAPassed: null,
            Units: evaluations,
            Reasons: reasons);
    }
}
