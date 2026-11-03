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

    public OutsourcedRecorder(TestWorkflowDependencies deps) : base(deps) { }

    public Task OutsourceTestAsync(int testOrderId, OutsourceTestPayload payload, int userId, string? ipAddress = null) =>
        UnitOfWork.RunAsync(_db, () => OutsourceCoreAsync(testOrderId, payload, userId, ipAddress));

    private async Task OutsourceCoreAsync(int testOrderId, OutsourceTestPayload p, int userId, string? ipAddress)
    {
        var lab = p.ExternalLab?.Trim();
        if (string.IsNullOrEmpty(lab)) throw new InvalidOperationException("Enter the external laboratory.");
        if (lab.Length > 200) throw new InvalidOperationException("The external laboratory name cannot exceed 200 characters.");
        var reason = p.Reason?.Trim();
        if (string.IsNullOrEmpty(reason)) throw new InvalidOperationException("Enter the reason for outsourcing.");

        var order = await _db.TestOrders.Include(t => t.Sample).FirstOrDefaultAsync(t => t.Id == testOrderId)
            ?? throw new NotFoundException($"Test order {testOrderId} not found.");
        var definition = await _db.TestDefinitions.FirstOrDefaultAsync(d => d.Code == order.TestCode)
            ?? throw new InvalidOperationException($"Test definition \"{order.TestCode}\" not found.");

        if (!AnalysisWorkflows.UsesTestAnalysis(definition.WorkflowType))
            throw new InvalidOperationException("Only physicochemical tests can be outsourced.");
        await _sectionScope.EnsureTestOrderAccessAsync(userId, testOrderId);

        if (order.OutsourcedToLab != null)
            throw new InvalidOperationException($"This test is already outsourced to {order.OutsourcedToLab}.");
        if (order.IsSuperseded || order.Status is not (ApprovalStatus.Pending or ApprovalStatus.InProgress))
            throw new InvalidOperationException("Only a pending or in-progress test can be outsourced.");
        if (await _db.TestAnalyses.AnyAsync(a => a.TestOrderId == testOrderId && a.IsActive))
            throw new InvalidOperationException("This test already has a recorded result and cannot be outsourced.");

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
            ?? throw new InvalidOperationException($"Test definition \"{code}\" not found.");

        var (order, definition, specById, analysedAtUtc) = await ValidateTestAnalysisOrderAsync(
            testOrderId, p.ReportedAt, null, parameters.Select(x => x.SpecificationId).ToList(),
            p.Password, workflowType, userId, outsourced: true);

        // the specs of this test, each exactly once (the shared check does not cover every workflow type)
        if (parameters.Count != specById.Count || !specById.Keys.ToHashSet().SetEquals(parameters.Select(x => x.SpecificationId)))
            throw new InvalidOperationException("Every specification for this test must be supplied exactly once.");

        var number = p.ExternalReportNumber?.Trim();
        if (string.IsNullOrEmpty(number)) throw new InvalidOperationException("Enter the external report number.");
        if (number.Length > 100) throw new InvalidOperationException("The external report number cannot exceed 100 characters.");

        var report = await _db.SampleDocuments.FirstOrDefaultAsync(d => d.Id == p.ReportDocumentId)
            ?? throw new InvalidOperationException("The external lab report was not found. Upload it first.");
        if (report.DocumentType != SampleDocumentType.ExternalLabReport || report.SampleId != order.SampleId || report.TestOrderId != order.Id)
            throw new InvalidOperationException("The report is not an external lab report uploaded for this test.");
        if (report.Status != SampleDocumentStatus.Current)
            throw new InvalidOperationException("The external lab report has been voided. Upload the report again.");

        // a LOD / water result is what dried and anhydrous assays read: one numeric percentage
        if (definition.MoistureRole != MoistureRole.None
            && (parameters.Count != 1 || parameters[0].Value is not { } pct || pct < 0m || pct >= 100m))
            throw new InvalidOperationException("A loss on drying or water result must be one numeric percentage from 0 to below 100.");

        var results = new List<ParameterResult>();
        foreach (var spec in specById.Values.OrderBy(s => s.DisplayOrder).ThenBy(s => s.Id))
        {
            var input = parameters.First(x => x.SpecificationId == spec.Id);
            ParameterResult pr;
            if (spec.LimitType is LimitType.Range or LimitType.NotMoreThan or LimitType.NotLessThan)
            {
                if (input.Value is not { } entered || !string.IsNullOrWhiteSpace(input.Text))
                    throw new InvalidOperationException($"Enter a numeric value for \"{spec.ParameterName}\".");
                int decimals = Math.Max(2, new[] { spec.LowerLimit, spec.UpperLimit }
                    .Where(x => x.HasValue).Select(x => MeasurementCalculator.GetSpecDecimalPlaces(x!.Value)).DefaultIfEmpty(0).Max());
                var reported = Math.Round(entered, decimals, MidpointRounding.AwayFromZero);
                pr = NewResult(order, spec, reported, GravimetricCalculator.FormatReportedDisplay(reported, spec),
                    SpecificationEvaluator.Evaluate(spec, reported), spec.ResultBasis, p, number, entered);
                pr.Readings.Add(new ResultReading { Kind = ReadingKind.Replicate, Index = 1, Value1 = entered, ComputedValue = reported });
            }
            else if (spec.LimitType is LimitType.Qualitative or LimitType.PresenceAbsence)
            {
                if (string.IsNullOrWhiteSpace(input.Text) || input.Value.HasValue)
                    throw new InvalidOperationException($"Enter the reported text for \"{spec.ParameterName}\".");
                if (input.Text.Length > 500) throw new InvalidOperationException("Reported text cannot exceed 500 characters.");
                var conforms = SingleResultRecorder.JudgeQualitativeText(spec, input.Text);
                var (display, status, limit) = SingleResultRecorder.BuildQualitativeOutcome(spec, conforms);
                pr = NewResult(order, spec, null, display, status, null, p, number, null);
                pr.SpecLimit = limit;
                pr.Readings.Add(new ResultReading { Kind = ReadingKind.Replicate, Index = 1, Text = input.Text.Trim(), Passed = conforms });
            }
            else
                throw new InvalidOperationException($"Specification \"{spec.ParameterName}\" ({spec.LimitType}) cannot be reported from an external lab.");
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
