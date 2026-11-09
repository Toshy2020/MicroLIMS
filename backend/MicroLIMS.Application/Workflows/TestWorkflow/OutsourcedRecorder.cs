using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Workflows;

// A physicochemical test sent to an outside laboratory. Outsourcing is a signed decision (TestOutsourced);
// the result is then entered from the external lab's report (a Current ExternalLabReport document of this
// test order) as an ordinary TestAnalysis + ParameterResult, so review, approval, the CoA and the
// LOD / water moisture lookup see it like any other result. Nothing is measured here, so no equipment.
public sealed class OutsourcedRecorder : TestWorkflowSupport
{
    public const string EngineVersion = "outsourced-1";
    public const string OutsourcedActionCode = "TestOrder.Outsourced";

    // What an external lab report can carry: a number judged against a limit, or the analyst's conforms decision.
    // The outsource gate and the record loop both read these, so they cannot drift.
    private static readonly HashSet<LimitType> NumericLimitTypes = new() { LimitType.Range, LimitType.NotMoreThan, LimitType.NotLessThan };
    private static readonly HashSet<LimitType> QualitativeLimitTypes = new() { LimitType.Qualitative, LimitType.PresenceAbsence };
    private static bool IsReportable(LimitType type) => NumericLimitTypes.Contains(type) || QualitativeLimitTypes.Contains(type);

    public OutsourcedRecorder(TestWorkflowDependencies deps) : base(deps) { }

    public Task OutsourceTestAsync(int testOrderId, OutsourceTestPayload payload, int userId, string? ipAddress = null) =>
        UnitOfWork.RunAsync(_db, () => OutsourceCoreAsync(testOrderId, payload, userId, ipAddress));

    private async Task OutsourceCoreAsync(int testOrderId, OutsourceTestPayload p, int userId, string? ipAddress)
    {
        var lab = p.ExternalLab?.Trim();
        if (string.IsNullOrEmpty(lab)) throw new BusinessRuleException("Enter the external laboratory.");
        if (lab.Length > 200) throw new BusinessRuleException("The external laboratory name cannot exceed 200 characters.");
        var reason = p.Reason?.Trim();
        if (string.IsNullOrEmpty(reason)) throw new BusinessRuleException("Enter the reason for outsourcing.");

        var order = await _db.TestOrders.Include(t => t.Sample).FirstOrDefaultAsync(t => t.Id == testOrderId)
            ?? throw new NotFoundException($"Test order {testOrderId} not found.");
        var definition = await _db.TestDefinitions.FirstOrDefaultAsync(d => d.Code == order.TestCode)
            ?? throw new BusinessRuleException($"Test definition \"{order.TestCode}\" not found.");

        if (!AnalysisWorkflows.UsesTestAnalysis(definition.WorkflowType))
            throw new BusinessRuleException("Only physicochemical tests can be outsourced.");
        await _sectionScope.EnsureTestOrderAccessAsync(userId, testOrderId);

        if (order.OutsourcedToLab != null)
            throw new BusinessRuleException($"This test is already outsourced to {order.OutsourcedToLab}.");
        if (order.IsSuperseded || order.Status is not (ApprovalStatus.Pending or ApprovalStatus.InProgress))
            throw new BusinessRuleException("Only a pending or in-progress test can be outsourced.");
        if (await _db.TestAnalyses.AnyAsync(a => a.TestOrderId == testOrderId))
            throw new BusinessRuleException("This test already has an in-house result; it cannot be outsourced.");

        // the signed decision is irreversible and every ordinary recorder is refused afterwards, so the
        // specifications must all be ones an external lab report can carry
        if (order.Sample?.ItemId is not int itemId)
            throw new BusinessRuleException("Test order sample must be linked to an item with specifications.");
        var specs = await SpecificationLookup.ForSampleAsync(_db, order.SampleId, itemId, order.TestCode);
        if (specs.Count == 0)
            throw new BusinessRuleException("No specifications configured for this test and item.");
        var unreportable = specs.FirstOrDefault(s => !IsReportable(s.LimitType));
        if (unreportable != null)
            throw new BusinessRuleException(
                $"Specification \"{unreportable.ParameterName}\" ({unreportable.LimitType}) cannot be reported from an external lab; this test cannot be outsourced.");

        _db.CurrentUserId = userId;
        await _signatureService.SignAsync(userId, p.Password, SignatureMeaning.TestOutsourced, "TestOrder", order.Id, reason, ipAddress);

        order.OutsourcedToLab = lab;
        order.OutsourcedAt = _clock.UtcNow.UtcDateTime;
        order.OutsourcedByUserId = userId;

        // saved with the order change below: one commit for the change and its audit entry
        _db.AuditLogs.Add(new AuditLog
        {
            EntityName = "TestOrder",
            EntityId = order.Id.ToString(),
            Action = OutsourcedActionCode,
            ActionCode = OutsourcedActionCode,
            ActionCategory = AuditActionCategory.Other,
            ActorType = ActorType.User,
            UserId = userId,
            SampleId = order.SampleId,
            TestOrderId = order.Id,
            SampleReferenceNumber = order.Sample?.ReferenceNumber,
            Reason = reason,
            NewValue = $"Outsourced to {lab}",
        });
        await _db.SaveChangesAsync();
    }

    public Task<TestWorkflowResult> RecordOutsourcedResultAsync(
        int testOrderId, RecordOutsourcedResultPayload payload, int userId, string? ipAddress = null) =>
        UnitOfWork.RunAsync(_db, () => RecordCoreAsync(testOrderId, payload, userId, ipAddress));

    private async Task<TestWorkflowResult> RecordCoreAsync(int testOrderId, RecordOutsourcedResultPayload p, int userId, string? ipAddress)
    {
        var parameters = p.Parameters ?? new List<OutsourcedParameterInput>();

        // the test's own workflow type is what the result is stored as
        var code = await _db.TestOrders.Where(t => t.Id == testOrderId).Select(t => t.TestCode).FirstOrDefaultAsync()
            ?? throw new NotFoundException($"Test order {testOrderId} not found.");
        var workflowType = await _db.TestDefinitions.Where(d => d.Code == code).Select(d => (WorkflowType?)d.WorkflowType).FirstOrDefaultAsync()
            ?? throw new BusinessRuleException($"Test definition \"{code}\" not found.");

        var (order, definition, specById, analysedAtUtc) = await ValidateTestAnalysisOrderAsync(
            testOrderId, p.ReportedAt, null, parameters.Select(x => x.SpecificationId).ToList(),
            p.Password, workflowType, userId, outsourced: true);

        // the specs of this test, each exactly once (the shared check does not cover every workflow type)
        if (parameters.Count != specById.Count || !specById.Keys.ToHashSet().SetEquals(parameters.Select(x => x.SpecificationId)))
            throw new BusinessRuleException("Every specification for this test must be supplied exactly once.");

        var number = p.ExternalReportNumber?.Trim();
        if (string.IsNullOrEmpty(number)) throw new BusinessRuleException("Enter the external report number.");
        if (number.Length > 100) throw new BusinessRuleException("The external report number cannot exceed 100 characters.");

        var report = await _db.SampleDocuments.FirstOrDefaultAsync(d => d.Id == p.ReportDocumentId)
            ?? throw new BusinessRuleException("The external lab report was not found. Upload it first.");
        if (report.DocumentType != SampleDocumentType.ExternalLabReport || report.SampleId != order.SampleId || report.TestOrderId != order.Id)
            throw new BusinessRuleException("The report is not an external lab report uploaded for this test.");
        if (report.Status != SampleDocumentStatus.Current)
            throw new BusinessRuleException("The external lab report has been voided. Upload the report again.");

        // a LOD / water result is what dried and anhydrous assays read: one numeric percentage
        if (definition.MoistureRole != MoistureRole.None
            && (parameters.Count != 1 || parameters[0].Value is not { } pct || pct < 0m || pct >= 100m))
            throw new BusinessRuleException("A loss on drying or water result must be one numeric percentage from 0 to below 100.");

        var results = new List<ParameterResult>();
        foreach (var spec in specById.Values.OrderBy(s => s.DisplayOrder).ThenBy(s => s.Id))
        {
            var input = parameters.First(x => x.SpecificationId == spec.Id);
            ParameterResult pr;
            if (NumericLimitTypes.Contains(spec.LimitType))
            {
                if (input.Value is not { } entered || input.Conforms.HasValue || !string.IsNullOrWhiteSpace(input.Observation))
                    throw new BusinessRuleException($"Enter a numeric value for \"{spec.ParameterName}\".");
                int decimals = Math.Max(2, new[] { spec.LowerLimit, spec.UpperLimit }
                    .Where(x => x.HasValue).Select(x => MeasurementCalculator.GetSpecDecimalPlaces(x!.Value)).DefaultIfEmpty(0).Max());
                var reported = Math.Round(entered, decimals, MidpointRounding.AwayFromZero);
                var display = GravimetricCalculator.FormatReportedDisplay(reported, spec);
                // the helper defaults an empty unit to "%", which a unitless result (pH) does not have
                if (string.IsNullOrWhiteSpace(spec.Unit) && display.EndsWith(" %")) display = display[..^2];
                pr = NewResult(order, spec, reported, display,
                    SpecificationEvaluator.Evaluate(spec, reported), spec.ResultBasis, p, number, entered);
                pr.Readings.Add(new ResultReading { Kind = ReadingKind.Replicate, Index = 1, Value1 = entered, ComputedValue = reported });
            }
            else if (QualitativeLimitTypes.Contains(spec.LimitType))
            {
                // the analyst decides conformity, as in-house entry; the lab's wording is kept as the observation
                if (input.Value.HasValue || input.Conforms is not { } conforms)
                    throw new BusinessRuleException($"Record whether \"{spec.ParameterName}\" conforms.");
                var observation = input.Observation?.Trim();
                if (!conforms && string.IsNullOrEmpty(observation))
                    throw new BusinessRuleException("An observation is required when result does not conform.");
                if (observation is { Length: > 500 }) throw new BusinessRuleException("Observation cannot exceed 500 characters.");
                var (display, status, limit) = SingleResultRecorder.BuildQualitativeOutcome(spec, conforms);
                pr = NewResult(order, spec, null, display, status, null, p, number, null);
                pr.SpecLimit = limit;
                pr.Readings.Add(new ResultReading { Kind = ReadingKind.Replicate, Index = 1, Text = observation, Passed = conforms });
            }
            else
                throw new BusinessRuleException($"Specification \"{spec.ParameterName}\" ({spec.LimitType}) cannot be reported from an external lab.");
            results.Add(pr);
        }

        return await PersistTestAnalysisAndFinalizeAsync(
            order, workflowType, null, analysedAtUtc, null, results,
            results.All(r => r.ReportedValue.HasValue) ? ResultType.Numeric : ResultType.Interpretive,
            p.Password, p.Comment, "External lab result", userId, ipAddress);
    }

    private static ParameterResult NewResult(TestOrder order, Specification spec, decimal? value, string display, ResultStatus status,
        ResultBasis? basis, RecordOutsourcedResultPayload p, string reportNumber, decimal? enteredValue) => new()
    {
        TestOrderId = order.Id,
        SpecificationId = spec.Id,
        ParameterName = spec.ParameterName,
        ReportedValue = value,
        ReportedDisplay = display,
        Unit = spec.Unit,
        SpecLimit = !string.IsNullOrWhiteSpace(spec.SpecLimit) ? spec.SpecLimit : SpecificationService.BuildCanonicalSpecLimit(spec),
        ResultBasis = basis,
        ComparisonStatus = status,
        IsActive = true,
        CalculationJson = JsonSerializer.Serialize(new
        {
            engineVersion = EngineVersion,
            quantity = "Outsourced",
            externalLab = order.OutsourcedToLab,
            externalReportNumber = reportNumber,
            reportDocumentId = p.ReportDocumentId,
            reportedAt = p.ReportedAt,
            enteredValue,
        }, SnapshotJson.Options),
    };
}
