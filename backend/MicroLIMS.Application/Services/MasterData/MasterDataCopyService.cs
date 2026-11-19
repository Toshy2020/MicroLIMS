using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using System.Globalization;

namespace MicroLIMS.Application.Services.MasterData;

public sealed record CopySpecificationsRequest(
    int SourceItemId,
    int TargetItemId,
    IReadOnlyList<int> SpecificationIds);

public sealed record CopySpecificationsPreviewRow(
    int SourceSpecificationId,
    string TestCode,
    string ParameterName,
    string Status,
    string? Message);

/// <summary>
/// Server-side copy of active specifications between items and item cloning
/// (item + specifications + auto-assigned tests in one transaction).
/// </summary>
public class MasterDataCopyService
{
    private const int SourceContextMaxLength = 100;

    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly SpecificationService _specificationService;
    private readonly SpecificationMasterDataService _specificationMasterDataService;
    private readonly ItemService _itemService;

    public MasterDataCopyService(
        IMicroLimsDbContext db,
        IUserSectionScopeService scope,
        SpecificationService specificationService,
        SpecificationMasterDataService specificationMasterDataService,
        ItemService itemService)
    {
        _db = db;
        _scope = scope;
        _specificationService = specificationService;
        _specificationMasterDataService = specificationMasterDataService;
        _itemService = itemService;
    }

    public async Task<IReadOnlyList<CopySpecificationsPreviewRow>> PreviewAsync(
        CopySpecificationsRequest r,
        int userId,
        CancellationToken ct = default)
    {
        var sourceExists = await _db.Items.AsNoTracking().AnyAsync(i => i.Id == r.SourceItemId, ct);
        if (!sourceExists)
            throw new NotFoundException(
                MessageCodes.MasterDataSourceItemNotFound,
                $"Source item {r.SourceItemId} not found.",
                new Dictionary<string, string> { ["sourceItemId"] = r.SourceItemId.ToString(CultureInfo.InvariantCulture) });

        var targetExists = await _db.Items.AsNoTracking().AnyAsync(i => i.Id == r.TargetItemId, ct);
        if (!targetExists)
            throw new NotFoundException(
                MessageCodes.MasterDataTargetItemNotFound,
                $"Target item {r.TargetItemId} not found.",
                new Dictionary<string, string> { ["targetItemId"] = r.TargetItemId.ToString(CultureInfo.InvariantCulture) });

        var requestedIds = r.SpecificationIds?.Distinct().ToList() ?? [];

        // Retired source rows are never offered or copied, even when explicitly named.
        var query = _db.Specifications
            .AsNoTracking()
            .Include(s => s.Stages)
            .Where(s => s.ItemId == r.SourceItemId && !s.IsRetired);

        if (requestedIds.Count > 0)
            query = query.Where(s => requestedIds.Contains(s.Id));

        var loaded = await query.ToListAsync(ct);
        var sourceSpecs = OrderSourceSpecifications(loaded, requestedIds);

        var targetSpecs = await _db.Specifications
            .AsNoTracking()
            .Where(s => s.ItemId == r.TargetItemId && !s.IsRetired)
            .ToListAsync(ct);

        var preview = new List<CopySpecificationsPreviewRow>(sourceSpecs.Count);
        foreach (var src in sourceSpecs)
        {
            preview.Add(await ClassifyForPreviewAsync(src, r.TargetItemId, targetSpecs, userId, ct));
        }

        return preview;
    }

    public async Task<int> CopyAsync(
        CopySpecificationsRequest r,
        int userId,
        CancellationToken ct = default)
    {
        var requestedIds = r.SpecificationIds?.Distinct().ToList() ?? [];
        if (requestedIds.Count == 0)
            throw new BusinessRuleException(
                MessageCodes.MasterDataSelectSpecificationToCopy,
                "Select at least one specification to copy.");

        var sourceItem = await _db.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == r.SourceItemId, ct);
        if (sourceItem is null)
            throw new NotFoundException(
                MessageCodes.MasterDataSourceItemNotFound,
                $"Source item {r.SourceItemId} not found.",
                new Dictionary<string, string> { ["sourceItemId"] = r.SourceItemId.ToString(CultureInfo.InvariantCulture) });

        var targetExists = await _db.Items.AsNoTracking().AnyAsync(i => i.Id == r.TargetItemId, ct);
        if (!targetExists)
            throw new NotFoundException(
                MessageCodes.MasterDataTargetItemNotFound,
                $"Target item {r.TargetItemId} not found.",
                new Dictionary<string, string> { ["targetItemId"] = r.TargetItemId.ToString(CultureInfo.InvariantCulture) });

        var loaded = await _db.Specifications
            .AsNoTracking()
            .Include(s => s.Stages)
            .Where(s => s.ItemId == r.SourceItemId && requestedIds.Contains(s.Id))
            .ToListAsync(ct);

        var byId = loaded.ToDictionary(s => s.Id);
        var orderedSpecs = new List<Specification>(requestedIds.Count);
        foreach (var id in requestedIds)
        {
            if (!byId.TryGetValue(id, out var spec))
                throw new NotFoundException(
                    MessageCodes.MasterDataSpecificationNotOnSource,
                    $"Specification {id} was not found on source item {r.SourceItemId}.",
                    new Dictionary<string, string> { ["id"] = id.ToString(CultureInfo.InvariantCulture), ["sourceItemId"] = r.SourceItemId.ToString(CultureInfo.InvariantCulture) });
            if (spec.IsRetired)
                throw new BusinessRuleException(
                    MessageCodes.MasterDataRetiredSpecificationCannotCopy,
                    $"Retired specification {id} cannot be copied.",
                    new Dictionary<string, string> { ["id"] = id.ToString(CultureInfo.InvariantCulture) });
            orderedSpecs.Add(spec);
        }

        // Pre-check ownership across all requested rows before skipping AlreadyPresent,
        // so copying a row in an unconfigured section is always refused with 403.
        foreach (var spec in orderedSpecs)
        {
            await SpecificationOwnership.EnsureCanEditAsync(_db, _scope, userId, spec.TestCode);
        }

        var targetSpecs = await _db.Specifications
            .AsNoTracking()
            .Where(s => s.ItemId == r.TargetItemId && !s.IsRetired)
            .ToListAsync(ct);

        var toCopy = orderedSpecs
            .Where(s => !IsAlreadyPresent(s, targetSpecs))
            .ToList();

        if (toCopy.Count == 0)
            return 0;

        return await UnitOfWork.RunAsync(_db, async () =>
        {
            return await CreateCopiedSpecificationsInTransactionAsync(
                sourceItem.Code,
                r.TargetItemId,
                toCopy,
                userId);
        });
    }

    public async Task<ItemResponse> CreateItemAsync(
        ItemSaveRequest request,
        int userId,
        CancellationToken ct = default)
    {
        if (request.CopyFrom is null)
            return await _itemService.CreateAsync(request, userId, ct);

        var copyFrom = request.CopyFrom;

        return await UnitOfWork.RunAsync(_db, async () =>
        {
            if (_db.CurrentUserId is null && userId > 0)
                _db.CurrentUserId = userId;

            var sourceItem = await _db.Items
                .AsNoTracking()
                .Include(i => i.AssignedTests)
                .FirstOrDefaultAsync(i => i.Id == copyFrom.SourceItemId, ct);
            if (sourceItem is null)
                throw new NotFoundException(
                    MessageCodes.MasterDataSourceItemNotFound,
                    $"Source item {copyFrom.SourceItemId} not found.",
                    new Dictionary<string, string> { ["sourceItemId"] = copyFrom.SourceItemId.ToString(CultureInfo.InvariantCulture) });

            // Code, Name, SopNumber and documents are never copied from sourceItem:
            // Name/Code/SopNumber always come from request, and documents are not touched.
            var mergedAssignedTests = copyFrom.AutoAssignedTests
                ? MergeAssignedTests(request.AssignedTests, sourceItem.AssignedTests)
                : request.AssignedTests;

            var createItemRequest = request with
            {
                AssignedTests = mergedAssignedTests,
                CopyFrom = null
            };

            ItemResponse createdItem;
            var previousContext = _db.CurrentSourceContext;
            _db.CurrentSourceContext = TruncateSourceContext($"Copied from item {sourceItem.Code}");
            try
            {
                createdItem = await _itemService.CreateAsync(createItemRequest, userId, ct);
            }
            finally
            {
                _db.CurrentSourceContext = previousContext;
            }

            if (copyFrom.Specifications)
            {
                var sourceSpecs = await _db.Specifications
                    .AsNoTracking()
                    .Include(s => s.Stages)
                    .Where(s => s.ItemId == sourceItem.Id && !s.IsRetired)
                    .OrderBy(s => s.DisplayOrder)
                    .ThenBy(s => s.Id)
                    .ToListAsync(ct);

                foreach (var spec in sourceSpecs)
                {
                    await SpecificationOwnership.EnsureCanEditAsync(_db, _scope, userId, spec.TestCode);
                }

                if (sourceSpecs.Count > 0)
                {
                    await CreateCopiedSpecificationsInTransactionAsync(
                        sourceItem.Code,
                        createdItem.Id,
                        sourceSpecs,
                        userId);
                }
            }

            var reloaded = await _db.Items
                .AsNoTracking()
                .Include(i => i.AssignedTests)
                .Include(i => i.Specifications.Where(s => !s.IsRetired))
                    .ThenInclude(s => s.Stages)
                .FirstAsync(i => i.Id == createdItem.Id, ct);

            return ItemResponse.From(reloaded);
        });
    }

    private async Task<CopySpecificationsPreviewRow> ClassifyForPreviewAsync(
        Specification src,
        int targetItemId,
        IReadOnlyList<Specification> targetSpecs,
        int userId,
        CancellationToken ct)
    {
        try
        {
            await SpecificationOwnership.EnsureCanEditAsync(_db, _scope, userId, src.TestCode);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or BusinessRuleException)
        {
            return new CopySpecificationsPreviewRow(src.Id, src.TestCode, src.ParameterName, "Invalid", ex.Message);
        }

        if (IsAlreadyPresent(src, targetSpecs))
        {
            return new CopySpecificationsPreviewRow(
                src.Id,
                src.TestCode,
                src.ParameterName,
                "AlreadyPresent",
                "A matching specification already exists on the target item.");
        }

        var candidate = BuildCandidateEntity(targetItemId, src);
        SpecificationService.ApplyCanonicalSpecLimit(candidate);

        try
        {
            await _specificationService.ValidateAsync(candidate, ct);
            return new CopySpecificationsPreviewRow(src.Id, src.TestCode, src.ParameterName, "Ok", null);
        }
        catch (BusinessRuleException ex)
        {
            return new CopySpecificationsPreviewRow(src.Id, src.TestCode, src.ParameterName, "Invalid", ex.Message);
        }
    }

    private async Task<int> CreateCopiedSpecificationsInTransactionAsync(
        string sourceItemCode,
        int targetItemId,
        IReadOnlyList<Specification> specs,
        int userId)
    {
        if (_db.CurrentUserId is null && userId > 0)
            _db.CurrentUserId = userId;

        var previousContext = _db.CurrentSourceContext;
        try
        {
            foreach (var src in specs)
            {
                _db.CurrentSourceContext = TruncateSourceContext(
                    $"Copied from specification {src.Id} of item {sourceItemCode}");
                await _specificationMasterDataService.CreateSpecificationAsync(
                    userId,
                    ToCreateRequest(targetItemId, src));
            }
        }
        finally
        {
            _db.CurrentSourceContext = previousContext;
        }

        return specs.Count;
    }

    private static List<Specification> OrderSourceSpecifications(
        List<Specification> loaded,
        List<int> requestedIds)
    {
        if (requestedIds.Count == 0)
        {
            return loaded
                .OrderBy(s => s.DisplayOrder)
                .ThenBy(s => s.Id)
                .ToList();
        }

        var orderById = requestedIds
            .Select((id, index) => (id, index))
            .ToDictionary(x => x.id, x => x.index);

        return loaded
            .OrderBy(s => orderById.GetValueOrDefault(s.Id, int.MaxValue))
            .ToList();
    }

    // Mirrors the active uniqueness & duplicate keys in SpecificationService.ValidateAsync (~L282-328).
    private static bool IsAlreadyPresent(Specification src, IReadOnlyList<Specification> targetSpecs)
    {
        var srcGrade = SpecificationService.NormalizeGrade(src.Grade)?.ToLowerInvariant();

        foreach (var t in targetSpecs)
        {
            if (t.IsRetired) continue;
            if (t.TestCode != src.TestCode) continue;
            if (t.ProductionStageRole != src.ProductionStageRole) continue;

            var targetGrade = SpecificationService.NormalizeGrade(t.Grade)?.ToLowerInvariant();
            if (!string.Equals(targetGrade, srcGrade, StringComparison.Ordinal)) continue;

            if (src.RelatedSubstanceRow is RelatedSubstanceRow kind)
            {
                if (t.RelatedSubstanceRow == kind && t.HplcMethodImpurityId == src.HplcMethodImpurityId)
                    return true;
                continue;
            }

            if (t.RelatedSubstanceRow is null && t.ParameterName == src.ParameterName)
                return true;

            if (src.HplcMethodAnalyteId is int analyteId
                && t.RelatedSubstanceRow is null
                && t.HplcMethodAnalyteId == analyteId
                && t.ResultBasis == src.ResultBasis)
                return true;

            if (src.IcpTestElementId is int elementId
                && t.IcpTestElementId == elementId
                && t.ResultBasis == src.ResultBasis)
                return true;
        }

        return false;
    }

    private static List<ItemTestRequest> MergeAssignedTests(
        List<ItemTestRequest>? explicitTests,
        ICollection<SampleTest> sourceTests)
    {
        var merged = new List<ItemTestRequest>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (explicitTests is not null)
        {
            foreach (var test in explicitTests)
            {
                if (seen.Add(test.TestCode))
                    merged.Add(test);
            }
        }

        foreach (var srcTest in sourceTests.OrderBy(t => t.Id))
        {
            if (seen.Add(srcTest.TestCode))
                merged.Add(new ItemTestRequest(srcTest.TestCode, srcTest.DisplayName));
        }

        return merged;
    }

    // Strip trailing fractional zeroes added when PostgreSQL numeric(18,6) columns
    // round-trip through EF Core, so ApplyCanonicalSpecLimit formats SpecLimit cleanly.
    private static decimal? TrimTrailingZeroes(decimal? value) =>
        value is decimal d ? d / 1.0000000000000000000000000000m : null;

    private static CreateSpecificationRequest ToCreateRequest(int targetItemId, Specification src) => new(
        ItemId: targetItemId,
        TestCode: src.TestCode,
        AlertLimit: src.AlertLimit,
        ActionLimit: src.ActionLimit,
        SpecLimit: src.SpecLimit,
        Unit: src.Unit,
        DilutionFactor: TrimTrailingZeroes(src.DilutionFactor),
        ParameterName: src.ParameterName,
        DisplayOrder: src.DisplayOrder,
        LimitType: src.LimitType,
        ReferenceStandard: src.ReferenceStandard,
        LowerLimit: TrimTrailingZeroes(src.LowerLimit),
        UpperLimit: TrimTrailingZeroes(src.UpperLimit),
        LowerInclusive: src.LowerInclusive,
        UpperInclusive: src.UpperInclusive,
        Target: TrimTrailingZeroes(src.Target),
        Tolerance: TrimTrailingZeroes(src.Tolerance),
        ToleranceMode: src.ToleranceMode,
        ExpectedResultText: src.ExpectedResultText,
        ExpectedState: src.ExpectedState,
        SampleQuantity: TrimTrailingZeroes(src.SampleQuantity),
        SampleQuantityUnit: src.SampleQuantityUnit,
        Stages: src.Stages
            .OrderBy(s => s.StageNumber)
            .Select(s => new SpecificationStageDto(null, s.StageNumber, s.StageLabel, s.AcceptanceCriteriaText))
            .ToList(),
        ResultBasis: src.ResultBasis,
        LabelClaim: TrimTrailingZeroes(src.LabelClaim),
        LabelClaimUnit: src.LabelClaimUnit,
        DosageForm: src.DosageForm,
        HplcMethodAnalyteId: src.HplcMethodAnalyteId,
        ProductionStageRole: src.ProductionStageRole,
        IcpTestElementId: src.IcpTestElementId,
        Grade: src.Grade,
        RelatedSubstanceRow: src.RelatedSubstanceRow,
        HplcMethodImpurityId: src.HplcMethodImpurityId);

    private static Specification BuildCandidateEntity(int targetItemId, Specification src) => new()
    {
        ItemId = targetItemId,
        TestCode = src.TestCode,
        ParameterName = src.ParameterName,
        DisplayOrder = src.DisplayOrder,
        LimitType = src.LimitType,
        ReferenceStandard = src.ReferenceStandard,
        LowerLimit = TrimTrailingZeroes(src.LowerLimit),
        UpperLimit = TrimTrailingZeroes(src.UpperLimit),
        LowerInclusive = src.LowerInclusive,
        UpperInclusive = src.UpperInclusive,
        Target = TrimTrailingZeroes(src.Target),
        Tolerance = TrimTrailingZeroes(src.Tolerance),
        ToleranceMode = src.ToleranceMode,
        ExpectedResultText = src.ExpectedResultText,
        ExpectedState = src.ExpectedState,
        SampleQuantity = TrimTrailingZeroes(src.SampleQuantity),
        SampleQuantityUnit = src.SampleQuantityUnit,
        AlertLimit = src.AlertLimit,
        ActionLimit = src.ActionLimit,
        SpecLimit = src.SpecLimit,
        Unit = src.Unit,
        DilutionFactor = TrimTrailingZeroes(src.DilutionFactor),
        ResultBasis = src.ResultBasis,
        LabelClaim = TrimTrailingZeroes(src.LabelClaim),
        LabelClaimUnit = src.LabelClaimUnit,
        DosageForm = src.DosageForm,
        HplcMethodAnalyteId = src.HplcMethodAnalyteId,
        RelatedSubstanceRow = src.RelatedSubstanceRow,
        HplcMethodImpurityId = src.HplcMethodImpurityId,
        IcpTestElementId = src.IcpTestElementId,
        ProductionStageRole = src.ProductionStageRole,
        Grade = src.Grade,
        Stages = src.Stages
            .OrderBy(s => s.StageNumber)
            .Select(s => new SpecificationStage
            {
                StageNumber = s.StageNumber,
                StageLabel = s.StageLabel,
                AcceptanceCriteriaText = s.AcceptanceCriteriaText
            })
            .ToList()
    };

    private static string TruncateSourceContext(string value) =>
        value.Length <= SourceContextMaxLength ? value : value[..SourceContextMaxLength];
}
