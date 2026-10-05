using System.Text.Json;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Workflows;

// ICP Workspace "Send for Review": turns one run sample's replicates into signed ParameterResults through the
// same PersistTestAnalysisAndFinalizeAsync the other analysis recorders use, so review, approval, summary and
// CoA work unchanged. Rows come from IcpSampleEntryContext so what is stored is exactly what was previewed.
public sealed class IcpMethodAssayRecorder : TestWorkflowSupport
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public IcpMethodAssayRecorder(TestWorkflowDependencies deps) : base(deps) { }

    public Task<TestWorkflowResult> SubmitAsync(int runSampleId, string password, string? comment, int userId, string? ipAddress = null) =>
        UnitOfWork.RunAsync(_db, () => SubmitCoreAsync(runSampleId, password, comment, userId, ipAddress));

    private async Task<TestWorkflowResult> SubmitCoreAsync(int runSampleId, string password, string? comment, int userId, string? ipAddress)
    {
        var c = await IcpSampleEntryContext.LoadAsync(_db, runSampleId, default, _clock);
        await _sectionScope.EnsureTestOrderAccessAsync(userId, c.Order.Id);

        var problem = c.SubmitProblem();
        if (problem != null)
            throw new InvalidOperationException(problem);

        var (order, _, _, analysedAtUtc) = await ValidateTestAnalysisOrderAsync(
            c.Order.Id, _clock.UtcNow.UtcDateTime, null, null, password, WorkflowType.IcpMethodAssay, userId);

        var levels = CalibrationStandardLevelsHelper.ParseAndValidate(c.Snapshot.StandardLevelsMgPerL).Levels;
        var cal = c.Run.Calibration!;
        var unitAmount = c.RunSample.UnitAmount;

        var parameterResults = new List<ParameterResult>();
        foreach (var row in c.EvaluateRows())
        {
            var element = c.Snapshot.Elements.First(e => e.Id == row.ElementId);
            var spec = row.Spec;
            var reps = c.RunSample.Replicates.OrderBy(r => r.ReplicateNo).ToList();
            var calc = IcpContentCalculator.Calculate(
                reps.Select(r => new IcpReplicateInput(r.ReplicateNo, r.SampleAmount, r.VolumeMl, r.DilutionFactor,
                    r.Concentrations.First(x => x.IcpMethodElementId == element.Id).SolutionMgPerL)).ToList(),
                levels.Min(), levels.Max(), element.ConversionFactor, unitAmount, spec.LabelClaim);

            var calculationJson = JsonSerializer.Serialize(new
            {
                element = element.Symbol,
                icpMethodElementId = element.Id,
                quantity = row.Basis switch
                {
                    ResultBasis.MgPerUnit => "IcpMgPerUnit",
                    ResultBasis.PercentLabelClaim => "IcpPercentLabelClaim",
                    _ => "IcpMgPerKg",
                },
                amountUnit = c.RunSample.AmountUnit.ToString(),
                unitAmount,
                conversionFactor = element.ConversionFactor,
                labelClaim = spec.LabelClaim,
                labelClaimUnit = spec.LabelClaimUnit,
                standardLevelsMgPerL = levels,
                replicates = reps.Select(r =>
                {
                    var content = calc.Replicates.First(x => x.ReplicateNo == r.ReplicateNo);
                    return new
                    {
                        replicateNo = r.ReplicateNo,
                        sampleAmount = r.SampleAmount,
                        volumeMl = r.VolumeMl,
                        dilutionFactor = r.DilutionFactor,
                        solutionMgPerL = r.Concentrations.First(x => x.IcpMethodElementId == element.Id).SolutionMgPerL,
                        contentPerAmount = content.ContentPerAmount,
                        belowLoq = content.BelowLoq,
                    };
                }),
                meanContentPerAmount = calc.MeanContentPerAmount,
                reportedValue = row.Value,
                display = row.Display,
                runCode = c.Run.Code,
                calibrationCode = cal.Code,
                calibrationConfirmedAt = cal.ConfirmedAt,
                correlationR = cal.Elements.FirstOrDefault(e => e.IcpMethodElementId == element.Id)?.CorrelationR,
                ccv = c.Run.CcvReadings.Where(r => r.IcpMethodElementId == element.Id).OrderBy(r => r.EnteredAt).Select(r => new
                {
                    measuredMgPerL = r.MeasuredMgPerL,
                    recoveryPercent = r.RecoveryPercent,
                    passed = r.Passed,
                    enteredAt = r.EnteredAt,
                }),
                icpMethodId = c.Run.IcpMethodId,
                icpRunId = c.Run.Id,
                icpRunSampleId = c.RunSample.Id,
            }, JsonOptions);

            parameterResults.Add(new ParameterResult
            {
                TestOrderId = order.Id,
                SpecificationId = spec.Id,
                ParameterName = !string.IsNullOrWhiteSpace(spec.ParameterName) ? spec.ParameterName : row.Symbol,
                ReportedValue = row.Value,
                ReportedDisplay = row.Display,
                Unit = row.Unit,
                SpecLimit = !string.IsNullOrWhiteSpace(spec.SpecLimit) ? spec.SpecLimit : SpecificationService.BuildCanonicalSpecLimit(spec),
                ResultBasis = row.Basis,
                ComparisonStatus = row.Status!.Value,
                OverRange = false,
                BelowLoq = calc.AllBelowLoq,
                CalculationJson = calculationJson,
                IsActive = true,
            });
        }

        return await PersistTestAnalysisAndFinalizeAsync(
            order,
            WorkflowType.IcpMethodAssay,
            c.Run.EquipmentId,
            analysedAtUtc,
            null,
            parameterResults,
            ResultType.Numeric,
            password,
            comment,
            "icp method assay",
            userId,
            ipAddress,
            validityRecordType: nameof(IcpCalibration),
            validityRecordId: cal.Id);
    }
}
