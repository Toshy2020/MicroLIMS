using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

// GC half of the method validation (spec 2026-10-03 3.1). HPLC half lives in HplcMethodService.cs.
public partial class HplcMethodService
{
    private static void ValidateGcSection(SaveHplcMethodRequest r)
    {
        if (!(r.FilmThicknessUm > 0m))
            throw new InvalidOperationException("Film thickness is required for a GC method.");
        if (r.CarrierGas == null)
            throw new InvalidOperationException("Carrier gas is required for a GC method.");
        if (!(r.InletTemperatureC > 0m) || !(r.DetectorTemperatureC > 0m))
            throw new InvalidOperationException("Inlet and detector temperatures are required for a GC method.");
        if (r.SplitRatio.HasValue && r.SplitRatio.Value < 1m)
            throw new InvalidOperationException("Split ratio must be 1 or more (leave empty for splitless).");

        if (r.DetectorType is not (HplcDetectorType.Fid or HplcDetectorType.Tcd or HplcDetectorType.Ecd or HplcDetectorType.Ms))
            throw new InvalidOperationException($"Detector {r.DetectorType} is not a GC detector.");

        if (r.MobilePhases.Count != 0 || r.GradientSteps.Count != 0 || r.ElutionMode != ElutionMode.Isocratic
            || r.ParticleSizeUm != null || r.ColumnTemperatureC != null || r.EquilibrationMin != null)
            throw new InvalidOperationException("Mobile phases, gradient, particle size, column temperature and equilibration are HPLC-only.");

        var oven = r.OvenSteps ?? new List<GcOvenStepInput>();
        if (oven.Count < 1)
            throw new InvalidOperationException("A GC method needs at least one oven program step.");
        for (var i = 0; i < oven.Count; i++)
        {
            var n = i + 1;
            var step = oven[i];
            if (i == 0 && step.RateCPerMin != null)
                throw new InvalidOperationException("The first oven step is the initial temperature and has no ramp rate.");
            if (i > 0 && !(step.RateCPerMin > 0m))
                throw new InvalidOperationException($"Oven step {n}: the ramp rate must be greater than zero.");
            if (step.TemperatureC <= 0m || step.HoldMin < 0m)
                throw new InvalidOperationException($"Oven step {n}: temperature must be greater than zero and hold zero or more.");
        }

        if (r.HeadspaceEnabled)
        {
            if (!(r.HeadspaceEquilibrationTemperatureC > 0m) || !(r.HeadspaceEquilibrationMin > 0m) || !(r.HeadspaceTransferLineTemperatureC > 0m))
                throw new InvalidOperationException("Headspace temperatures and equilibration time are required when headspace is on.");
        }
        else if (r.HeadspaceEquilibrationTemperatureC != null || r.HeadspaceEquilibrationMin != null || r.HeadspaceTransferLineTemperatureC != null)
            throw new InvalidOperationException("Headspace values are only used when headspace is on.");

        var residual = r.ResultMode == HplcResultMode.ResidualSolvents;
        if (residual && !(r.SampleSolutionVolumeMl > 0m))
            throw new InvalidOperationException("Sample solution volume is required for residual solvents.");
        if (!residual && r.SampleSolutionVolumeMl != null)
            throw new InvalidOperationException("Standard concentration and sample solution volume are only used for residual solvents.");

        foreach (var a in r.Analytes)
        {
            if (a.WavelengthNm != null)
                throw new InvalidOperationException("Wavelength is not used on a GC method.");

            if (residual)
            {
                if (!(a.StandardConcentrationUgPerMl > 0m))
                    throw new InvalidOperationException($"{a.Name}: standard concentration (µg/mL) is required.");
                if (a.TheoreticalWeightStdMg != 0m || a.TheoreticalWeightTestMg != 0m || a.StandardDilution != null)
                    throw new InvalidOperationException("Theoretical weights and standard dilution are not used for residual solvents.");
            }
            else
            {
                if (a.TheoreticalWeightStdMg <= 0m || a.TheoreticalWeightTestMg <= 0m)
                    throw new InvalidOperationException("Theoretical weights must be greater than zero.");
                if (a.StandardConcentrationUgPerMl != null)
                    throw new InvalidOperationException("Standard concentration and sample solution volume are only used for residual solvents.");
                if (a.StandardDilution.HasValue && a.StandardDilution.Value <= 0m)
                    throw new InvalidOperationException("Standard dilution must be greater than zero.");
            }
        }
    }
}
