using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.MasterData;

// Master data: specification. Behind SpecificationMasterDataController
// (api/masterdata/...); the controller only maps HTTP to these calls.
public class SpecificationMasterDataService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly SpecificationService _specificationService;

    public SpecificationMasterDataService(IMicroLimsDbContext db, IUserSectionScopeService scope, SpecificationService? specificationService = null)
    {
        _db = db;
        _scope = scope;
        _specificationService = specificationService ?? new SpecificationService(db);
    }

    public async Task<object> GetSpecificationsAsync(int currentUserId, int itemId)
    {
        var specs = await _specificationService.GetForItemAsync(itemId);
        var scope = await _scope.GetAccessibleSectionIdsAsync(currentUserId);

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
                s.ExpectedState, s.SampleQuantity, s.SampleQuantityUnit, s.TestAnalyteId, s.ResultBasis, s.SampleMatrix,
                s.LabelClaim, s.LabelClaimUnit, s.ConversionFactor, s.DosageForm, s.Stages,
                canEdit, def?.Section?.Name ?? string.Empty);
        }).ToList();

        return rows;
    }

    public async Task<object> CreateSpecificationAsync(int currentUserId, CreateSpecificationRequest request)
    {
        await SpecificationOwnership.EnsureCanEditAsync(_db, _scope, currentUserId, request.TestCode);

        var limitType = request.LimitType ?? LimitType.CountTiered;
        var paramName = request.ParameterName;
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
            TestAnalyteId = request.TestAnalyteId,
            ResultBasis = request.ResultBasis,
            SampleMatrix = request.SampleMatrix,
            LabelClaim = request.LabelClaim,
            LabelClaimUnit = request.LabelClaimUnit,
            ConversionFactor = request.ConversionFactor ?? 1.0m,
            DosageForm = request.DosageForm,
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
        return spec;
    }

    public async Task<object> UpdateSpecificationAsync(int currentUserId, int id, UpdateSpecificationRequest request)
    {
        var spec = await _db.Specifications.Include(s => s.Stages).FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new NotFoundException($"Specification {id} not found.");

        // Check both the row's current lab and the lab it would move to -
        // a Section Head may not reassign a row into or out of their lab
        // by changing TestCode either.
        await SpecificationOwnership.EnsureCanEditAsync(_db, _scope, currentUserId, spec.TestCode);
        await SpecificationOwnership.EnsureCanEditAsync(_db, _scope, currentUserId, request.TestCode);

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
        spec.TestAnalyteId = request.TestAnalyteId;
        spec.ResultBasis = request.ResultBasis;
        spec.SampleMatrix = request.SampleMatrix;
        spec.LabelClaim = request.LabelClaim;
        spec.LabelClaimUnit = request.LabelClaimUnit;
        spec.DosageForm = request.DosageForm;
        if (request.ConversionFactor.HasValue)
            spec.ConversionFactor = request.ConversionFactor.Value;

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
        await _specificationService.ValidateAsync(spec);

        await _db.SaveChangesAsync();
        return spec;
    }

    // No downstream dependents to guard - Results/CountTestReadings copy
    // the Alert/Action/Spec values as plain strings at calculation time
    // rather than referencing the Specification row itself.
    public async Task<object> DeleteSpecificationAsync(int currentUserId, int id)
    {
        var spec = await _db.Specifications.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new NotFoundException($"Specification {id} not found.");
        await SpecificationOwnership.EnsureCanEditAsync(_db, _scope, currentUserId, spec.TestCode);
        _db.Specifications.Remove(spec);
        await _db.SaveChangesAsync();
        return new { };
    }
}
