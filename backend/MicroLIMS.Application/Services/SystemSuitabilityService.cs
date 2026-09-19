using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.Configurations;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;

namespace MicroLIMS.Application.Services;

public class SystemSuitabilityService : ISystemSuitabilityService
{
    private readonly MicroLimsDbContext _db;
    private readonly IElectronicSignatureService _signatureService;
    private readonly IUserSectionScopeService _scope;
    private readonly ILabClock _clock;

    public SystemSuitabilityService(
        MicroLimsDbContext db,
        IElectronicSignatureService signatureService,
        IUserSectionScopeService scope,
        ILabClock? clock = null)
    {
        _db = db;
        _signatureService = signatureService;
        _scope = scope;
        _clock = clock ?? LabClock.Default;
    }


    public static (bool Passed, string? FailureReasons) EvaluateAcceptanceCriteria(
        TestDefinition test,
        decimal? rsdPercent,
        decimal? resolution,
        decimal? tailingFactor,
        decimal? theoreticalPlates)
    {
        var failures = new List<string>();

        if (test.SstMaxRsdPercent.HasValue)
        {
            if (!rsdPercent.HasValue)
            {
                failures.Add($"RSD% is required (max {test.SstMaxRsdPercent.Value}%) but was not provided.");
            }
            else if (rsdPercent.Value > test.SstMaxRsdPercent.Value)
            {
                failures.Add($"RSD% ({rsdPercent.Value}%) exceeds maximum limit ({test.SstMaxRsdPercent.Value}%).");
            }
        }

        if (test.SstMinResolution.HasValue)
        {
            if (!resolution.HasValue)
            {
                failures.Add($"Resolution is required (min {test.SstMinResolution.Value}) but was not provided.");
            }
            else if (resolution.Value < test.SstMinResolution.Value)
            {
                failures.Add($"Resolution ({resolution.Value}) is below minimum limit ({test.SstMinResolution.Value}).");
            }
        }

        if (test.SstMaxTailingFactor.HasValue)
        {
            if (!tailingFactor.HasValue)
            {
                failures.Add($"Tailing factor is required (max {test.SstMaxTailingFactor.Value}) but was not provided.");
            }
            else if (tailingFactor.Value > test.SstMaxTailingFactor.Value)
            {
                failures.Add($"Tailing factor ({tailingFactor.Value}) exceeds maximum limit ({test.SstMaxTailingFactor.Value}).");
            }
        }

        if (test.SstMinTheoreticalPlates.HasValue)
        {
            if (!theoreticalPlates.HasValue)
            {
                failures.Add($"Theoretical plates is required (min {test.SstMinTheoreticalPlates.Value}) but was not provided.");
            }
            else if (theoreticalPlates.Value < test.SstMinTheoreticalPlates.Value)
            {
                failures.Add($"Theoretical plates ({theoreticalPlates.Value}) is below minimum limit ({test.SstMinTheoreticalPlates.Value}).");
            }
        }

        if (failures.Count == 0)
        {
            return (true, null);
        }

        return (false, string.Join("; ", failures));
    }

    public async Task<SystemSuitabilityRun> CreateAsync(
        CreateSystemSuitabilityRunRequest request,
        int userId,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var test = await _db.TestDefinitions.FirstOrDefaultAsync(t => t.Id == request.TestDefinitionId, ct)
            ?? throw new InvalidOperationException($"Test definition {request.TestDefinitionId} not found.");

        if (!test.RequiresSystemSuitability)
            throw new InvalidOperationException("Test definition does not require system suitability.");

        if (string.IsNullOrWhiteSpace(test.MethodAbbreviation))
            throw new InvalidOperationException("Test definition does not have a method abbreviation configured.");

        // Section scoping guard
        var scope = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
        if (scope != null && !scope.Contains(test.SectionId))
            throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");

        // Equipment validation: HPLC + same section
        var equip = await _db.Equipment.FirstOrDefaultAsync(e => e.Id == request.EquipmentId, ct)
            ?? throw new InvalidOperationException($"Equipment {request.EquipmentId} not found.");

        if (equip.Type != EquipmentType.Hplc)
            throw new InvalidOperationException("Selected equipment must be an HPLC instrument.");

        if (equip.SectionId != test.SectionId)
            throw new InvalidOperationException("Equipment belongs to a different laboratory section than the test definition.");

        // Column validation: active + same section
        var col = await _db.ChromatographyColumns.FirstOrDefaultAsync(c => c.Id == request.ChromatographyColumnId, ct)
            ?? throw new InvalidOperationException($"Chromatography column {request.ChromatographyColumnId} not found.");

        if (!col.IsActive)
            throw new InvalidOperationException("Chromatography column is not active.");

        if (col.SectionId != test.SectionId)
            throw new InvalidOperationException("Chromatography column belongs to a different laboratory section than the test definition.");

        // Reference standard material: ReferenceStandard + usable (in stock, not expired) + same section
        var material = await _db.Materials.FirstOrDefaultAsync(m => m.Id == request.ReferenceStandardMaterialId, ct)
            ?? throw new InvalidOperationException($"Reference standard material {request.ReferenceStandardMaterialId} not found.");

        if (material.MaterialType != MaterialType.ReferenceStandard)
            throw new InvalidOperationException("Material must be a reference standard.");

        if (material.SectionId != test.SectionId)
            throw new InvalidOperationException("Reference standard belongs to a different laboratory section than the test definition.");

        if (material.QuantityRemaining <= 0)
            throw new InvalidOperationException("Reference standard is depleted.");

        if (material.ExpiryDate.HasValue && material.ExpiryDate.Value.Date < DateTime.UtcNow.Date)
            throw new InvalidOperationException("Reference standard is expired.");

        if (!material.Purity.HasValue || material.Purity.Value <= 0 || material.Purity.Value > 100)
            throw new InvalidOperationException("Reference standard has invalid purity.");

        // Numeric checks
        if (request.StandardWeightMg <= 0)
            throw new InvalidOperationException("Standard weight must be greater than 0.");

        if (request.StandardDilution <= 0)
            throw new InvalidOperationException("Standard dilution must be greater than 0.");

        if (request.StandardMeanArea <= 0)
            throw new InvalidOperationException("Standard mean area must be greater than 0.");

        // Server-side Pass/Fail evaluation
        var (passed, failureReasons) = EvaluateAcceptanceCriteria(
            test,
            request.RsdPercent,
            request.Resolution,
            request.TailingFactor,
            request.TheoreticalPlates);

        // Signs first - a wrong password writes nothing below. Signed against
        // the method (TestDefinition) because the run has no Id until
        // SaveChanges and ElectronicSignatures is append-only (a later
        // EntityId update is rejected by the database). The run points at
        // its signature through SignatureId, so the link is still direct -
        // same ordering as SamplePreparationService.CommitPreparationAsync.
        _db.CurrentUserId = userId;
        var signature = await _signatureService.SignAsync(
            userId,
            request.Password,
            SignatureMeaning.SuitabilityRunPerformed,
            "TestDefinition",
            test.Id,
            request.Comment,
            ipAddress);

        var now = _clock.UtcNow.UtcDateTime;
        var methodAbbr = test.MethodAbbreviation.Trim().ToUpperInvariant();

        var run = new SystemSuitabilityRun
        {
            TestDefinitionId = test.Id,
            SectionId = test.SectionId,
            EquipmentId = equip.Id,
            ChromatographyColumnId = col.Id,
            ReferenceStandardMaterialId = material.Id,
            StandardPurityPercent = material.Purity.Value,
            StandardWeightMg = request.StandardWeightMg,
            StandardDilution = request.StandardDilution,
            StandardMeanArea = request.StandardMeanArea,
            RsdPercent = request.RsdPercent,
            Resolution = request.Resolution,
            TailingFactor = request.TailingFactor,
            TheoreticalPlates = request.TheoreticalPlates,
            Passed = passed,
            FailureReasons = failureReasons,
            PerformedByUserId = userId,
            PerformedAt = now,
            Signature = signature,
            Comment = request.Comment
        };

        run.Code = await SystemSuitabilityRunCode.NextAsync(
            _db.SystemSuitabilityRuns.Select(r => r.Code),
            methodAbbr,
            run.PerformedAt,
            _clock,
            "S.S",
            ct);

        _db.SystemSuitabilityRuns.Add(run);

        // Two runs under the same code at the same moment both pick the same next number.
        // Unique index retry pattern identical to PreparedLotNumber / MediaPreparationService.
        if (!await UniqueIndexSave.TrySaveChangesAsync(_db, SystemSuitabilityRunConfiguration.CodeIndexName))
        {
            run.Code = await SystemSuitabilityRunCode.NextAsync(
                _db.SystemSuitabilityRuns.Select(r => r.Code),
                methodAbbr,
                run.PerformedAt,
                _clock,
                "S.S",
                ct);


            if (!await UniqueIndexSave.TrySaveChangesAsync(_db, SystemSuitabilityRunConfiguration.CodeIndexName))
            {
                throw new InvalidOperationException(
                    $"Suitability run code {run.Code} was taken by another run at the same moment. Nothing was saved - submit the run again.");
            }
        }

        return run;
    }

    public async Task<List<SystemSuitabilityRun>> GetAllAsync(
        SystemSuitabilityRunFilter filter,
        int userId,
        CancellationToken ct = default)
    {
        var scope = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
        var query = _db.SystemSuitabilityRuns.AsNoTracking()
            .Include(r => r.TestDefinition)
            .Include(r => r.Section)
            .Include(r => r.Equipment)
            .Include(r => r.ChromatographyColumn)
            .Include(r => r.ReferenceStandardMaterial)
            .Include(r => r.PerformedByUser)
            .Include(r => r.Signature)
            .AsQueryable();

        if (scope != null)
        {
            query = query.Where(r => scope.Contains(r.SectionId));
        }

        if (filter.TestDefinitionId.HasValue)
            query = query.Where(r => r.TestDefinitionId == filter.TestDefinitionId.Value);

        if (!string.IsNullOrWhiteSpace(filter.TestCode))
            query = query.Where(r => r.TestDefinition != null && r.TestDefinition.Code == filter.TestCode);

        if (filter.Passed.HasValue)
            query = query.Where(r => r.Passed == filter.Passed.Value);

        if (filter.Date.HasValue)
            query = query.Where(r => r.PerformedAt.Date == filter.Date.Value.Date);

        if (filter.FromDate.HasValue)
            query = query.Where(r => r.PerformedAt.Date >= filter.FromDate.Value.Date);

        if (filter.ToDate.HasValue)
            query = query.Where(r => r.PerformedAt.Date <= filter.ToDate.Value.Date);

        return await query.OrderByDescending(r => r.PerformedAt).ThenByDescending(r => r.Id).ToListAsync(ct);
    }

    public async Task<SystemSuitabilityRun?> GetByIdAsync(int id, int userId, CancellationToken ct = default)
    {
        await _scope.EnsureSuitabilityRunAccessAsync(userId, id, ct);

        return await _db.SystemSuitabilityRuns.AsNoTracking()
            .Include(r => r.TestDefinition)
            .Include(r => r.Section)
            .Include(r => r.Equipment)
            .Include(r => r.ChromatographyColumn)
            .Include(r => r.ReferenceStandardMaterial)
            .Include(r => r.PerformedByUser)
            .Include(r => r.Signature)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<SuitabilityRunReportDetailsDto> GetReportDetailsAsync(int runId, int userId, CancellationToken ct = default)
    {
        await _scope.EnsureSuitabilityRunAccessAsync(userId, runId, ct);

        var run = await _db.SystemSuitabilityRuns.AsNoTracking()
            .Where(r => r.Id == runId)
            .Select(r => new
            {
                r.TestDefinition!.SstMaxRsdPercent,
                r.TestDefinition.SstMinResolution,
                r.TestDefinition.SstMaxTailingFactor,
                r.TestDefinition.SstMinTheoreticalPlates,
                r.Equipment!.Vendor,
                r.Equipment.CdsSoftware,
                ColumnSerial = r.ChromatographyColumn!.SerialNumber,
                r.SignatureId
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"System suitability run {runId} not found.");

        var signature = await _db.ElectronicSignatures.AsNoTracking()
            .Where(s => s.Id == run.SignatureId)
            .Select(s => new SignatureDto(s.UserFullNameSnapshot, s.UsernameSnapshot, s.RoleSnapshot, s.MeaningOfSignature.ToString(), s.SignedAt, s.Comment))
            .FirstOrDefaultAsync(ct);

        var linked = await _db.TestOrders.AsNoTracking()
            .Where(o => o.SystemSuitabilityRunId == runId)
            .OrderBy(o => o.Sample!.ReferenceNumber)
            .Select(o => new
            {
                o.Id,
                o.SampleId,
                o.Sample!.ReferenceNumber,
                ItemName = o.Sample.Item != null ? o.Sample.Item.Name : null,
                o.Sample.BatchNumber,
                o.TestCode,
                Result = _db.HplcAssayResults
                    .Where(h => h.TestOrderId == o.Id && h.IsActive)
                    .Select(h => new { h.ReportedResult, h.ComparisonStatus, h.EnteredAt })
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        return new SuitabilityRunReportDetailsDto(
            run.SstMaxRsdPercent, run.SstMinResolution, run.SstMaxTailingFactor, run.SstMinTheoreticalPlates,
            run.Vendor, run.CdsSoftware?.ToString(), run.ColumnSerial, signature,
            linked.Select(l => new SuitabilityRunLinkedTestDto(
                l.Id, l.SampleId, l.ReferenceNumber, l.ItemName, l.BatchNumber, l.TestCode,
                l.Result?.ReportedResult, l.Result?.ComparisonStatus, l.Result?.EnteredAt)).ToList());
    }

    public async Task<List<SystemSuitabilityRun>> GetSelectableRunsForTestOrderAsync(
        int testOrderId,
        int userId,
        CancellationToken ct = default)
    {
        await _scope.EnsureTestOrderAccessAsync(userId, testOrderId, ct);

        var order = await _db.TestOrders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == testOrderId, ct)
            ?? throw new InvalidOperationException($"Test order {testOrderId} not found.");

        var testDef = await _db.TestDefinitions.AsNoTracking().FirstOrDefaultAsync(t => t.Code == order.TestCode, ct)
            ?? throw new InvalidOperationException($"Test definition for code \"{order.TestCode}\" not found.");

        return await _db.SystemSuitabilityRuns.AsNoTracking()
            .Include(r => r.TestDefinition)
            .Include(r => r.Equipment)
            .Include(r => r.ChromatographyColumn)
            .Include(r => r.ReferenceStandardMaterial)
            .Where(r => r.Passed && r.TestDefinitionId == testDef.Id && r.SectionId == order.SectionId)
            .OrderByDescending(r => r.PerformedAt)
            .ThenByDescending(r => r.Id)
            .ToListAsync(ct);
    }

    // The run a test order is currently linked to (null when not linked yet).
    public async Task<SystemSuitabilityRun?> GetLinkedRunForTestOrderAsync(int testOrderId, int userId, CancellationToken ct = default)
    {
        await _scope.EnsureTestOrderAccessAsync(userId, testOrderId, ct);

        var runId = await _db.TestOrders.AsNoTracking()
            .Where(o => o.Id == testOrderId)
            .Select(o => o.SystemSuitabilityRunId)
            .FirstOrDefaultAsync(ct);
        if (runId is null) return null;

        return await _db.SystemSuitabilityRuns.AsNoTracking()
            .Include(r => r.TestDefinition)
            .Include(r => r.Section)
            .Include(r => r.Equipment)
            .Include(r => r.ChromatographyColumn)
            .Include(r => r.ReferenceStandardMaterial)
            .Include(r => r.Signature)
            .FirstOrDefaultAsync(r => r.Id == runId.Value, ct);
    }

    public async Task LinkTestOrdersAsync(
        int runId,
        IEnumerable<int> testOrderIds,
        int userId,
        CancellationToken ct = default)
    {
        await _scope.EnsureSuitabilityRunAccessAsync(userId, runId, ct);

        var run = await _db.SystemSuitabilityRuns
            .Include(r => r.TestDefinition)
            .FirstOrDefaultAsync(r => r.Id == runId, ct)
            ?? throw new InvalidOperationException($"System suitability run {runId} not found.");

        if (!run.Passed)
            throw new InvalidOperationException("Cannot link test order to a failed system suitability run.");

        var idList = testOrderIds.Distinct().ToList();
        if (idList.Count == 0)
            throw new InvalidOperationException("At least one test order ID is required.");

        await _scope.EnsureTestOrdersAccessAsync(userId, idList, ct);

        var orders = await _db.TestOrders
            .Where(o => idList.Contains(o.Id))
            .ToListAsync(ct);

        if (orders.Count != idList.Count)
            throw new InvalidOperationException("One or more test orders were not found.");

        foreach (var order in orders)
        {
            if (order.IsSuperseded ||
                order.Status == ApprovalStatus.Approved ||
                order.Status == ApprovalStatus.Rejected ||
                order.Status == ApprovalStatus.Voided)
            {
                throw new InvalidOperationException($"Test order {order.Id} is closed or superseded and cannot be linked.");
            }

            if (order.SectionId != run.SectionId)
            {
                throw new InvalidOperationException($"Test order {order.Id} belongs to a different laboratory section than the system suitability run.");
            }

            if (order.TestCode != run.TestDefinition!.Code)
            {
                throw new InvalidOperationException($"Test order {order.Id} has test code \"{order.TestCode}\", which does not match run method \"{run.TestDefinition.Code}\".");
            }

            // Block relink once an active HPLC assay result exists for this test order (REQ-FP-003)
            var hasActiveResult = await _db.HplcAssayResults
                .AnyAsync(r => r.TestOrderId == order.Id && r.IsActive, ct);
            if (hasActiveResult)
            {
                throw new InvalidOperationException($"Cannot link test order {order.Id} because an active HPLC assay result already exists for it.");
            }

            order.SystemSuitabilityRunId = run.Id;
        }

        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync(ct);
    }
}
