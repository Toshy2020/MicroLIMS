using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Services.MasterData;

// Master data: specification. Behind SpecificationMasterDataController
// (api/masterdata/...); the controller only maps HTTP to these calls.
public class SpecificationMasterDataService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly SpecificationService _specificationService;
    private readonly MasterDataBatchService _batch;
    private readonly ChangeReasonGuard _reasonGuard;
    private readonly TimeProvider _time;

    public SpecificationMasterDataService(
        IMicroLimsDbContext db,
        IUserSectionScopeService scope,
        SpecificationService? specificationService = null,
        MasterDataBatchService? batchService = null,
        ChangeReasonGuard? reasonGuard = null,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _scope = scope;
        _specificationService = specificationService ?? new SpecificationService(db);
        _batch = batchService ?? new MasterDataBatchService(db);
        _reasonGuard = reasonGuard ?? new ChangeReasonGuard(db, new MasterDataUsageService(db, scope));
        _time = timeProvider ?? TimeProvider.System;
    }

    public async Task<object> GetSpecificationsAsync(int currentUserId, int itemId) =>
        await ToRowsAsync(currentUserId, await _specificationService.GetForItemAsync(itemId));

    public async Task<object> GetSpecificationsForTestOrderAsync(int currentUserId, int testOrderId)
    {
        var order = await _db.TestOrders
            .Where(o => o.Id == testOrderId)
            .Select(o => new { o.SampleId, o.TestCode, o.Sample!.ItemId })
            .FirstOrDefaultAsync()
            ?? throw new NotFoundException($"Test order {testOrderId} not found.");
        if (order.ItemId is not int itemId)
            return new List<SpecificationRowDto>();

        return await ToRowsAsync(currentUserId, await SpecificationLookup.ForSampleAsync(_db, order.SampleId, itemId, order.TestCode));
    }

    private async Task<List<SpecificationRowDto>> ToRowsAsync(int currentUserId, List<Specification> specs)
    {
        var scope = await _scope.GetConfigurableSectionIdsAsync(currentUserId);

        var testCodes = specs.Select(s => s.TestCode).Distinct().ToList();
        var testDefs = await _db.TestDefinitions
            .AsNoTracking()
            .Include(t => t.Section)
            .Where(t => testCodes.Contains(t.Code))
            .ToListAsync();
        var byCode = testDefs.ToDictionary(t => t.Code);

        var rows = specs.Select(s =>
        {
            byCode.TryGetValue(s.TestCode, out var def);
            var canEdit = scope is null || (def != null && scope.Contains(def.SectionId));
            return new SpecificationRowDto(
                s.Id, s.ItemId, s.TestCode, s.AlertLimit, s.ActionLimit, s.SpecLimit, s.Unit, s.DilutionFactor,
                s.ParameterName, s.DisplayOrder, s.LimitType, s.ReferenceStandard, s.LowerLimit, s.UpperLimit,
                s.LowerInclusive, s.UpperInclusive, s.Target, s.Tolerance, s.ToleranceMode, s.ExpectedResultText,
                s.ExpectedState, s.SampleQuantity, s.SampleQuantityUnit, s.ResultBasis,
                s.LabelClaim, s.LabelClaimUnit, s.DosageForm, s.Stages.Select(SpecificationStageResponse.From).ToList(),
                canEdit, def?.Section?.Name ?? string.Empty, s.HplcMethodAnalyteId, s.ProductionStageRole, s.IcpTestElementId, s.Grade, s.RelatedSubstanceRow, s.HplcMethodImpurityId)
            {
                Version = s.Version,
                IsRetired = s.IsRetired,
                RetiredAt = s.RetiredAt,
                RetiredByUserId = s.RetiredByUserId
            };
        }).ToList();

        return rows;
    }

    public async Task<SpecificationResponse> CreateSpecificationAsync(int currentUserId, CreateSpecificationRequest request)
    {
        await SpecificationOwnership.EnsureCanEditAsync(_db, _scope, currentUserId, request.TestCode);

        var limitType = request.LimitType ?? LimitType.CountTiered;
        var paramName = request.ParameterName;
        if (string.IsNullOrWhiteSpace(paramName) && request.RelatedSubstanceRow is RelatedSubstanceRow kind && Enum.IsDefined(kind))
        {
            var impurityName = request.HplcMethodImpurityId is int impurityId
                ? await _db.HplcMethodImpurities.Where(i => i.Id == impurityId).Select(i => i.Name).FirstOrDefaultAsync()
                : null;
            paramName = SpecificationService.RelatedSubstanceParameterName(kind, impurityName);
        }
        if (string.IsNullOrWhiteSpace(paramName))
        {
            var testDef = await _db.TestDefinitions.FirstOrDefaultAsync(t => t.Code == request.TestCode);
            paramName = !string.IsNullOrWhiteSpace(testDef?.DisplayName) ? testDef.DisplayName : request.TestCode;
        }

        var spec = new Specification
        {
            ItemId = request.ItemId,
            TestCode = request.TestCode,
            ParameterName = paramName,
            DisplayOrder = request.DisplayOrder ?? 0,
            LimitType = limitType,
            ReferenceStandard = request.ReferenceStandard,
            LowerLimit = request.LowerLimit,
            UpperLimit = request.UpperLimit,
            LowerInclusive = request.LowerInclusive ?? true,
            UpperInclusive = request.UpperInclusive ?? true,
            Target = request.Target,
            Tolerance = request.Tolerance,
            ToleranceMode = request.ToleranceMode,
            ExpectedResultText = request.ExpectedResultText,
            ExpectedState = request.ExpectedState,
            SampleQuantity = request.SampleQuantity,
            SampleQuantityUnit = request.SampleQuantityUnit,
            AlertLimit = request.AlertLimit ?? string.Empty,
            ActionLimit = request.ActionLimit ?? string.Empty,
            SpecLimit = request.SpecLimit ?? string.Empty,
            Unit = request.Unit ?? string.Empty,
            DilutionFactor = request.DilutionFactor,
            ResultBasis = request.ResultBasis,
            LabelClaim = request.LabelClaim,
            LabelClaimUnit = request.LabelClaimUnit,
            DosageForm = request.DosageForm,
            HplcMethodAnalyteId = request.HplcMethodAnalyteId,
            RelatedSubstanceRow = request.RelatedSubstanceRow,
            HplcMethodImpurityId = request.HplcMethodImpurityId,
            IcpTestElementId = request.IcpTestElementId,
            ProductionStageRole = request.ProductionStageRole,
            Grade = request.Grade,
            Stages = request.Stages?.Select(s => new SpecificationStage
            {
                StageNumber = s.StageNumber,
                StageLabel = s.StageLabel,
                AcceptanceCriteriaText = s.AcceptanceCriteriaText
            }).ToList() ?? new List<SpecificationStage>()
        };

        SpecificationService.ApplyCanonicalSpecLimit(spec);
        await _specificationService.ValidateAsync(spec);

        _db.Specifications.Add(spec);
        await _db.SaveChangesAsync();
        return SpecificationResponse.From(spec);
    }

    public Task<IReadOnlyList<SpecificationResponse>> CreateSpecificationsBatchAsync(
        int currentUserId,
        IReadOnlyList<CreateSpecificationRequest>? requests,
        CancellationToken ct = default) =>
        _batch.ExecuteAsync(
            requests,
            (row, index, token) => ValidateCreateSpecificationRowAsync(currentUserId, row, index, token),
            row => CreateSpecificationAsync(currentUserId, row),
            ct: ct);

    private async Task<IReadOnlyList<FieldError>> ValidateCreateSpecificationRowAsync(
        int currentUserId,
        CreateSpecificationRequest request,
        int row,
        CancellationToken ct)
    {
        var errors = new List<FieldError>();

        if (request.ItemId <= 0 || !await _db.Items.AsNoTracking().AnyAsync(i => i.Id == request.ItemId, ct))
        {
            errors.Add(new FieldError("itemId", $"Item {request.ItemId} not found.", row));
        }

        if (string.IsNullOrWhiteSpace(request.TestCode))
        {
            errors.Add(new FieldError("testCode", "Test code is required.", row));
        }
        else
        {
            try
            {
                await SpecificationOwnership.EnsureCanEditAsync(_db, _scope, currentUserId, request.TestCode);
            }
            catch (Exception ex) when (ex is BusinessRuleException or UnauthorizedAccessException)
            {
                errors.Add(new FieldError("testCode", ex.Message, row));
            }
        }

        return errors;
    }

    public async Task<SpecificationResponse> UpdateSpecificationAsync(
        int currentUserId,
        int id,
        UpdateSpecificationRequest request,
        string? changeReason = null,
        CancellationToken cancellationToken = default)
    {
        var spec = await _db.Specifications.Include(s => s.Stages).FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Specification {id} not found.");
        RecordVersion.EnsureCurrent(_db, spec);

        if (spec.IsRetired)
            throw new BusinessRuleException("Retired specifications cannot be edited; reinstate the specification first.");

        // Check both the row's current lab and the lab it would move to -
        // a Section Head may not reassign a row into or out of their lab
        // by changing TestCode either.
        await SpecificationOwnership.EnsureCanEditAsync(_db, _scope, currentUserId, spec.TestCode);
        await SpecificationOwnership.EnsureCanEditAsync(_db, _scope, currentUserId, request.TestCode);
        await _reasonGuard.EnsureAsync(MasterRecordType.Specification, id, ChangeKind.Edit, changeReason, currentUserId, cancellationToken);

        spec.TestCode = request.TestCode;
        if (request.ParameterName != null)
            spec.ParameterName = request.ParameterName;
        if (request.DisplayOrder.HasValue)
            spec.DisplayOrder = request.DisplayOrder.Value;
        if (request.LimitType.HasValue)
            spec.LimitType = request.LimitType.Value;
        spec.ReferenceStandard = request.ReferenceStandard;
        spec.LowerLimit = request.LowerLimit;
        spec.UpperLimit = request.UpperLimit;
        if (request.LowerInclusive.HasValue)
            spec.LowerInclusive = request.LowerInclusive.Value;
        if (request.UpperInclusive.HasValue)
            spec.UpperInclusive = request.UpperInclusive.Value;
        spec.Target = request.Target;
        spec.Tolerance = request.Tolerance;
        spec.ToleranceMode = request.ToleranceMode;
        spec.ExpectedResultText = request.ExpectedResultText;
        spec.ExpectedState = request.ExpectedState;
        spec.SampleQuantity = request.SampleQuantity;
        spec.SampleQuantityUnit = request.SampleQuantityUnit;
        if (request.AlertLimit != null)
            spec.AlertLimit = request.AlertLimit;
        if (request.ActionLimit != null)
            spec.ActionLimit = request.ActionLimit;
        if (request.SpecLimit != null)
            spec.SpecLimit = request.SpecLimit;
        spec.Unit = request.Unit ?? string.Empty;
        spec.DilutionFactor = request.DilutionFactor;
        spec.ResultBasis = request.ResultBasis;
        spec.LabelClaim = request.LabelClaim;
        spec.LabelClaimUnit = request.LabelClaimUnit;
        spec.DosageForm = request.DosageForm;
        spec.HplcMethodAnalyteId = request.HplcMethodAnalyteId;
        spec.RelatedSubstanceRow = request.RelatedSubstanceRow;
        spec.HplcMethodImpurityId = request.HplcMethodImpurityId;
        spec.IcpTestElementId = request.IcpTestElementId;
        spec.ProductionStageRole = request.ProductionStageRole;
        spec.Grade = request.Grade;

        // Replace-all on stages
        _db.SpecificationStages.RemoveRange(spec.Stages);
        spec.Stages = request.Stages?.Select(s => new SpecificationStage
        {
            SpecificationId = spec.Id,
            StageNumber = s.StageNumber,
            StageLabel = s.StageLabel,
            AcceptanceCriteriaText = s.AcceptanceCriteriaText
        }).ToList() ?? new List<SpecificationStage>();

        SpecificationService.ApplyCanonicalSpecLimit(spec);
        await _specificationService.ValidateAsync(spec, cancellationToken);

        if (_db.CurrentUserId is null && currentUserId > 0)
            _db.CurrentUserId = currentUserId;
        await _db.SaveChangesAsync(cancellationToken);
        return SpecificationResponse.From(spec);
    }

    // A specification referred to by any recorded result (ParameterResult.SpecificationId
    // or a projected ResultRecord for that item and test) is retired rather than
    // deleted so historical traceability survives intact; unused rows are deleted.
    public async Task<SpecificationDeleteOutcome> DeleteSpecificationAsync(
        int currentUserId,
        int id,
        string? changeReason = null,
        CancellationToken cancellationToken = default)
    {
        var spec = await _db.Specifications.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Specification {id} not found.");
        await SpecificationOwnership.EnsureCanEditAsync(_db, _scope, currentUserId, spec.TestCode);

        if (spec.IsRetired)
            throw new BusinessRuleException("This specification is already retired.");

        var hasResults = await _db.ParameterResults.AsNoTracking()
                .AnyAsync(pr => pr.SpecificationId == id, cancellationToken)
            || await _db.ResultRecords.AsNoTracking()
                .AnyAsync(r => r.TestCode == spec.TestCode && r.Sample != null && r.Sample.ItemId == spec.ItemId, cancellationToken);

        if (_db.CurrentUserId is null && currentUserId > 0)
            _db.CurrentUserId = currentUserId;

        if (hasResults)
        {
            await _reasonGuard.EnsureAsync(MasterRecordType.Specification, id, ChangeKind.Retire, changeReason, currentUserId, cancellationToken);
            spec.IsRetired = true;
            spec.RetiredAt = _time.GetUtcNow().UtcDateTime;
            spec.RetiredByUserId = currentUserId;
            await _db.SaveChangesAsync(cancellationToken);
            return new SpecificationDeleteOutcome("retired", id);
        }

        await _reasonGuard.EnsureAsync(MasterRecordType.Specification, id, ChangeKind.Delete, changeReason, currentUserId, cancellationToken);
        _db.Specifications.Remove(spec);
        await _db.SaveChangesAsync(cancellationToken);
        return new SpecificationDeleteOutcome("deleted", id);
    }

    public async Task<SpecificationResponse> ReinstateSpecificationAsync(
        int currentUserId,
        int id,
        string? changeReason = null,
        CancellationToken cancellationToken = default)
    {
        var spec = await _db.Specifications.Include(s => s.Stages).FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Specification {id} not found.");
        await SpecificationOwnership.EnsureCanEditAsync(_db, _scope, currentUserId, spec.TestCode);

        if (!spec.IsRetired)
            throw new BusinessRuleException("Specification is not retired.");

        await _reasonGuard.EnsureAsync(MasterRecordType.Specification, id, ChangeKind.Retire, changeReason, currentUserId, cancellationToken);

        spec.IsRetired = false;
        spec.RetiredAt = null;
        spec.RetiredByUserId = null;

        await _specificationService.ValidateAsync(spec, cancellationToken);

        if (_db.CurrentUserId is null && currentUserId > 0)
            _db.CurrentUserId = currentUserId;
        await _db.SaveChangesAsync(cancellationToken);
        return SpecificationResponse.From(spec);
    }

    public async Task<IReadOnlyList<FieldError>> ValidateSpecificationAsync(
        int currentUserId,
        ValidateSpecificationRequest request,
        int? id = null,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<FieldError>();
        var effectiveId = id ?? request.Id ?? 0;

        int itemId = request.ItemId ?? 0;
        if (effectiveId > 0)
        {
            var existing = await _db.Specifications.AsNoTracking()
                .Where(s => s.Id == effectiveId)
                .Select(s => new { s.ItemId, s.TestCode })
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is null)
            {
                errors.Add(new FieldError("id", $"Specification {effectiveId} not found."));
                return errors;
            }
            if (itemId <= 0)
                itemId = existing.ItemId;
        }
        else if (itemId <= 0 || !await _db.Items.AsNoTracking().AnyAsync(i => i.Id == itemId, cancellationToken))
        {
            errors.Add(new FieldError("itemId", $"Item {itemId} not found."));
        }

        if (string.IsNullOrWhiteSpace(request.TestCode))
        {
            errors.Add(new FieldError("testCode", "Test code is required."));
            return errors;
        }

        try
        {
            await SpecificationOwnership.EnsureCanEditAsync(_db, _scope, currentUserId, request.TestCode);
        }
        catch (Exception ex) when (ex is BusinessRuleException or UnauthorizedAccessException)
        {
            errors.Add(new FieldError("testCode", ex.Message));
            return errors;
        }

        var paramName = request.ParameterName;
        if (string.IsNullOrWhiteSpace(paramName) && request.RelatedSubstanceRow is RelatedSubstanceRow kind && Enum.IsDefined(kind))
        {
            var impurityName = request.HplcMethodImpurityId is int impurityId
                ? await _db.HplcMethodImpurities.AsNoTracking().Where(i => i.Id == impurityId).Select(i => i.Name).FirstOrDefaultAsync(cancellationToken)
                : null;
            paramName = SpecificationService.RelatedSubstanceParameterName(kind, impurityName);
        }
        if (string.IsNullOrWhiteSpace(paramName))
        {
            var testDef = await _db.TestDefinitions.AsNoTracking().FirstOrDefaultAsync(t => t.Code == request.TestCode, cancellationToken);
            paramName = !string.IsNullOrWhiteSpace(testDef?.DisplayName) ? testDef.DisplayName : request.TestCode;
        }

        var spec = new Specification
        {
            Id = effectiveId,
            ItemId = itemId,
            TestCode = request.TestCode,
            ParameterName = paramName,
            DisplayOrder = request.DisplayOrder ?? 0,
            LimitType = request.LimitType ?? LimitType.CountTiered,
            ReferenceStandard = request.ReferenceStandard,
            LowerLimit = request.LowerLimit,
            UpperLimit = request.UpperLimit,
            LowerInclusive = request.LowerInclusive ?? true,
            UpperInclusive = request.UpperInclusive ?? true,
            Target = request.Target,
            Tolerance = request.Tolerance,
            ToleranceMode = request.ToleranceMode,
            ExpectedResultText = request.ExpectedResultText,
            ExpectedState = request.ExpectedState,
            SampleQuantity = request.SampleQuantity,
            SampleQuantityUnit = request.SampleQuantityUnit,
            AlertLimit = request.AlertLimit ?? string.Empty,
            ActionLimit = request.ActionLimit ?? string.Empty,
            SpecLimit = request.SpecLimit ?? string.Empty,
            Unit = request.Unit ?? string.Empty,
            DilutionFactor = request.DilutionFactor,
            ResultBasis = request.ResultBasis,
            LabelClaim = request.LabelClaim,
            LabelClaimUnit = request.LabelClaimUnit,
            DosageForm = request.DosageForm,
            HplcMethodAnalyteId = request.HplcMethodAnalyteId,
            RelatedSubstanceRow = request.RelatedSubstanceRow,
            HplcMethodImpurityId = request.HplcMethodImpurityId,
            IcpTestElementId = request.IcpTestElementId,
            ProductionStageRole = request.ProductionStageRole,
            Grade = request.Grade,
            Stages = request.Stages?.Select(s => new SpecificationStage
            {
                StageNumber = s.StageNumber,
                StageLabel = s.StageLabel,
                AcceptanceCriteriaText = s.AcceptanceCriteriaText
            }).ToList() ?? new List<SpecificationStage>()
        };

        try
        {
            SpecificationService.ApplyCanonicalSpecLimit(spec);
            await _specificationService.ValidateAsync(spec, cancellationToken);
        }
        catch (FieldValidationException ex)
        {
            errors.AddRange(ex.FieldErrors);
        }
        catch (BusinessRuleException ex)
        {
            errors.Add(new FieldError(MapSpecValidationField(ex.Message), ex.Message));
        }

        return errors;
    }

    private static string MapSpecValidationField(string message)
    {
        if (message.Contains("Test", StringComparison.OrdinalIgnoreCase)) return "testCode";
        if (message.Contains("Parameter", StringComparison.OrdinalIgnoreCase)) return "parameterName";
        if (message.Contains("Grade", StringComparison.OrdinalIgnoreCase)) return "grade";
        if (message.Contains("stage", StringComparison.OrdinalIgnoreCase)) return "productionStageRole";
        if (message.Contains("analyte", StringComparison.OrdinalIgnoreCase)) return "hplcMethodAnalyteId";
        if (message.Contains("element", StringComparison.OrdinalIgnoreCase)) return "icpTestElementId";
        if (message.Contains("basis", StringComparison.OrdinalIgnoreCase)) return "resultBasis";
        if (message.Contains("label claim", StringComparison.OrdinalIgnoreCase)) return "labelClaim";
        if (message.Contains("dilution", StringComparison.OrdinalIgnoreCase)) return "dilutionFactor";
        if (message.Contains("Limit type", StringComparison.OrdinalIgnoreCase)) return "limitType";
        if (message.Contains("Lower", StringComparison.OrdinalIgnoreCase)) return "lowerLimit";
        if (message.Contains("Upper", StringComparison.OrdinalIgnoreCase)) return "upperLimit";
        if (message.Contains("Target", StringComparison.OrdinalIgnoreCase)) return "target";
        if (message.Contains("Tolerance", StringComparison.OrdinalIgnoreCase)) return "tolerance";
        if (message.Contains("Expected", StringComparison.OrdinalIgnoreCase)) return "expectedResultText";
        return "specLimit";
    }
}
