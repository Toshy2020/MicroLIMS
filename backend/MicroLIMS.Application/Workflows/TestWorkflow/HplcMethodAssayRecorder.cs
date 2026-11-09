using System.Text.Json;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Workflows;

// HPLC Workspace "Send for Review" (HPLC chain S6, Part B): turns one run
// sample's replicates into signed ParameterResults through the same
// PersistTestAnalysisAndFinalizeAsync the other analysis recorders use, so
// review, approval, summary and CoA work unchanged.
public sealed class HplcMethodAssayRecorder : TestWorkflowSupport
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public HplcMethodAssayRecorder(TestWorkflowDependencies deps) : base(deps) { }

    public Task<TestWorkflowResult> SubmitAsync(int runSampleId, string password, string? comment, int userId, string? ipAddress = null) =>
        UnitOfWork.RunAsync(_db, () => SubmitCoreAsync(runSampleId, password, comment, userId, ipAddress));

    private async Task<TestWorkflowResult> SubmitCoreAsync(int runSampleId, string password, string? comment, int userId, string? ipAddress)
    {
        var c = await HplcSampleEntryContext.LoadAsync(_db, runSampleId);
        await _sectionScope.EnsureTestOrderAccessAsync(userId, c.Order.Id);

        var problem = c.SubmitProblem();
        if (problem != null)
            throw new InvalidOperationException(problem);

        var (order, _, _, analysedAtUtc) = await ValidateTestAnalysisOrderAsync(
            c.Order.Id, _clock.UtcNow.UtcDateTime, null, null, password, WorkflowType.HplcMethodAssay, userId);

        SampleMoisture? moisture = null;
        if (c.Snapshot.ResultMode == HplcResultMode.Assay && c.MoistureRole != MoistureRole.None)
        {
            moisture = await new SampleMoistureResolver(_db).ResolveLockedAsync(c.Order.SampleId, c.MoistureRole)
                ?? throw new InvalidOperationException(SampleMoistureResolver.MissingMessage(c.MoistureRole));
        }

        var relatedSubstances = c.IsRelatedSubstances ? c.CalculateRelatedSubstances() : new List<RsRowResult>();
        var rows = c.IsRelatedSubstances ? relatedSubstances.Select(r => r.Row).ToList() : c.EvaluateRows(moisture?.Percent);
        var inputs = c.Snapshot.ResultMode == HplcResultMode.Assay ? c.BuildAnalyteInputs(moisture?.Percent) : new List<HplcAssayAnalyteInput>();
        var rsInputs = c.IsResidualSolvents ? c.BuildResidualSolventInputs() : new List<HplcResidualSolventInput>();
        var crInputs = c.IsCleaningResidue ? c.BuildCleaningResidueInputs() : new List<HplcCleaningResidueInput>();
        var individual = HplcAssayCalculator.IsIndividualBasis(c.StageRole);
        var sst = c.Run.Sst!;

        // Content-basis rows: enough to reproduce the reported value (as-is value, moisture used, basis).
        (string? ResultBasis, decimal? Moisture, decimal? AsIs) ContentFields(HplcAssayRow row, HplcAssayAnalyteInput input)
        {
            if (row.Basis is not (ResultBasis.PercentAsIs or ResultBasis.PercentDriedBasis or ResultBasis.PercentAnhydrousBasis))
                return (null, null, null);
            var asIs = individual
                ? row.Readings.Single().AssayPercent
                : HplcAssayCalculator.Calculate(input.AnalyteId, input.Name, input.ThWtStdMg, input.ThWtTestMg, input.Std, input.Reps).MeanAssayPercent;
            return (row.Basis.ToString(), input.MoisturePercent, asIs);
        }

        var parameterResults = new List<ParameterResult>();
        foreach (var row in rows)
        {
            string calculationJson;
            if (c.IsRelatedSubstances)
                calculationJson = RelatedSubstancesJson(c, relatedSubstances.First(x => ReferenceEquals(x.Row, row)));
            else if (c.IsResidualSolvents)
            {
                var rs = rsInputs.First(i => i.AnalyteId == row.AnalyteId);
                calculationJson = JsonSerializer.Serialize(new
                {
                    analyte = rs.Name,
                    hplcMethodAnalyteId = rs.AnalyteId,
                    quantity = "ResidualSolventPpm",
                    basis = "Mean",
                    standardConcentrationUgPerMl = rs.StandardConcentrationUgPerMl,
                    sampleSolutionVolumeMl = rs.SampleSolutionVolumeMl,
                    standardMeanResponse = rs.StandardMeanResponse,
                    replicates = row.Readings.Select(r => new
                    {
                        replicateNo = r.ReplicateNo,
                        sampleWeightMg = rs.Reps.First(x => x.ReplicateNo == r.ReplicateNo).SampleWeightMg,
                        response = r.Response,
                        ppm = r.AssayPercent,
                        ppmDisplay = ResidualSolventCalculator.FormatPpm(r.AssayPercent),
                    }),
                    reportedValue = row.Value,
                    notDetected = HplcResidualSolventEvaluator.IsNotDetected(rs),
                    runCode = c.Run.Code,
                    sstCode = sst.Code,
                    hplcMethodId = c.Run.HplcMethodId,
                    hplcRunId = c.Run.Id,
                    hplcRunSampleId = c.RunSample.Id,
                }, JsonOptions);
            }
            else if (c.IsCleaningResidue)
            {
                var cr = crInputs.First(i => i.AnalyteId == row.AnalyteId);
                calculationJson = JsonSerializer.Serialize(new
                {
                    analyte = cr.Name,
                    hplcMethodAnalyteId = cr.AnalyteId,
                    quantity = "CleaningResidue",
                    basis = "Mean",
                    standardConcentrationUgPerMl = cr.StandardConcentrationUgPerMl,
                    sampleSolutionVolumeMl = cr.SampleSolutionVolumeMl,
                    swabAreaCm2 = cr.SwabAreaCm2,
                    macoLimit = cr.MacoLimit,
                    macoUnit = cr.MacoUnit.ToString(),
                    standardMeanResponse = cr.StandardMeanResponse,
                    replicates = row.Readings.Select(r => new
                    {
                        replicateNo = r.ReplicateNo,
                        response = r.Response,
                        value = r.AssayPercent,
                        display = CleaningResidueCalculator.FormatValue(r.AssayPercent, cr.MacoUnit),
                    }),
                    reportedValue = row.Value,
                    notDetected = CleaningResidueCalculator.IsNotDetected(cr),
                    runCode = c.Run.Code,
                    sstCode = sst.Code,
                    hplcMethodId = c.Run.HplcMethodId,
                    hplcRunId = c.Run.Id,
                    hplcRunSampleId = c.RunSample.Id,
                }, JsonOptions);
            }
            else if (!string.IsNullOrWhiteSpace(c.Snapshot.InternalStandardName))
            {
                var input = inputs.First(i => i.AnalyteId == row.AnalyteId);
                var content = ContentFields(row, input);
                calculationJson = JsonSerializer.Serialize(new
                {
                    analyte = input.Name,
                    hplcMethodAnalyteId = input.AnalyteId,
                    internalStandardName = c.Snapshot.InternalStandardName,
                    quantity = row.Basis == ResultBasis.MgPerUnit ? "AmountPerUnit" : "AssayPercent",
                    basis = individual ? "Individual" : "Mean",
                    replicateNo = row.ReplicateNo,
                    thWtStdMg = input.ThWtStdMg,
                    thWtTestMg = input.ThWtTestMg,
                    standardWeightMg = input.Std.StandardWeightMg,
                    standardPurityPercent = input.Std.PurityPercent,
                    standardMoisturePercent = input.Std.MoisturePercent,
                    standardMeanRatio = input.Std.MeanResponse,
                    labelClaim = row.Basis == ResultBasis.MgPerUnit ? row.Spec.LabelClaim : null,
                    replicates = row.Readings.Select(r =>
                    {
                        var rep = c.RunSample.Replicates.First(x => x.ReplicateNo == r.ReplicateNo);
                        return new
                        {
                            replicateNo = r.ReplicateNo,
                            actualWeightMg = rep.ActualWeightMg,
                            response = rep.Responses.First(x => x.HplcMethodAnalyteId == row.AnalyteId).Response,
                            internalStandardResponse = rep.InternalStandardResponse,
                            ratio = r.Response,
                            assayPercent = r.AssayPercent,
                            assayPercentDisplay = HplcSampleAssayEvaluator.FormatPercent(r.AssayPercent),
                        };
                    }),
                    resultBasis = content.ResultBasis,
                    moisturePercent = content.Moisture,
                    asIsValue = content.AsIs,
                    reportedValue = row.Value,
                    runCode = c.Run.Code,
                    sstCode = sst.Code,
                    hplcMethodId = c.Run.HplcMethodId,
                    hplcRunId = c.Run.Id,
                    hplcRunSampleId = c.RunSample.Id,
                }, JsonOptions);
            }
            else
            {
                var input = inputs.First(i => i.AnalyteId == row.AnalyteId);
                var content = ContentFields(row, input);
                calculationJson = JsonSerializer.Serialize(new
                {
                    analyte = input.Name,
                    hplcMethodAnalyteId = input.AnalyteId,
                    quantity = row.Basis == ResultBasis.MgPerUnit ? "AmountPerUnit" : "AssayPercent",
                    basis = individual ? "Individual" : "Mean",
                    replicateNo = row.ReplicateNo,
                    thWtStdMg = input.ThWtStdMg,
                    thWtTestMg = input.ThWtTestMg,
                    standardWeightMg = input.Std.StandardWeightMg,
                    standardPurityPercent = input.Std.PurityPercent,
                    standardMoisturePercent = input.Std.MoisturePercent,
                    standardMeanResponse = input.Std.MeanResponse,
                    labelClaim = row.Basis == ResultBasis.MgPerUnit ? row.Spec.LabelClaim : null,
                    replicates = row.Readings.Select(r => new
                    {
                        replicateNo = r.ReplicateNo,
                        actualWeightMg = input.Reps.First(x => x.ReplicateNo == r.ReplicateNo).ActualWeightMg,
                        response = r.Response,
                        assayPercent = r.AssayPercent,
                        assayPercentDisplay = HplcSampleAssayEvaluator.FormatPercent(r.AssayPercent),
                    }),
                    resultBasis = content.ResultBasis,
                    moisturePercent = content.Moisture,
                    asIsValue = content.AsIs,
                    reportedValue = row.Value,
                    runCode = c.Run.Code,
                    sstCode = sst.Code,
                    hplcMethodId = c.Run.HplcMethodId,
                    hplcRunId = c.Run.Id,
                    hplcRunSampleId = c.RunSample.Id,
                }, JsonOptions);
            }

            var parameterResult = new ParameterResult
            {
                TestOrderId = order.Id,
                SpecificationId = row.Spec.Id == 0 ? null : row.Spec.Id,
                ParameterName = row.AnalyteName,
                ReportedValue = row.Value,
                ReportedDisplay = row.Display,
                Unit = row.Unit,
                SpecLimit = !string.IsNullOrWhiteSpace(row.Spec.SpecLimit) ? row.Spec.SpecLimit : SpecificationService.BuildCanonicalSpecLimit(row.Spec),
                ResultBasis = null,
                ComparisonStatus = row.Status,
                OverRange = false,
                BelowLoq = false,
                CalculationJson = calculationJson,
                IsActive = true,
            };
            foreach (var reading in c.IsRelatedSubstances ? new List<HplcAssayReading>() : row.Readings)
            {
                parameterResult.Readings.Add(new ResultReading
                {
                    Kind = ReadingKind.Replicate,
                    Stage = reading.ReplicateNo,
                    Index = 1,
                    Value1 = reading.Response,
                    ComputedValue = reading.AssayPercent,
                    Passed = true,
                });
            }
            if (c.IsRelatedSubstances)
                AddContributionReadings(parameterResult, relatedSubstances.First(x => ReferenceEquals(x.Row, row)));
            parameterResults.Add(parameterResult);
        }

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
        }, JsonOptions);

        return await PersistTestAnalysisAndFinalizeAsync(
            order,
            WorkflowType.HplcMethodAssay,
            c.Run.EquipmentId,
            analysedAtUtc,
            conditionsJson,
            parameterResults,
            ResultType.Numeric,
            password,
            comment,
            "hplc method assay",
            userId,
            ipAddress,
            validityRecordType: nameof(HplcSstRecord),
            validityRecordId: sst.Id);
    }

    // What the related-substance row was calculated from: the reference solution, and per replicate every
    // entered peak (including disregarded ones, carrying impurity id or unspecified, RRT, area, pct, and disregarded: true/false).
    private static string RelatedSubstancesJson(HplcSampleEntryContext c, RsRowResult rs)
    {
        var analyte = c.Snapshot.Analytes.First();
        return JsonSerializer.Serialize(new
        {
            analyte = analyte.Name,
            hplcMethodAnalyteId = analyte.Id,
            parameter = rs.Row.AnalyteName,
            quantity = "RelatedSubstance",
            kind = rs.Kind.ToString(),
            impurityId = rs.ImpurityId,
            referenceSolutionPercent = c.Snapshot.ReferenceSolutionPercent,
            disregardLimitPercent = c.Snapshot.DisregardLimitPercent,
            replicates = rs.Replicates.Select(d => new
            {
                replicateNo = d.ReplicateNo,
                referenceArea = d.ReferenceArea,
                percent = d.Percent,
                peaks = d.Peaks.Select(p => new
                {
                    impurityId = p.ImpurityId,
                    label = p.Label,
                    relativeRetention = p.RelativeRetention,
                    area = p.Area,
                    correctionFactor = p.CorrectionFactor,
                    percent = p.Percent,
                    disregarded = p.Disregarded,
                }),
            }),
            reportedValue = rs.Row.Value,
            notDetected = rs.Row.Display == RelatedSubstancesEvaluator.NotDetected,
            runCode = c.Run.Code,
            sstCode = c.Run.Sst!.Code,
            hplcMethodId = c.Run.HplcMethodId,
            hplcRunId = c.Run.Id,
            hplcRunSampleId = c.RunSample.Id,
        }, JsonOptions);
    }

    // One reading per counted peak: the peak area, its % and a text naming it.
    private static void AddContributionReadings(ParameterResult parameterResult, RsRowResult rs)
    {
        foreach (var replicate in rs.Replicates)
        {
            var index = 0;
            foreach (var peak in replicate.Contributions)
            {
                parameterResult.Readings.Add(new ResultReading
                {
                    Kind = ReadingKind.Replicate,
                    Stage = replicate.ReplicateNo,
                    Index = ++index,
                    Value1 = peak.Area,
                    ComputedValue = peak.Percent,
                    Text = $"{peak.Label}: {peak.Percent.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)}",
                    Passed = true,
                });
            }
        }
    }
}
