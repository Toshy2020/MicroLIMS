using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Validators;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Services;

/// <summary>
/// Receives a batch of 1–100 samples of a single category all-or-nothing,
/// validating all rows up front and reusing the existing single-row services
/// inside one outer transaction.
/// </summary>
public class SampleBatchReceiveService
{
    public const int MaxBatchRows = 100;

    private readonly IMicroLimsDbContext _db;
    private readonly IReceivingService _receivingService;
    private readonly ReceiveSampleValidator _itemValidator;
    private readonly ReceiptLabService _receiptLabs;
    private readonly WaterService _waterService;
    private readonly EMService _emService;
    private readonly AfterCleaningService _afterCleaningService;
    private readonly IUserSectionScopeService _scope;

    public SampleBatchReceiveService(
        IMicroLimsDbContext db,
        IReceivingService receivingService,
        ReceiveSampleValidator itemValidator,
        ReceiptLabService receiptLabs,
        WaterService waterService,
        EMService emService,
        AfterCleaningService afterCleaningService,
        IUserSectionScopeService scope)
    {
        _db = db;
        _receivingService = receivingService;
        _itemValidator = itemValidator;
        _receiptLabs = receiptLabs;
        _waterService = waterService;
        _emService = emService;
        _afterCleaningService = afterCleaningService;
        _scope = scope;
    }

    public Task<BatchReceiveResult> ReceiveAsync(BatchReceiveRequest request, int userId, CancellationToken ct = default) =>
        ReceiveAsync(request, userId, canReceiveForAnyLab: false, ct);

    public async Task<BatchReceiveResult> ReceiveAsync(
        BatchReceiveRequest request,
        int userId,
        bool canReceiveForAnyLab,
        CancellationToken ct = default)
    {
        var (category, rowCount, listField) = ValidateRequestShape(request);
        var errors = new List<FieldError>();
        IReadOnlyCollection<int>?[]? resolvedItemTargets = null;

        switch (category)
        {
            case "Product":
            {
                var rows = request.Items!;
                resolvedItemTargets = new IReadOnlyCollection<int>?[rows.Count];
                for (var i = 0; i < rows.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var row = rows[i];
                    errors.AddRange(_itemValidator.ValidateFields(ToItemBasedRequest(row, userId, null), i));
                    resolvedItemTargets[i] = await TryResolveItemTargetsAsync(userId, canReceiveForAnyLab, row.TargetSectionIds, i, errors);
                }
                break;
            }
            case "Water":
            {
                var rows = request.Water!;
                for (var i = 0; i < rows.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var row = rows[i];
                    errors.AddRange(_waterService.Validate(ToWaterRequest(row, userId), i));
                    await TryValidateSectionScopeAsync(userId, row.LabSectionId, i, errors, ct);
                }
                break;
            }
            case "EM":
            {
                var rows = request.Em!;
                for (var i = 0; i < rows.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    errors.AddRange(_emService.Validate(ToEMRequest(rows[i], userId), i));
                }
                break;
            }
            case "AfterCleaning":
            {
                var rows = request.AfterCleaning!;
                for (var i = 0; i < rows.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var row = rows[i];
                    errors.AddRange(_afterCleaningService.Validate(ToAfterCleaningRequest(row, userId), i));
                    await TryValidateSectionScopeAsync(userId, row.LabSectionId, i, errors, ct);
                }
                break;
            }
        }

        if (errors.Count > 0)
            throw new FieldValidationException("Some rows need correction.", errors);

        return await UnitOfWork.RunAsync(_db, async () =>
        {
            var samples = new List<BatchReceivedSample>(rowCount);
            for (var i = 0; i < rowCount; i++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var dto = category switch
                    {
                        "Product" => await _receivingService.ReceiveSampleAsync(
                            ToItemBasedRequest(request.Items![i], userId, resolvedItemTargets![i])),
                        "Water" => await _waterService.ReceiveAsync(ToWaterRequest(request.Water![i], userId)),
                        "EM" => await _emService.ReceiveAsync(ToEMRequest(request.Em![i], userId)),
                        "AfterCleaning" => await _afterCleaningService.ReceiveAsync(
                            ToAfterCleaningRequest(request.AfterCleaning![i], userId)),
                        _ => throw new BusinessRuleException($"Unsupported category '{category}'.")
                    };
                    samples.Add(new BatchReceivedSample(i, dto.Id, dto.ReferenceNumber));
                }
                catch (BusinessRuleException ex)
                {
                    throw new FieldValidationException(ex.Message, new[] { new FieldError(string.Empty, ex.Message, i) });
                }
            }

            return new BatchReceiveResult(samples);
        });
    }

    private static (string Category, int RowCount, string ListField) ValidateRequestShape(BatchReceiveRequest request)
    {
        if (request is null)
            throw new FieldValidationException("Request is required.", new[] { new FieldError("category", "Request is required.") });

        var provided = new List<(string Category, string Field, int Count)>(4);
        if (request.Items is not null) provided.Add(("Product", "items", request.Items.Count));
        if (request.Water is not null) provided.Add(("Water", "water", request.Water.Count));
        if (request.Em is not null) provided.Add(("EM", "em", request.Em.Count));
        if (request.AfterCleaning is not null) provided.Add(("AfterCleaning", "afterCleaning", request.AfterCleaning.Count));

        if (provided.Count != 1)
            throw new FieldValidationException(
                "Exactly one sample list must be provided.",
                new[] { new FieldError("category", "Exactly one sample list must be provided.") });

        var (expectedCategory, listField, count) = provided[0];
        if (!string.Equals(request.Category, expectedCategory, StringComparison.Ordinal))
            throw new FieldValidationException(
                "Category does not match the provided sample list.",
                new[] { new FieldError("category", "Category does not match the provided sample list.") });

        if (count == 0)
            throw new FieldValidationException(
                "At least one sample row is required.",
                new[] { new FieldError(listField, "At least one sample row is required.") });

        if (count > MaxBatchRows)
            throw new FieldValidationException(
                $"A batch may contain at most {MaxBatchRows} rows.",
                new[] { new FieldError(listField, $"A batch may contain at most {MaxBatchRows} rows.") });

        return (expectedCategory, count, listField);
    }

    private async Task<IReadOnlyCollection<int>?> TryResolveItemTargetsAsync(
        int userId,
        bool canReceiveForAnyLab,
        IReadOnlyCollection<int>? requested,
        int row,
        List<FieldError> errors)
    {
        try
        {
            return await _receiptLabs.ResolveTargetsAsync(userId, canReceiveForAnyLab, requested);
        }
        catch (Exception ex) when (ex is BusinessRuleException or UnauthorizedAccessException)
        {
            errors.Add(new FieldError("targetSectionIds", ex.Message, row));
            return null;
        }
    }

    private async Task TryValidateSectionScopeAsync(
        int userId,
        int? labSectionId,
        int row,
        List<FieldError> errors,
        CancellationToken ct)
    {
        // The service's own ReceiveAsync checks scope again inside the transaction; this reports it per row up front.
        try
        {
            await _scope.ResolveSectionForCreateAsync(userId, labSectionId, ct);
        }
        catch (BusinessRuleException ex)
        {
            errors.Add(new FieldError("labSectionId", ex.Message, row));
        }
    }

    private static ItemBasedReceiveRequest ToItemBasedRequest(
        ReceiveItemBasedSampleRequest r, int userId, IReadOnlyCollection<int>? targets) =>
        new(r.ItemId, r.CauseOfTestingId, r.SampleQuantity, r.SampledBy,
            r.BatchNumber, r.ControlNumber, r.MfgDate, r.ExpDate,
            r.ProductionStage, userId, targets, r.Grade);

    private static WaterReceiveRequest ToWaterRequest(ReceiveWaterRequest r, int userId) =>
        new(r.WaterDepartmentId, r.CauseOfTestingId, r.SampleQuantity, r.SampledBy,
            r.ControlNumber, userId, r.SampledAt, r.LabSectionId);

    private static EMReceiveRequest ToEMRequest(ReceiveEMRequest r, int userId) =>
        new(r.DepartmentId, r.CauseOfTestingId, r.SampledBy, r.ControlNumber, userId, r.SampledAt);

    private static AfterCleaningReceiveRequest ToAfterCleaningRequest(ReceiveAfterCleaningRequest r, int userId) =>
        new(r.MachineId, r.CauseOfTestingId, r.SampledBy, r.ControlNumber,
            userId, r.PreviousProductName, r.PreviousProductBatchNumber,
            r.SampledAt, r.LabSectionId, r.PreviousProductItemId, r.Techniques);
}
