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
        return EvaluateAcceptanceCriteria(
            test.SstMaxRsdPercent,
            test.SstMinResolution,
            test.SstMaxTailingFactor,
            test.SstMinTheoreticalPlates,
            rsdPercent,
            resolution,
            tailingFactor,
            theoreticalPlates);
    }

    public static (bool Passed, string? FailureReasons) EvaluateAcceptanceCriteria(
        decimal? maxRsd,
        decimal? minRes,
        decimal? maxTailing,
        decimal? minPlates,
        decimal? rsdPercent,
        decimal? resolution,
        decimal? tailingFactor,
        decimal? theoreticalPlates)
    {
        var failures = new List<string>();

        if (maxRsd.HasValue)
        {
            if (!rsdPercent.HasValue)
            {
                failures.Add($"RSD% is required (max {maxRsd.Value}%) but was not provided.");
            }
            else if (rsdPercent.Value > maxRsd.Value)
            {
                failures.Add($"RSD% ({rsdPercent.Value}%) exceeds maximum limit ({maxRsd.Value}%).");
            }
        }

        if (minRes.HasValue)
        {
            if (!resolution.HasValue)
            {
                failures.Add($"Resolution is required (min {minRes.Value}) but was not provided.");
            }
            else if (resolution.Value < minRes.Value)
            {
                failures.Add($"Resolution ({resolution.Value}) is below minimum limit ({minRes.Value}).");
            }
        }

        if (maxTailing.HasValue)
        {
            if (!tailingFactor.HasValue)
            {
                failures.Add($"Tailing factor is required (max {maxTailing.Value}) but was not provided.");
            }
            else if (tailingFactor.Value > maxTailing.Value)
            {
                failures.Add($"Tailing factor ({tailingFactor.Value}) exceeds maximum limit ({maxTailing.Value}).");
            }
        }

        if (minPlates.HasValue)
        {
            if (!theoreticalPlates.HasValue)
            {
                failures.Add($"Theoretical plates is required (min {minPlates.Value}) but was not provided.");
            }
            else if (theoreticalPlates.Value < minPlates.Value)
            {
                failures.Add($"Theoretical plates ({theoreticalPlates.Value}) is below minimum limit ({minPlates.Value}).");
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

        bool isMultiAnalyte = test.WorkflowType == WorkflowType.HplcMultiAnalyte || test.EquationType == EquationType.HplcMultiAnalyte;

        int runRefMatId;
        decimal runPurity;
        decimal runWeight;
        decimal runDilution;
        decimal runMeanArea;
        decimal? runRsd;
        decimal? runResolution;
        decimal? runTailing;
        decimal? runPlates;
        bool passed;
        string? failureReasons;
        List<SystemSuitabilityRunAnalyte> runAnalytes = new();

        if (isMultiAnalyte)
        {
            if (request.Analytes == null || request.Analytes.Count == 0)
                throw new InvalidOperationException("Analyte rows are required for HPLC multi-analyte suitability runs.");

            var activeAnalytes = await _db.TestAnalytes
                .Where(a => a.TestDefinitionId == test.Id && a.IsActive)
                .OrderBy(a => a.DisplayOrder)
                .ThenBy(a => a.Id)
                .ToListAsync(ct);

            if (activeAnalytes.Count == 0)
                throw new InvalidOperationException($"Test definition {test.Code} has no active analytes configured.");

            var duplicateIds = request.Analytes
                .GroupBy(a => a.TestAnalyteId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicateIds.Count > 0)
                throw new InvalidOperationException($"Duplicate analyte entry for TestAnalyteId {duplicateIds[0]}.");

            var allTestAnalytes = await _db.TestAnalytes
                .Where(a => a.TestDefinitionId == test.Id)
                .ToDictionaryAsync(a => a.Id, ct);

            foreach (var reqRow in request.Analytes)
            {
                if (!allTestAnalytes.TryGetValue(reqRow.TestAnalyteId, out var analyteEntity))
                    throw new InvalidOperationException($"Analyte {reqRow.TestAnalyteId} is not configured for test {test.Code}.");

                if (!analyteEntity.IsActive)
                    throw new InvalidOperationException($"Analyte {analyteEntity.Element} is inactive.");
            }

            var reqAnalyteIds = request.Analytes.Select(a => a.TestAnalyteId).ToHashSet();
            var missingAnalytes = activeAnalytes.Where(a => !reqAnalyteIds.Contains(a.Id)).ToList();
            if (missingAnalytes.Count > 0)
                throw new InvalidOperationException($"Missing analyte row for: {string.Join(", ", missingAnalytes.Select(a => a.Element))}.");

            var allFailures = new List<string>();

            foreach (var reqRow in request.Analytes)
            {
                var analyteEntity = allTestAnalytes[reqRow.TestAnalyteId];

                var mat = await _db.Materials.FirstOrDefaultAsync(m => m.Id == reqRow.ReferenceStandardMaterialId, ct)
                    ?? throw new InvalidOperationException($"Reference standard material {reqRow.ReferenceStandardMaterialId} not found.");

                if (mat.MaterialType != MaterialType.ReferenceStandard)
                    throw new InvalidOperationException($"Material for analyte {analyteEntity.Element} must be a reference standard.");

                if (mat.SectionId != test.SectionId)
                    throw new InvalidOperationException($"Reference standard for analyte {analyteEntity.Element} belongs to a different laboratory section than the test definition.");

                if (mat.QuantityRemaining <= 0)
                    throw new InvalidOperationException($"Reference standard for analyte {analyteEntity.Element} is depleted.");

                if (mat.ExpiryDate.HasValue && mat.ExpiryDate.Value.Date < DateTime.UtcNow.Date)
                    throw new InvalidOperationException($"Reference standard for analyte {analyteEntity.Element} is expired.");

                if (!mat.Purity.HasValue || mat.Purity.Value <= 0 || mat.Purity.Value > 100)
                    throw new InvalidOperationException($"Reference standard for analyte {analyteEntity.Element} has invalid purity.");

                if (reqRow.StandardWeightMg <= 0)
                    throw new InvalidOperationException($"Standard weight for analyte {analyteEntity.Element} must be greater than 0.");

                if (reqRow.StandardDilution <= 0)
                    throw new InvalidOperationException($"Standard dilution for analyte {analyteEntity.Element} must be greater than 0.");

                if (reqRow.StandardMeanArea <= 0)
                    throw new InvalidOperationException($"Standard mean area for analyte {analyteEntity.Element} must be greater than 0.");

                var (rowPassed, rowFailureReasons) = EvaluateAcceptanceCriteria(
                    analyteEntity.SstMaxRsdPercent,
                    analyteEntity.SstMinResolution,
                    analyteEntity.SstMaxTailingFactor,
                    analyteEntity.SstMinTheoreticalPlates,
                    reqRow.RsdPercent,
                    reqRow.Resolution,
                    reqRow.TailingFactor,
                    reqRow.TheoreticalPlates);

                if (!rowPassed && !string.IsNullOrEmpty(rowFailureReasons))
                {
                    allFailures.Add($"{analyteEntity.Element}: {rowFailureReasons}");
                }

                var runAnalyte = new SystemSuitabilityRunAnalyte
                {
                    TestAnalyteId = analyteEntity.Id,
                    AnalyteName = analyteEntity.Element,
                    WavelengthNm = analyteEntity.WavelengthNm,
                    ReferenceStandardMaterialId = mat.Id,
                    ReferenceStandardMaterial = mat,
                    StandardPurityPercent = mat.Purity.Value,
                    StandardWeightMg = reqRow.StandardWeightMg,
                    StandardDilution = reqRow.StandardDilution,
                    StandardMeanArea = reqRow.StandardMeanArea,
                    RsdPercent = reqRow.RsdPercent,
                    Resolution = reqRow.Resolution,
                    TailingFactor = reqRow.TailingFactor,
                    TheoreticalPlates = reqRow.TheoreticalPlates,
                    Passed = rowPassed,
                    FailureReasons = rowFailureReasons
                };

                runAnalytes.Add(runAnalyte);
            }

            passed = runAnalytes.All(a => a.Passed);
            failureReasons = passed ? null : string.Join("; ", allFailures);

            var first = runAnalytes[0];
            runRefMatId = first.ReferenceStandardMaterialId;
            runPurity = first.StandardPurityPercent;
            runWeight = first.StandardWeightMg;
            runDilution = first.StandardDilution;
            runMeanArea = first.StandardMeanArea;
            runRsd = null;
            runResolution = null;
            runTailing = null;
            runPlates = null;
        }
        else
        {
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
            var eval = EvaluateAcceptanceCriteria(
                test,
                request.RsdPercent,
                request.Resolution,
                request.TailingFactor,
                request.TheoreticalPlates);

            passed = eval.Passed;
            failureReasons = eval.FailureReasons;
            runRefMatId = material.Id;
            runPurity = material.Purity.Value;
            runWeight = request.StandardWeightMg;
            runDilution = request.StandardDilution;
            runMeanArea = request.StandardMeanArea;
            runRsd = request.RsdPercent;
            runResolution = request.Resolution;
            runTailing = request.TailingFactor;
            runPlates = request.TheoreticalPlates;
        }

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
            ReferenceStandardMaterialId = runRefMatId,
            StandardPurityPercent = runPurity,
            StandardWeightMg = runWeight,
            StandardDilution = runDilution,
            StandardMeanArea = runMeanArea,
            RsdPercent = runRsd,
            Resolution = runResolution,
            TailingFactor = runTailing,
            TheoreticalPlates = runPlates,
            Passed = passed,
            FailureReasons = failureReasons,
            PerformedByUserId = userId,
            PerformedAt = now,
            Signature = signature,
            Comment = request.Comment,
            Analytes = runAnalytes
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
            .Include(r => r.Analytes)
                .ThenInclude(a => a.ReferenceStandardMaterial)
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
            .Include(r => r.Analytes)
                .ThenInclude(a => a.ReferenceStandardMaterial)
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

        var runAnalytes = await _db.SystemSuitabilityRunAnalytes.AsNoTracking()
            .Where(a => a.SystemSuitabilityRunId == runId)
            .Include(a => a.ReferenceStandardMaterial)
            .Include(a => a.TestAnalyte)
            .OrderBy(a => a.TestAnalyte != null ? a.TestAnalyte.DisplayOrder : 0)
            .ThenBy(a => a.Id)
            .ToListAsync(ct);

        List<SuitabilityRunReportAnalyteDto>? reportAnalytes = runAnalytes.Count > 0
            ? runAnalytes.Select(a => new SuitabilityRunReportAnalyteDto(
                a.TestAnalyteId,
                a.AnalyteName,
                a.WavelengthNm,
                a.ReferenceStandardMaterial?.MaterialName,
                a.ReferenceStandardMaterial?.BatchNumber,
                a.StandardPurityPercent,
                a.StandardWeightMg,
                a.StandardDilution,
                a.StandardMeanArea,
                a.RsdPercent,
                a.Resolution,
                a.TailingFactor,
                a.TheoreticalPlates,
                a.TestAnalyte?.SstMaxRsdPercent,
                a.TestAnalyte?.SstMinResolution,
                a.TestAnalyte?.SstMaxTailingFactor,
                a.TestAnalyte?.SstMinTheoreticalPlates,
                a.Passed,
                a.FailureReasons)).ToList()
            : null;

        return new SuitabilityRunReportDetailsDto(
            run.SstMaxRsdPercent, run.SstMinResolution, run.SstMaxTailingFactor, run.SstMinTheoreticalPlates,
            run.Vendor, run.CdsSoftware?.ToString(), run.ColumnSerial, signature,
            linked.Select(l => new SuitabilityRunLinkedTestDto(
                l.Id, l.SampleId, l.ReferenceNumber, l.ItemName, l.BatchNumber, l.TestCode,
                l.Result?.ReportedResult, l.Result?.ComparisonStatus, l.Result?.EnteredAt)).ToList(),
            reportAnalytes);
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
            .Include(r => r.Analytes)
                .ThenInclude(a => a.ReferenceStandardMaterial)
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
            .Include(r => r.Analytes)
                .ThenInclude(a => a.ReferenceStandardMaterial)
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

            // Same for dissolution: its vessels (including a pending next stage) were calculated against this run's standard.
            var hasActiveAnalysis = await _db.TestAnalyses
                .AnyAsync(a => a.TestOrderId == order.Id && a.IsActive, ct);
            if (hasActiveAnalysis)
            {
                throw new InvalidOperationException($"Cannot link test order {order.Id} because an active result already exists for it.");
            }

            order.SystemSuitabilityRunId = run.Id;
        }

        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync(ct);
    }
}
