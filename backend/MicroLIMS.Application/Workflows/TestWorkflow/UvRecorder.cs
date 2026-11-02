using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Workflows;

// UV spectrophotometry (raw-material STMs): a signed absorbance result stored as TestAnalysis +
// ParameterResult, like titration and gravimetric. The method constants (A1%, total volume, standard
// volume) come from the Test Master and are copied into each result's CalculationJson, so later edits
// never change a signed result. Dried / anhydrous basis reads the sample's own LOD / water result.
public sealed class UvRecorder : TestWorkflowSupport
{
    public const string EngineVersion = "uv-1";

    public const decimal MinWavelengthNm = 190m;
    public const decimal MaxWavelengthNm = 1100m;
    public const decimal MaxAbsorbance = 5m;

    private static readonly ResultBasis?[] AllowedBases =
        { null, ResultBasis.PercentAsIs, ResultBasis.PercentDriedBasis, ResultBasis.PercentAnhydrousBasis };

    public UvRecorder(TestWorkflowDependencies deps) : base(deps) { }

    public Task<TestWorkflowResult> RecordUvResultAsync(
        int testOrderId, UvPayload payload, int userId, string? ipAddress = null) =>
        UnitOfWork.RunAsync(_db, () => RecordCoreAsync(testOrderId, payload, userId, ipAddress));

    private async Task<TestWorkflowResult> RecordCoreAsync(int testOrderId, UvPayload p, int userId, string? ipAddress)
    {
        var parameters = p.Parameters ?? new List<UvParameterInput>();
        var (order, t, specById, analysedAtUtc) = await ValidateTestAnalysisOrderAsync(
            testOrderId, p.AnalysedAt, p.EquipmentId, parameters.Select(x => x.SpecificationId).ToList(),
            p.Password, WorkflowType.UvSpectrophotometry, userId);

        // ---- test constants (old rows are read defensively; the Test Master refuses these on save) ----
        if (t.EquationType != EquationType.UvSpectrophotometry)
            throw new InvalidOperationException("Test definition equation type must be UvSpectrophotometry.");
        var mode = t.UvCalculationMode ?? throw new InvalidOperationException("A UV test needs a calculation mode.");
        if (!t.ReplicateCount.HasValue || t.ReplicateCount.Value < 1 || t.ReplicateCount.Value > 30)
            throw new InvalidOperationException("Test definition does not have a valid replicate count (1-30).");
        int replicateCount = t.ReplicateCount.Value;

        bool limitMode = mode == UvCalculationMode.AbsorbanceLimit;
        bool standardMode = mode == UvCalculationMode.StandardComparison;
        decimal? a1 = null, volume = null, stdVolume = null;
        if (mode == UvCalculationMode.SpecificAbsorbanceAssay)
        {
            if (!(t.UvSpecificAbsorbance > 0m)) throw new InvalidOperationException("This UV mode needs a specific absorbance above 0.");
            a1 = t.UvSpecificAbsorbance;
        }
        if (!limitMode)
        {
            if (!(t.UvTotalVolumeMl > 0m)) throw new InvalidOperationException("This UV mode needs a total volume above 0.");
            volume = t.UvTotalVolumeMl;
        }
        if (standardMode)
        {
            if (!(t.UvStandardVolumeMl > 0m)) throw new InvalidOperationException("A UV test against a standard needs a standard volume above 0.");
            stdVolume = t.UvStandardVolumeMl;
        }

        // ---- request shape ----
        if (!p.EquipmentId.HasValue)
            throw new InvalidOperationException("Select the spectrophotometer used.");
        var equipmentType = await _db.Equipment.Where(e => e.Id == p.EquipmentId.Value).Select(e => e.Type).FirstAsync();
        if (equipmentType is not (EquipmentType.UvVis or EquipmentType.Other))
            throw new InvalidOperationException("The selected equipment is not a UV spectrophotometer.");

        foreach (var param in parameters)
        {
            if (param.WavelengthNm < MinWavelengthNm || param.WavelengthNm > MaxWavelengthNm)
                throw new InvalidOperationException($"Wavelength must be between {MinWavelengthNm:0} and {MaxWavelengthNm:0} nm.");
            if (param.Replicates == null || param.Replicates.Count != replicateCount)
                throw new InvalidOperationException($"Each parameter must have exactly {replicateCount} replicate(s).");

            for (int i = 0; i < param.Replicates.Count; i++)
            {
                var rep = param.Replicates[i];
                bool aboveZero = limitMode ? rep.Absorbance >= 0m : rep.Absorbance > 0m;
                if (!aboveZero || rep.Absorbance > MaxAbsorbance)
                    throw new InvalidOperationException(limitMode
                        ? "Absorbance must be at least 0 and at most 5."
                        : "Absorbance must be above 0 and at most 5.");
                if (limitMode)
                {
                    if (rep.WeightG.HasValue)
                        throw new InvalidOperationException($"Replicate {i + 1}: a sample weight is not used in an absorbance limit test.");
                }
                else if (!(rep.WeightG > 0m))
                    throw new InvalidOperationException($"Replicate {i + 1}: sample weight must be above 0 g.");
            }

            if (limitMode)
            {
                if (!(param.ReferenceAbsorbance > 0m) || param.ReferenceAbsorbance > MaxAbsorbance)
                    throw new InvalidOperationException("A reference absorbance above 0 and at most 5 is required for each parameter.");
            }
            else if (param.ReferenceAbsorbance.HasValue)
                throw new InvalidOperationException("A reference absorbance is only used in an absorbance limit test.");
        }

        if (standardMode)
        {
            if (!p.StandardMaterialId.HasValue) throw new InvalidOperationException("Select the standard lot.");
            if (!(p.StandardWeightG > 0m)) throw new InvalidOperationException("Standard weight must be above 0 g.");
            if (!(p.StandardAbsorbance > 0m) || p.StandardAbsorbance > MaxAbsorbance)
                throw new InvalidOperationException("Standard absorbance must be above 0 and at most 5.");
        }
        else if (p.StandardMaterialId.HasValue || p.StandardWeightG.HasValue || p.StandardAbsorbance.HasValue)
            throw new InvalidOperationException("A standard is only used when the test compares against a standard.");

        // ---- specifications ----
        var specs = specById.Values.OrderBy(s => s.DisplayOrder).ThenBy(s => s.Id).ToList();
        foreach (var s in specs)
        {
            if (s.HplcMethodAnalyteId.HasValue || s.IcpMethodElementId.HasValue || s.RelatedSubstanceRow.HasValue)
                throw new InvalidOperationException($"Specification '{s.ParameterName}' cannot be used by a UV test.");
            if (!AllowedBases.Contains(s.ResultBasis))
                throw new InvalidOperationException($"Specification '{s.ParameterName}' has no valid UV result basis.");
            if (limitMode)
            {
                if (s.LimitType != LimitType.Qualitative)
                    throw new InvalidOperationException($"Specification '{s.ParameterName}' must have the Qualitative limit type for an absorbance limit test.");
                if (s.ResultBasis.HasValue)
                    throw new InvalidOperationException($"Specification '{s.ParameterName}': a result basis is not used in an absorbance limit test.");
            }
            else if (s.LimitType is not (LimitType.Range or LimitType.NotMoreThan or LimitType.NotLessThan))
                throw new InvalidOperationException($"Specification '{s.ParameterName}' must have a Range, NotMoreThan or NotLessThan limit.");
        }

        var moistureRole = SampleMoistureResolver.NeededRole(specs.Select(s => s.ResultBasis), "UV test");
        SampleMoisture? moisture = null;
        if (moistureRole != MoistureRole.None)
            moisture = await new SampleMoistureResolver(_db).ResolveLockedAsync(order.SampleId, moistureRole)
                ?? throw new InvalidOperationException(SampleMoistureResolver.MissingMessage(moistureRole));

        // ---- standard lot: the same usability rules as the standards picker, enforced at record time ----
        Material? lot = null;
        if (standardMode)
        {
            lot = await _db.Materials.FirstOrDefaultAsync(m => m.Id == p.StandardMaterialId!.Value)
                ?? throw new InvalidOperationException("Standard material not found.");
            var accessible = await _sectionScope.GetAccessibleSectionIdsAsync(userId);
            if (accessible != null && !accessible.Contains(lot.SectionId))
                throw new InvalidOperationException($"Standard lot {lot.LotLabel} belongs to another laboratory.");
            if (lot.MaterialType is not (MaterialType.ReferenceStandard or MaterialType.WorkingStandard))
                throw new InvalidOperationException("The standard lot must be a reference standard or an approved working standard.");
            if (lot.ExpiryDate.HasValue && DateOnly.FromDateTime(lot.ExpiryDate.Value) < _clock.LabToday)
                throw new InvalidOperationException($"Standard lot {lot.LotLabel} expired on {lot.ExpiryDate:yyyy-MM-dd}.");
            if (lot.QuantityRemaining <= 0m)
                throw new InvalidOperationException($"Standard lot {lot.LotLabel} has no stock left.");
            if (!(lot.Purity > 0m))
                throw new InvalidOperationException($"Standard lot {lot.LotLabel} has no purity recorded.");
        }

        // ---- one ParameterResult per specification ----
        var inputBySpec = parameters.ToDictionary(x => x.SpecificationId);
        var parameterResults = new List<ParameterResult>();
        foreach (var spec in specs)
        {
            var input = inputBySpec[spec.Id];
            var values = new List<decimal>();
            for (int i = 0; i < input.Replicates.Count; i++)
            {
                var rep = input.Replicates[i];
                values.Add(mode switch
                {
                    UvCalculationMode.SpecificAbsorbanceAssay => UvCalculator.SpecificAbsorbanceAssay(rep.Absorbance, volume!.Value, a1!.Value, rep.WeightG!.Value),
                    UvCalculationMode.SpecificAbsorbanceValue => UvCalculator.SpecificAbsorbanceValue(rep.Absorbance, volume!.Value, rep.WeightG!.Value),
                    UvCalculationMode.StandardComparison => UvCalculator.StandardComparison(
                        rep.Absorbance, p.StandardAbsorbance!.Value, p.StandardWeightG!.Value, stdVolume!.Value, volume!.Value,
                        rep.WeightG!.Value, lot!.Purity!.Value, lot.MoisturePercent ?? 0m),
                    _ => rep.Absorbance,
                });
            }
            decimal asIs = values.Average();

            decimal reported;
            string display;
            ResultStatus status;
            decimal? moistureUsed = null;
            if (limitMode)
            {
                // Absorbances are read to three decimals; the mean is judged as it is shown.
                reported = Math.Round(asIs, 3, MidpointRounding.AwayFromZero);
                display = $"{reported.ToString("0.000", CultureInfo.InvariantCulture)} {(string.IsNullOrWhiteSpace(spec.Unit) ? "AU" : spec.Unit.Trim())}";
                status = UvCalculator.WithinReference(reported, input.ReferenceAbsorbance!.Value)
                    ? ResultStatus.WithinLimits : ResultStatus.OutOfSpecification;
            }
            else
            {
                if (spec.ResultBasis is ResultBasis.PercentDriedBasis or ResultBasis.PercentAnhydrousBasis) moistureUsed = moisture!.Percent;
                int decimals = Math.Max(2, new[] { spec.LowerLimit, spec.UpperLimit }
                    .Where(x => x.HasValue).Select(x => MeasurementCalculator.GetSpecDecimalPlaces(x!.Value)).DefaultIfEmpty(0).Max());
                reported = Math.Round(HplcAssayCalculator.ApplyBasis(asIs, spec.ResultBasis, moistureUsed), decimals, MidpointRounding.AwayFromZero);
                display = GravimetricCalculator.FormatReportedDisplay(reported, spec);
                status = SpecificationEvaluator.Evaluate(spec, reported);
            }

            var pr = new ParameterResult
            {
                TestOrderId = order.Id,
                SpecificationId = spec.Id,
                ParameterName = spec.ParameterName,
                ReportedValue = reported,
                ReportedDisplay = display,
                Unit = spec.Unit,
                SpecLimit = !string.IsNullOrWhiteSpace(spec.SpecLimit) ? spec.SpecLimit : SpecificationService.BuildCanonicalSpecLimit(spec),
                ResultBasis = spec.ResultBasis,
                ComparisonStatus = status,
                CalculationJson = JsonSerializer.Serialize(new
                {
                    engineVersion = EngineVersion,
                    quantity = "Uv",
                    mode = mode.ToString(),
                    wavelengthNm = input.WavelengthNm,
                    totalVolumeMl = volume,
                    specificAbsorbance = a1,
                    standard = lot == null ? null : new
                    {
                        lotId = lot.Id, code = lot.LotLabel, purityPercent = lot.Purity, moisturePercent = lot.MoisturePercent ?? 0m,
                        weightG = p.StandardWeightG, absorbance = p.StandardAbsorbance, volumeMl = stdVolume,
                    },
                    referenceAbsorbance = input.ReferenceAbsorbance,
                    resultBasis = spec.ResultBasis?.ToString(),
                    asIsValue = asIs,
                    moisturePercent = moistureUsed,
                    replicates = input.Replicates.Select((r, i) => new { replicateNo = i + 1, weightG = r.WeightG, absorbance = r.Absorbance, value = values[i] }),
                    reportedValue = reported,
                }, SnapshotJson.Options),
                IsActive = true,
            };
            for (int i = 0; i < values.Count; i++)
                pr.Readings.Add(new ResultReading
                {
                    Kind = ReadingKind.Absorbance, Index = i + 1,
                    Value1 = input.Replicates[i].WeightG, Value2 = input.Replicates[i].Absorbance, Value3 = input.WavelengthNm,
                    ComputedValue = values[i],
                });
            parameterResults.Add(pr);
        }

        // The cascade (SampleReviewService.FindDependentAssaysAsync) finds this entry by parameterResultId.
        string? conditionsJson = moisture == null ? null : JsonSerializer.Serialize(new
        {
            moisture = new
            {
                parameterResultId = moisture.ParameterResultId,
                testOrderId = moisture.TestOrderId,
                percent = moisture.Percent,
                role = moisture.Role.ToString(),
                testCode = moisture.TestCode,
            }
        }, SnapshotJson.Options);

        return await PersistTestAnalysisAndFinalizeAsync(
            order, WorkflowType.UvSpectrophotometry, p.EquipmentId, analysedAtUtc, conditionsJson, parameterResults,
            ResultType.Numeric, p.Password, p.Comment, "UV spectrophotometry", userId, ipAddress);
    }
}
