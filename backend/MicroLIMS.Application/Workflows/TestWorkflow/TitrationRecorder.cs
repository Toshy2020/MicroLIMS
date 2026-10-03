using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Workflows;

public record TitrationStandardInput(int MaterialId, decimal WeightMg, decimal TitreMl);
public record TitrationReplicateInput(decimal SampleWeightMg, decimal TitrantVolumeMl);

public record TitrationPayload(
    DateTime AnalysedAt,
    int? EquipmentId,
    int TitrantPreparationId,
    int? ExcessPreparationId,
    decimal? BlankVolumeMl,
    decimal? TitrationTemperatureC,
    decimal? LossOnDryingPercent,
    decimal? AverageUnitWeightMg,
    List<TitrationStandardInput>? Standards,
    List<int> SpecificationIds,
    List<TitrationReplicateInput> Replicates,
    string Password,
    string? Comment);

// Titration assay (spec 2026-10-03, sections 4-5 and amendments). Server-side rules
// and calculation; the frontend preview is display only.
public sealed class TitrationRecorder : TestWorkflowSupport
{
    private static readonly ResultBasis[] NeedLoss = { ResultBasis.PercentDriedBasis, ResultBasis.PercentAnhydrousBasis };
    private static readonly ResultBasis[] NeedUnitWeight = { ResultBasis.PercentLabelClaim, ResultBasis.MgPerUnit };

    public TitrationRecorder(TestWorkflowDependencies deps) : base(deps) { }

    public Task<TestWorkflowResult> RecordTitrationResultAsync(
        int testOrderId, TitrationPayload payload, int userId, string? ipAddress = null) =>
        UnitOfWork.RunAsync(_db, () => RecordCoreAsync(testOrderId, payload, userId, ipAddress));

    private async Task<TestWorkflowResult> RecordCoreAsync(int testOrderId, TitrationPayload p, int userId, string? ipAddress)
    {
        var (order, t, specById, analysedAtUtc) = await ValidateTestAnalysisOrderAsync(
            testOrderId, p.AnalysedAt, p.EquipmentId, p.SpecificationIds, p.Password, WorkflowType.Titration, userId);

        var cfg = TitrationConfig.From(t);
        bool residual = cfg.Mode == TitrationMode.Residual;
        bool relative = cfg.Calculation == TitrationCalculation.Relative;
        bool kf = cfg.Type == TitrationType.KarlFischer;
        var nowUtc = _clock.UtcNow.UtcDateTime;
        var today = _clock.LabToday;

        // ---- request shape ----
        if (p.Replicates == null || p.Replicates.Count != cfg.ReplicateCount)
            throw new InvalidOperationException($"Exactly {cfg.ReplicateCount} replicate(s) are required.");
        for (int i = 0; i < p.Replicates.Count; i++)
        {
            if (p.Replicates[i].SampleWeightMg <= 0m) throw new InvalidOperationException($"Replicate {i + 1}: sample weight must be greater than zero.");
            if (p.Replicates[i].TitrantVolumeMl <= 0m) throw new InvalidOperationException($"Replicate {i + 1}: titrant volume must be greater than zero.");
        }

        if (cfg.BlankRequired)
        {
            if (!p.BlankVolumeMl.HasValue) throw new InvalidOperationException("A blank volume is required for this test.");
            if (p.BlankVolumeMl.Value < 0m) throw new InvalidOperationException("Blank volume must not be negative.");
        }
        else if (p.BlankVolumeMl.HasValue)
            throw new InvalidOperationException("This test does not use a blank.");
        decimal blank = p.BlankVolumeMl ?? 0m;

        if (cfg.TempCorrection)
        {
            if (!p.TitrationTemperatureC.HasValue) throw new InvalidOperationException("The titration temperature is required for temperature correction.");
        }
        else if (p.TitrationTemperatureC.HasValue)
            throw new InvalidOperationException("This test does not use temperature correction.");

        var specs = specById.Values.OrderBy(s => s.DisplayOrder).ThenBy(s => s.Id).ToList();
        foreach (var s in specs)
        {
            if (s.ResultBasis is not (ResultBasis.PercentAsIs or ResultBasis.PercentDriedBasis or ResultBasis.PercentAnhydrousBasis
                or ResultBasis.PercentLabelClaim or ResultBasis.MgPerUnit))
                throw new InvalidOperationException($"Specification '{s.ParameterName}' has no valid titration result basis.");
        }
        bool needLoss = specs.Any(s => NeedLoss.Contains(s.ResultBasis!.Value));
        bool needUnitWt = specs.Any(s => NeedUnitWeight.Contains(s.ResultBasis!.Value));
        if (needLoss)
        {
            if (!p.LossOnDryingPercent.HasValue || p.LossOnDryingPercent < 0m || p.LossOnDryingPercent >= 100m)
                throw new InvalidOperationException("Loss on drying / water % (0 to below 100) is required for a dried or anhydrous basis.");
        }
        else if (p.LossOnDryingPercent.HasValue)
            throw new InvalidOperationException("Loss on drying / water % is only used with a dried or anhydrous basis.");
        if (needUnitWt)
        {
            if (!p.AverageUnitWeightMg.HasValue || p.AverageUnitWeightMg <= 0m)
                throw new InvalidOperationException("The average unit weight (mg) is required for a label claim or mg per unit basis.");
        }
        else if (p.AverageUnitWeightMg.HasValue)
            throw new InvalidOperationException("The average unit weight is only used with a label claim or mg per unit basis.");

        // ---- equipment type (the FP section check is in ValidateTestAnalysisOrderAsync) ----
        if (p.EquipmentId.HasValue)
        {
            var type = await _db.Equipment.Where(e => e.Id == p.EquipmentId.Value).Select(e => e.Type).FirstAsync();
            if (type is not (EquipmentType.Titrator or EquipmentType.KarlFischer or EquipmentType.Other))
                throw new InvalidOperationException("The selected equipment is not a titrator.");
        }

        // ---- titrant preparations ----
        var (titrantPrep, titrantOpt, titrantSnap) = await LoadPreparationAsync(
            p.TitrantPreparationId, cfg.TitrantMasterId, t.SectionId, cfg, cfg.TempCorrection, nowUtc, "Titrant");
        if (!titrantSnap.NominalStrength.HasValue || titrantSnap.NominalStrength <= 0m)
            throw new InvalidOperationException("The titrant preparation has no nominal strength recorded.");
        decimal n = titrantSnap.NominalStrength.Value;
        decimal f = titrantOpt.Factor ?? throw new InvalidOperationException("The titrant has no current factor.");

        (SolutionPreparation Prep, TitrantPreparationOption Opt, SolutionMasterResponse Snap)? excess = null;
        if (residual)
        {
            if (!p.ExcessPreparationId.HasValue) throw new InvalidOperationException("The excess titrant preparation is required for residual titration.");
            excess = await LoadPreparationAsync(p.ExcessPreparationId.Value, t.TitrationExcessSolutionMasterId!.Value,
                t.SectionId, cfg, false, nowUtc, "Excess titrant");
        }
        else if (p.ExcessPreparationId.HasValue)
            throw new InvalidOperationException("An excess titrant is only used in residual titration.");

        // ---- standards (relative) ----
        List<(decimal W, decimal Purity, decimal Moisture, decimal Titre)> stdRows = new();
        decimal? deductQuantity = null;
        Material? deductLot = null;
        if (relative)
        {
            if (p.Standards == null || p.Standards.Count < 1 || p.Standards.Count > 3)
                throw new InvalidOperationException("One to three standard titrations are required for the relative calculation.");
            if (p.Standards.Select(s => s.MaterialId).Distinct().Count() != 1)
                throw new InvalidOperationException("All standard titrations must use the same standard lot.");
            foreach (var s in p.Standards)
            {
                if (s.WeightMg <= 0m) throw new InvalidOperationException("Standard weight must be greater than zero.");
                if (s.TitreMl <= 0m) throw new InvalidOperationException("Standard titre must be greater than zero.");
            }
            deductLot = await _db.Materials.FirstOrDefaultAsync(m => m.Id == p.Standards[0].MaterialId)
                ?? throw new InvalidOperationException("Standard material not found.");
            if (deductLot.SectionId != t.SectionId)
                throw new InvalidOperationException("The standard lot belongs to another laboratory.");
            if (deductLot.MaterialType is not (MaterialType.ReferenceStandard or MaterialType.WorkingStandard))
                throw new InvalidOperationException("The standard lot must be a reference standard or an approved working standard.");
            if (!deductLot.Purity.HasValue)
                throw new InvalidOperationException($"Standard lot {deductLot.LotLabel} has no purity recorded.");
            decimal totalMg = p.Standards.Sum(s => s.WeightMg);
            deductQuantity = deductLot.Unit switch
            {
                MaterialUnit.Gram => totalMg / 1000m,
                MaterialUnit.Kilogram => totalMg / 1_000_000m,
                _ => throw new InvalidOperationException($"Lot {deductLot.LotLabel} is stocked in {deductLot.Unit}; a standard weight in mg cannot be deducted from it."),
            };
            var check = LotUsability.Check(deductLot, cfg.StandardEntryId!.Value, deductQuantity, today);
            if (!check.Usable) throw new InvalidOperationException($"Standard lot {deductLot.LotLabel}: {check.Reason}");
            foreach (var s in p.Standards)
                stdRows.Add((s.WeightMg, deductLot.Purity.Value, deductLot.MoisturePercent ?? 0m, s.TitreMl));
        }
        else if (p.Standards is { Count: > 0 })
            throw new InvalidOperationException("Standard titrations are only used with the relative calculation.");

        // ---- calculation ----
        decimal? tStd = cfg.TempCorrection ? titrantOpt.StandardizationTemperatureC : null;
        decimal coefficient = t.TitrationExpansionCoefficient ?? TitrationDefinitionRules.DefaultExpansionCoefficient;
        decimal Corr(decimal v) => cfg.TempCorrection
            ? TitrationEngine.CorrectVolume(v, tStd!.Value, p.TitrationTemperatureC!.Value, coefficient)
            : v;
        decimal blankC = Corr(blank);

        decimal? k = null;
        var kRows = new List<object>();
        if (relative)
        {
            var ks = new List<decimal>();
            foreach (var (w, purity, mc, titre) in stdRows)
            {
                decimal ks1 = TitrationEngine.StandardK(w, purity, mc, TitrationEngine.NetVolume(false, Corr(titre), blankC));
                ks.Add(ks1);
                kRows.Add(new { weightMg = w, titreMl = titre, correctedTitreMl = Corr(titre), k = ks1 });
            }
            k = ks.Average();
        }

        decimal MgFor(decimal vC)
        {
            if (relative) return TitrationEngine.MgRelative(TitrationEngine.NetVolume(false, vC, blankC), k!.Value);
            decimal fEq = t.TitrationEquivalencyFactor ?? 1m;
            if (residual && !cfg.BlankRequired)
                return TitrationEngine.MgResidualNoBlank(t.TitrationExcessVolumeMl!.Value, excess!.Value.Snap.NominalStrength!.Value,
                    excess.Value.Opt.Factor!.Value, vC, n, f, fEq);
            return TitrationEngine.MgDirect(TitrationEngine.NetVolume(residual, vC, blankC), n, f, kf ? 1m : fEq);
        }

        var mgs = new List<decimal>();
        var corrected = new List<decimal>();
        for (int i = 0; i < p.Replicates.Count; i++)
        {
            try
            {
                decimal vC = Corr(p.Replicates[i].TitrantVolumeMl);
                corrected.Add(vC);
                mgs.Add(MgFor(vC));
            }
            catch (InvalidOperationException ex) { throw new InvalidOperationException($"Replicate {i + 1}: {ex.Message}"); }
        }

        var warnings = new List<string>();
        if (titrantOpt.Warning != null) warnings.Add(titrantOpt.Warning);

        // ---- one ParameterResult per specification ----
        var parameterResults = new List<ParameterResult>();
        var perSpecJson = new List<object>();
        bool rsdFlag = false;
        foreach (var spec in specs)
        {
            decimal? labelClaim = NeedUnitWeight.Contains(spec.ResultBasis!.Value) ? spec.LabelClaim : null;
            var values = new List<decimal>();
            for (int i = 0; i < mgs.Count; i++)
            {
                try
                {
                    values.Add(TitrationEngine.ApplyBasis(mgs[i], p.Replicates[i].SampleWeightMg, spec.ResultBasis.Value,
                        p.LossOnDryingPercent, p.AverageUnitWeightMg, labelClaim));
                }
                catch (InvalidOperationException ex) { throw new InvalidOperationException($"{spec.ParameterName}: {ex.Message}"); }
            }

            var summary = TitrationEngine.Summarize(values, t.TitrationMaxRsdPercent);
            int decimals = Math.Max(2, new[] { spec.LowerLimit, spec.UpperLimit, spec.Target, spec.Tolerance }
                .Where(x => x.HasValue).Select(x => MeasurementCalculator.GetDecimalPlaces(x!.Value)).DefaultIfEmpty(0).Max());
            decimal reported = Math.Round(summary.Mean, decimals, MidpointRounding.AwayFromZero);
            var status = SpecificationEvaluator.Evaluate(spec, reported);
            if (summary.RsdExceeded)
            {
                rsdFlag = true;
                if (status == ResultStatus.WithinLimits) status = ResultStatus.RequiresReview;
            }

            var pr = new ParameterResult
            {
                TestOrderId = order.Id,
                SpecificationId = spec.Id,
                ParameterName = spec.ParameterName,
                ReportedValue = reported,
                ReportedDisplay = string.IsNullOrWhiteSpace(spec.Unit)
                    ? reported.ToString("F" + decimals, CultureInfo.InvariantCulture)
                    : $"{reported.ToString("F" + decimals, CultureInfo.InvariantCulture)} {spec.Unit.Trim()}",
                Unit = spec.Unit,
                SpecLimit = !string.IsNullOrWhiteSpace(spec.SpecLimit) ? spec.SpecLimit : SpecificationService.BuildCanonicalSpecLimit(spec),
                ResultBasis = spec.ResultBasis,
                ComparisonStatus = status,
                CalculationJson = JsonSerializer.Serialize(new
                {
                    mean = summary.Mean, rsd = summary.RsdPercent, n = values.Count,
                    maxRsdPercent = t.TitrationMaxRsdPercent, rsdExceeded = summary.RsdExceeded,
                    basis = spec.ResultBasis.ToString(), replicates = values,
                }, SnapshotJson.Options),
                IsActive = true,
            };
            for (int i = 0; i < values.Count; i++)
                pr.Readings.Add(new ResultReading
                {
                    Kind = ReadingKind.Titration, Index = i + 1,
                    Value1 = p.Replicates[i].SampleWeightMg, Value2 = p.Replicates[i].TitrantVolumeMl, Value3 = corrected[i],
                    ComputedValue = values[i],
                });
            parameterResults.Add(pr);
            perSpecJson.Add(new { specificationId = spec.Id, parameter = spec.ParameterName, basis = spec.ResultBasis.ToString(),
                mean = summary.Mean, rsdPercent = summary.RsdPercent, rsdExceeded = summary.RsdExceeded });
        }
        if (rsdFlag) warnings.Add($"RSD exceeds the configured maximum of {t.TitrationMaxRsdPercent:0.###} % - requires review.");

        // ---- snapshot (spec 3.4 + A6) ----
        var snapshot = new
        {
            type = cfg.Type, mode = cfg.Mode, calculation = cfg.Calculation, endpoint = cfg.Endpoint, indicator = t.TitrationIndicator,
            nonAqueous = cfg.NonAqueous,
            titrant = new
            {
                preparationId = titrantPrep.Id, code = titrantPrep.Code, masterName = titrantSnap.Name,
                nominalStrength = n, strengthUnit = titrantSnap.StrengthUnit,
                factorUsed = f, factorState = titrantOpt.FactorState,
                standardizationId = titrantOpt.StandardizationId, standardizedAt = titrantOpt.StandardizedAt,
                standardizationTemperatureC = titrantOpt.StandardizationTemperatureC,
            },
            equivalencyFactor = t.TitrationEquivalencyFactor,
            blankVolumeMl = p.BlankVolumeMl,
            excess = excess == null ? null : new
            {
                preparationId = excess.Value.Prep.Id, code = excess.Value.Prep.Code, masterName = excess.Value.Snap.Name,
                nominalStrength = excess.Value.Snap.NominalStrength, factor = excess.Value.Opt.Factor,
                standardizationId = excess.Value.Opt.StandardizationId, volumeMl = t.TitrationExcessVolumeMl,
            },
            temperatureCorrection = cfg.TempCorrection ? new
            {
                standardizationTemperatureC = tStd, titrationTemperatureC = p.TitrationTemperatureC, coefficient,
            } : null,
            lossOnDryingPercent = p.LossOnDryingPercent,
            averageUnitWeightMg = p.AverageUnitWeightMg,
            standard = deductLot == null ? null : new
            {
                lotId = deductLot.Id, lotLabel = deductLot.LotLabel, kind = deductLot.MaterialType.ToString(),
                purityPercent = deductLot.Purity, moisturePercent = deductLot.MoisturePercent,
                totalWeightMg = p.Standards!.Sum(s => s.WeightMg), entries = kRows, k,
            },
            replicateMg = mgs,
            results = perSpecJson,
            warnings,
        };
        var conditionsJson = JsonSerializer.Serialize(snapshot, SnapshotJson.Options);

        var lotToDeduct = deductLot;
        var quantityToDeduct = deductQuantity;
        return await PersistTestAnalysisAndFinalizeAsync(
            order, WorkflowType.Titration, p.EquipmentId, analysedAtUtc, conditionsJson, parameterResults,
            ResultType.Numeric, p.Password, p.Comment, "Titration", userId, ipAddress,
            afterSigned: lotToDeduct == null ? null : () =>
            {
                lotToDeduct.QuantityRemaining -= quantityToDeduct!.Value;
                lotToDeduct.LastModifiedByUserId = userId;
                lotToDeduct.LastModifiedAt = nowUtc;
            });
    }

    private async Task<(SolutionPreparation Prep, TitrantPreparationOption Opt, SolutionMasterResponse Snap)> LoadPreparationAsync(
        int preparationId, int expectedMasterId, int sectionId, TitrationConfig cfg, bool requireTemperature, DateTime nowUtc, string what)
    {
        var prep = await _db.SolutionPreparations.FirstOrDefaultAsync(x => x.Id == preparationId)
            ?? throw new InvalidOperationException($"{what} preparation not found.");
        if (prep.SectionId != sectionId)
            throw new InvalidOperationException($"{what} preparation belongs to another laboratory.");
        if (prep.SolutionMasterId != expectedMasterId)
            throw new InvalidOperationException($"{what} preparation is not of the solution configured on this test.");
        if (SolutionPreparationResponse.EffectiveStatusOf(prep, nowUtc) != SolutionPreparationStatus.Prepared)
            throw new InvalidOperationException($"{what} preparation {prep.Code} is expired or discarded.");

        var records = await _db.TitrantStandardizations.AsNoTracking().Where(x => x.SolutionPreparationId == prep.Id).ToListAsync();
        var opt = TitrationTitrants.Evaluate(prep, records, cfg.Calculation, requireTemperature, nowUtc, _clock.ToLabLocal, _clock.LabToday);
        if (!opt.Usable)
            throw new InvalidOperationException($"{what} preparation {prep.Code}: {opt.BlockReason}");

        var snap = JsonSerializer.Deserialize<SolutionMasterResponse>(prep.RecipeSnapshotJson, SnapshotJson.Options)
            ?? throw new InvalidOperationException($"{what} preparation's recipe snapshot could not be read.");
        return (prep, opt, snap);
    }
}
