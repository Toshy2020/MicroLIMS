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

        var inputs = c.BuildAnalyteInputs();
        var rows = HplcSampleAssayEvaluator.Evaluate(inputs, c.StageRole);
        var individual = HplcAssayCalculator.IsIndividualBasis(c.StageRole);
        var sst = c.Run.Sst!;

        var parameterResults = new List<ParameterResult>();
        foreach (var row in rows)
        {
            var input = inputs.First(i => i.AnalyteId == row.AnalyteId);
            var calculationJson = JsonSerializer.Serialize(new
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
                }),
                reportedValue = row.Value,
                runCode = c.Run.Code,
                sstCode = sst.Code,
                hplcMethodId = c.Run.HplcMethodId,
            }, JsonOptions);

            var parameterResult = new ParameterResult
            {
                TestOrderId = order.Id,
                SpecificationId = row.Spec.Id,
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
            foreach (var reading in row.Readings)
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
            parameterResults.Add(parameterResult);
        }

        return await PersistTestAnalysisAndFinalizeAsync(
            order,
            WorkflowType.HplcMethodAssay,
            c.Run.EquipmentId,
            analysedAtUtc,
            null,
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
}
