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

        var cal = c.Run.Calibration!;
        var def = c.Definition;

        var parameterResults = new List<ParameterResult>();
        foreach (var row in c.EvaluateRows())
        {
            var element = def.IcpElements.First(e => e.Id == row.ElementId);
            var spec = row.Spec;
            var conc = c.RunSample.Concentrations.First(x => x.IcpTestElementId == element.Id);
            var assay = row.Mode == IcpTestMode.Assay;

            var calculationJson = JsonSerializer.Serialize(new
            {
                element = element.Symbol,
                icpTestElementId = element.Id,
                quantity = assay ? "IcpAssayPercent" : "IcpImpurityPpm",
                mode = row.Mode.ToString(),
                sampleForm = assay ? def.IcpSampleForm?.ToString() : null,
                targetPpm = assay ? element.TargetPpm : null,
                theoreticalAmount = assay ? def.IcpTheoreticalAmount : null,
                specificGravity = assay && def.IcpSampleForm == IcpSampleForm.Liquid ? def.IcpSpecificGravity : null,
                actualWeightG = assay ? c.RunSample.ActualWeightG : null,
                resultPpm = conc.ResultPpm,
                reportedValue = row.Value,
                display = row.Display,
                runCode = c.Run.Code,
                calibrationCode = cal.Code,
                calibrationConfirmedAt = cal.ConfirmedAt,
                correlationR = cal.Elements.FirstOrDefault(e => string.Equals(e.Symbol, element.Symbol, StringComparison.OrdinalIgnoreCase))?.CorrelationR,
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
                ResultBasis = null,
                ComparisonStatus = row.Status!.Value,
                OverRange = false,
                BelowLoq = false,
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
