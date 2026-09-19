using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Storage;
using MicroLIMS.Persistence.Configurations;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;

namespace MicroLIMS.Application.Services;

public class CalibrationRunService : ICalibrationRunService
{
    private readonly MicroLimsDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly IElectronicSignatureService _signatureService;
    private readonly IUserSectionScopeService _scope;
    private readonly ILabClock _clock;
    private readonly ILogger<CalibrationRunService> _logger;

    public CalibrationRunService(
        MicroLimsDbContext db,
        IFileStorageService storage,
        IElectronicSignatureService signatureService,
        IUserSectionScopeService scope,
        ILogger<CalibrationRunService> logger,
        ILabClock? clock = null)
    {
        _db = db;
        _storage = storage;
        _signatureService = signatureService;
        _scope = scope;
        _logger = logger;
        _clock = clock ?? LabClock.Default;
    }

    public static (bool Passed, string? Reason) EvaluateCorrelation(
        decimal runValue,
        CorrelationType runType,
        decimal? minCriterion,
        CorrelationType? criterionType)
    {
        if (runValue <= 0m || runValue > 1m)
        {
            return (false, $"Correlation value ({runValue}) must be in (0, 1].");
        }

        if (!minCriterion.HasValue) return (true, null);

        bool passed;
        if (criterionType.HasValue && criterionType.Value != runType)
        {
            if (criterionType.Value == CorrelationType.RSquared && runType == CorrelationType.R)
            {
                // Run reports r, criterion is r^2 -> compare r * r >= min
                passed = (runValue * runValue) >= minCriterion.Value;
            }
            else // criterionType.Value == CorrelationType.R && runType == CorrelationType.RSquared
            {
                // Run reports r^2, criterion is r -> compare r^2 >= min * min
                passed = runValue >= (minCriterion.Value * minCriterion.Value);
            }
        }
        else
        {
            passed = runValue >= minCriterion.Value;
        }

        if (!passed)
        {
            return (false, $"Correlation ({runValue} {runType}) is below minimum limit / does not meet criterion ({minCriterion.Value} {criterionType}).");
        }

        return (true, null);
    }

    private record EvaluatedCheck(
        CalibrationCheckType CheckType,
        int SequencePosition,
        decimal? NominalMgPerL,
        decimal MeasuredMgPerL,
        decimal? RecoveryPercent,
        bool Passed);

    private record EvaluatedAnalyte(
        TestAnalyte TestAnalyte,
        decimal CorrelationValue,
        CorrelationType CorrelationType,
        int NumberOfStandards,
        decimal LowestStandardMgPerL,
        decimal HighestStandardMgPerL,
        bool Passed,
        string? FailureReasons,
        List<EvaluatedCheck> Checks);

    private record EvaluatedRun(
        TestDefinition Test,
        Equipment Equipment,
        Material CalStandard,
        Material? IcvStandard,
        DateTime CalibrationAtUtc,
        bool StandardsExpiredOrMissing,
        string? StandardsExpiryFailureReason,
        int AnalytesPassed,
        int AnalytesTotal,
        bool Passed,
        List<EvaluatedAnalyte> Analytes);

    private async Task<EvaluatedRun> EvaluateRunAsync(
        int testDefinitionId,
        int equipmentId,
        int calibrationStandardMaterialId,
        int? icvStandardMaterialId,
        DateTime calibrationAt,
        List<CreateCalibrationRunAnalyteRequest>? analyteRequests,
        int userId,
        CancellationToken ct)
    {
        var test = await _db.TestDefinitions
            .Include(t => t.Analytes)
            .FirstOrDefaultAsync(t => t.Id == testDefinitionId, ct)
            ?? throw new InvalidOperationException($"Test definition {testDefinitionId} not found.");

        if (test.EquationType != EquationType.CalibrationCurve)
            throw new InvalidOperationException("Test definition does not use the Calibration Curve equation.");

        if (string.IsNullOrWhiteSpace(test.MethodAbbreviation))
            throw new InvalidOperationException("Test definition does not have a method abbreviation configured.");

        // Section scoping guard
        var scope = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
        if (scope != null && !scope.Contains(test.SectionId))
            throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");

        // Equipment validation: ICP-OES + same section
        var equip = await _db.Equipment.FirstOrDefaultAsync(e => e.Id == equipmentId, ct)
            ?? throw new InvalidOperationException($"Equipment {equipmentId} not found.");

        if (equip.Type != EquipmentType.IcpOes)
            throw new InvalidOperationException("Selected equipment must be an ICP-OES instrument.");

        if (equip.SectionId != test.SectionId)
            throw new InvalidOperationException("Equipment belongs to a different laboratory section than the test definition.");

        // Calibration standard material validation

        var calStandard = await _db.Materials.FirstOrDefaultAsync(m => m.Id == calibrationStandardMaterialId, ct)
            ?? throw new InvalidOperationException($"Calibration standard material {calibrationStandardMaterialId} not found.");

        if (calStandard.SectionId != test.SectionId)
            throw new InvalidOperationException("Calibration standard belongs to a different laboratory section than the test definition.");

        // ICV standard material validation (if provided)
        Material? icvStandard = null;
        if (icvStandardMaterialId.HasValue)
        {
            icvStandard = await _db.Materials.FirstOrDefaultAsync(m => m.Id == icvStandardMaterialId.Value, ct)
                ?? throw new InvalidOperationException($"ICV standard material {icvStandardMaterialId.Value} not found.");

            if (icvStandard.SectionId != test.SectionId)
                throw new InvalidOperationException("ICV standard belongs to a different laboratory section than the test definition.");
        }

        // CalibrationAt validation (tolerance: cannot be in the future beyond 5 minutes)
        var calAtUtc = calibrationAt == default
            ? _clock.UtcNow.UtcDateTime
            : (calibrationAt.Kind == DateTimeKind.Utc ? calibrationAt : DateTime.SpecifyKind(calibrationAt, DateTimeKind.Utc));
        var nowUtc = _clock.UtcNow.UtcDateTime;
        if (calAtUtc > nowUtc.AddMinutes(5))
            throw new InvalidOperationException("Calibration time cannot be in the future (exceeds 5-minute tolerance).");

        // Analytes validation
        if (analyteRequests == null || analyteRequests.Count == 0)
            throw new InvalidOperationException("At least one analyte is required for a calibration run.");

        // Standards expiry check compares against the lab-local date of CalibrationAt
        var calLabDate = DateOnly.FromDateTime(_clock.ToLabLocal(calAtUtc));
        bool calStandardExpiredOrMissing = !calStandard.ExpiryDate.HasValue || DateOnly.FromDateTime(calStandard.ExpiryDate.Value.Date) < calLabDate;
        bool icvStandardExpiredOrMissing = icvStandard != null && (!icvStandard.ExpiryDate.HasValue || DateOnly.FromDateTime(icvStandard.ExpiryDate.Value.Date) < calLabDate);
        bool standardsGateFailed = calStandardExpiredOrMissing || icvStandardExpiredOrMissing;
        string? standardsExpiryReason = standardsGateFailed ? "standard expired / no expiry date" : null;

        var evaluatedAnalytes = new List<EvaluatedAnalyte>();

        foreach (var analyteReq in analyteRequests)
        {
            var testAnalyte = test.Analytes.FirstOrDefault(a => a.Id == analyteReq.TestAnalyteId)
                ?? await _db.TestAnalytes.FirstOrDefaultAsync(a => a.Id == analyteReq.TestAnalyteId && a.TestDefinitionId == test.Id, ct)
                ?? throw new InvalidOperationException($"Analyte {analyteReq.TestAnalyteId} is not configured for test {test.Code}.");

            if (!testAnalyte.IsActive)
                throw new InvalidOperationException($"Analyte {testAnalyte.Element} ({testAnalyte.WavelengthNm} nm) is inactive.");

            // Check IS without configured window rule
            if (analyteReq.Checks != null && analyteReq.Checks.Any(c => c.CheckType == CalibrationCheckType.InternalStandard))
            {
                if (!test.CalIsRecoveryLowPercent.HasValue || !test.CalIsRecoveryHighPercent.HasValue)
                {
                    throw new InvalidOperationException("Internal standard checks cannot be submitted when no internal standard recovery window is configured.");
                }
            }

            var failures = new List<string>();

            if (standardsGateFailed)
            {
                failures.Add(standardsExpiryReason!);
            }

            // Correlation check
            var (corrPassed, corrReason) = EvaluateCorrelation(
                analyteReq.CorrelationValue,
                analyteReq.CorrelationType,
                test.CalMinCorrelation,
                test.CalCorrelationType);
            if (!corrPassed && corrReason != null)
            {
                failures.Add(corrReason);
            }

            // Number of standards check
            if (test.CalMinStandards.HasValue && analyteReq.NumberOfStandards < test.CalMinStandards.Value)
            {
                failures.Add($"Number of standards ({analyteReq.NumberOfStandards}) is below minimum required ({test.CalMinStandards.Value}).");
            }

            // Required checks per configuration (A4)
            if (test.CalRequireBlank == true && (analyteReq.Checks == null || !analyteReq.Checks.Any(c => c.CheckType == CalibrationCheckType.Blank)))
            {
                failures.Add("Analyte is missing required Blank check.");
            }
            if (test.CalRequireIcv == true && (analyteReq.Checks == null || !analyteReq.Checks.Any(c => c.CheckType == CalibrationCheckType.Icv)))
            {
                failures.Add("Analyte is missing required ICV check.");
            }
            if (test.CalRequireCcv == true && (analyteReq.Checks == null || !analyteReq.Checks.Any(c => c.CheckType == CalibrationCheckType.Ccv)))
            {
                failures.Add("Analyte is missing required CCV check.");
            }
            if (test.CalRequireInternalStandard == true && (analyteReq.Checks == null || !analyteReq.Checks.Any(c => c.CheckType == CalibrationCheckType.InternalStandard)))
            {
                failures.Add("Analyte is missing required Internal Standard check.");
            }

            var evaluatedChecks = new List<EvaluatedCheck>();
            if (analyteReq.Checks != null)
            {
                foreach (var checkReq in analyteReq.Checks)
                {
                    bool checkPassed = true;
                    decimal? recoveryPercent = null;

                    if (checkReq.CheckType == CalibrationCheckType.Blank)
                    {
                        var maxBlank = test.CalBlankMax ?? testAnalyte.LoqMgPerL;
                        if (checkReq.MeasuredMgPerL > maxBlank)
                        {
                            checkPassed = false;
                            failures.Add($"Blank measured ({checkReq.MeasuredMgPerL} mg/L) exceeds maximum limit ({maxBlank} mg/L) at sequence {checkReq.SequencePosition}.");
                        }
                    }
                    else if (checkReq.CheckType == CalibrationCheckType.Icv || checkReq.CheckType == CalibrationCheckType.Ccv)
                    {
                        if (!checkReq.NominalMgPerL.HasValue || checkReq.NominalMgPerL.Value <= 0m)
                            throw new InvalidOperationException($"Nominal concentration must be greater than 0 for {checkReq.CheckType} check at sequence {checkReq.SequencePosition}.");

                        recoveryPercent = (checkReq.MeasuredMgPerL * 100m) / checkReq.NominalMgPerL.Value;

                        if (test.CalCheckRecoveryLowPercent.HasValue && recoveryPercent.Value < test.CalCheckRecoveryLowPercent.Value ||
                            test.CalCheckRecoveryHighPercent.HasValue && recoveryPercent.Value > test.CalCheckRecoveryHighPercent.Value)
                        {
                            checkPassed = false;
                            failures.Add($"{checkReq.CheckType} recovery ({recoveryPercent.Value}%) is outside acceptance window [{test.CalCheckRecoveryLowPercent}%, {test.CalCheckRecoveryHighPercent}%] at sequence {checkReq.SequencePosition}.");
                        }
                    }
                    else if (checkReq.CheckType == CalibrationCheckType.InternalStandard)
                    {
                        if (!checkReq.NominalMgPerL.HasValue || checkReq.NominalMgPerL.Value <= 0m)
                            throw new InvalidOperationException($"Nominal concentration must be greater than 0 for Internal Standard check at sequence {checkReq.SequencePosition}.");

                        recoveryPercent = (checkReq.MeasuredMgPerL * 100m) / checkReq.NominalMgPerL.Value;

                        if (recoveryPercent.Value < test.CalIsRecoveryLowPercent!.Value ||
                            recoveryPercent.Value > test.CalIsRecoveryHighPercent!.Value)
                        {
                            checkPassed = false;
                            failures.Add($"Internal Standard recovery ({recoveryPercent.Value}%) is outside acceptance window [{test.CalIsRecoveryLowPercent}%, {test.CalIsRecoveryHighPercent}%] at sequence {checkReq.SequencePosition}.");
                        }
                    }

                    evaluatedChecks.Add(new EvaluatedCheck(
                        checkReq.CheckType,
                        checkReq.SequencePosition,
                        checkReq.NominalMgPerL,
                        checkReq.MeasuredMgPerL,
                        recoveryPercent,
                        checkPassed));
                }
            }

            bool analytePassed = failures.Count == 0;
            string? failureReasonsStr = failures.Count > 0 ? string.Join("; ", failures) : null;

            evaluatedAnalytes.Add(new EvaluatedAnalyte(
                testAnalyte,
                analyteReq.CorrelationValue,
                analyteReq.CorrelationType,
                analyteReq.NumberOfStandards,
                analyteReq.LowestStandardMgPerL,
                analyteReq.HighestStandardMgPerL,
                analytePassed,
                failureReasonsStr,
                evaluatedChecks));
        }

        int passedCount = evaluatedAnalytes.Count(a => a.Passed);
        int totalCount = evaluatedAnalytes.Count;
        bool runPassed = passedCount == totalCount && totalCount > 0;

        return new EvaluatedRun(
            test,
            equip,
            calStandard,
            icvStandard,
            calAtUtc,
            standardsGateFailed,
            standardsExpiryReason,
            passedCount,
            totalCount,
            runPassed,
            evaluatedAnalytes);
    }

    public async Task<CalibrationRunPreviewResult> PreviewAsync(
        PreviewCalibrationRunRequest request,
        int userId,
        CancellationToken ct = default)
    {
        var eval = await EvaluateRunAsync(
            request.TestDefinitionId,
            request.EquipmentId,
            request.CalibrationStandardMaterialId,
            request.IcvStandardMaterialId,
            request.CalibrationAt,
            request.Analytes,
            userId,
            ct);

        var previewAnalytes = eval.Analytes.Select(a => new CalibrationRunPreviewAnalyteResult(
            a.TestAnalyte.Id,
            a.TestAnalyte.Element,
            a.TestAnalyte.WavelengthNm,
            a.TestAnalyte.View,
            a.CorrelationValue,
            a.CorrelationType,
            a.NumberOfStandards,
            a.LowestStandardMgPerL,
            a.HighestStandardMgPerL,
            a.Passed,
            a.FailureReasons,
            a.Passed,
            a.Checks.Select(c => new CalibrationRunPreviewCheckResult(
                c.CheckType,
                c.SequencePosition,
                c.NominalMgPerL,
                c.MeasuredMgPerL,
                c.RecoveryPercent,
                c.Passed)).ToList()
        )).ToList();

        return new CalibrationRunPreviewResult(
            eval.Test.Id,
            eval.Equipment.Id,
            eval.CalStandard.Id,
            eval.IcvStandard?.Id,
            eval.CalibrationAtUtc,
            eval.StandardsExpiredOrMissing,
            eval.StandardsExpiryFailureReason,
            eval.AnalytesPassed,
            eval.AnalytesTotal,
            eval.Passed,
            previewAnalytes);
    }

    public async Task<CalibrationRun> CreateAsync(
        CreateCalibrationRunRequest request,
        Stream fileStream,
        string originalFileName,
        string declaredContentType,
        int userId,
        string? ipAddress,
        CancellationToken ct = default)
    {
        if (fileStream == null)
            throw new InvalidOperationException("A calibration report file is required.");

        byte[] fileBytes;
        using (var ms = new MemoryStream())
        {
            await fileStream.CopyToAsync(ms, ct);
            fileBytes = ms.ToArray();
        }

        if (fileBytes.Length == 0)
            throw new InvalidOperationException("The uploaded file is empty.");

        // Single code path evaluation (never trusts a preview)
        var eval = await EvaluateRunAsync(
            request.TestDefinitionId,
            request.EquipmentId,
            request.CalibrationStandardMaterialId,
            request.IcvStandardMaterialId,
            request.CalibrationAt,
            request.Analytes,
            userId,
            ct);

        // Electronic signature check and creation
        var signature = await _signatureService.SignAsync(
            userId,
            request.Password,
            SignatureMeaning.CalibrationRunPerformed,
            "TestDefinition",
            eval.Test.Id,
            request.Comment,
            ipAddress);


        var now = _clock.UtcNow.UtcDateTime;
        var methodAbbr = eval.Test.MethodAbbreviation!.Trim().ToUpperInvariant();

        var run = new CalibrationRun
        {
            TestDefinitionId = eval.Test.Id,
            SectionId = eval.Test.SectionId,
            EquipmentId = eval.Equipment.Id,
            CalibrationStandardMaterialId = eval.CalStandard.Id,
            IcvStandardMaterialId = eval.IcvStandard?.Id,
            CalibrationAt = eval.CalibrationAtUtc,
            PerformedByUserId = userId,
            PerformedAt = now,
            Signature = signature,
            Comment = request.Comment,
            AnalytesPassed = eval.AnalytesPassed,
            AnalytesTotal = eval.AnalytesTotal,
            Passed = eval.Passed,
            Status = CalibrationRunStatus.Active
        };

        foreach (var evalAnalyte in eval.Analytes)
        {
            var runAnalyte = new CalibrationRunAnalyte
            {
                TestAnalyteId = evalAnalyte.TestAnalyte.Id,
                Element = evalAnalyte.TestAnalyte.Element,
                WavelengthNm = evalAnalyte.TestAnalyte.WavelengthNm,
                View = evalAnalyte.TestAnalyte.View,
                CorrelationValue = evalAnalyte.CorrelationValue,
                CorrelationType = evalAnalyte.CorrelationType,
                NumberOfStandards = evalAnalyte.NumberOfStandards,
                LowestStandardMgPerL = evalAnalyte.LowestStandardMgPerL,
                HighestStandardMgPerL = evalAnalyte.HighestStandardMgPerL,
                Passed = evalAnalyte.Passed,
                FailureReasons = evalAnalyte.FailureReasons
            };

            foreach (var evalCheck in evalAnalyte.Checks)
            {
                runAnalyte.Checks.Add(new CalibrationRunCheck
                {
                    CheckType = evalCheck.CheckType,
                    SequencePosition = evalCheck.SequencePosition,
                    NominalMgPerL = evalCheck.NominalMgPerL,
                    MeasuredMgPerL = evalCheck.MeasuredMgPerL,
                    RecoveryPercent = evalCheck.RecoveryPercent,
                    Passed = evalCheck.Passed
                });
            }

            run.Analytes.Add(runAnalyte);
        }

        // Code generation with lab-local time
        run.Code = await SystemSuitabilityRunCode.NextAsync(
            _db.CalibrationRuns.Select(r => r.Code),
            methodAbbr,
            run.PerformedAt,
            _clock,
            "CAL",
            ct);

        // Document handling
        var ext = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".pdf";
        var storageKey = await _storage.SaveAsync($"calibration-runs/{Guid.NewGuid()}{ext}", fileBytes);

        string hashHex;

        using (var sha = SHA256.Create())
        {
            hashHex = Convert.ToHexString(sha.ComputeHash(fileBytes)).ToLowerInvariant();
        }

        run.Document = new CalibrationRunDocument
        {
            StorageKey = storageKey,
            OriginalFileName = Path.GetFileName(originalFileName),
            ContentType = string.IsNullOrWhiteSpace(declaredContentType) ? "application/octet-stream" : declaredContentType,
            SizeBytes = fileBytes.Length,
            ContentSha256 = hashHex,
            UploadedByUserId = userId,
            UploadedAt = now
        };

        _db.CalibrationRuns.Add(run);

        if (!await UniqueIndexSave.TrySaveChangesAsync(_db, CalibrationRunConfiguration.CodeIndexName))
        {
            run.Code = await SystemSuitabilityRunCode.NextAsync(
                _db.CalibrationRuns.Select(r => r.Code),
                methodAbbr,
                run.PerformedAt,
                _clock,
                "CAL",
                ct);

            if (!await UniqueIndexSave.TrySaveChangesAsync(_db, CalibrationRunConfiguration.CodeIndexName))
            {
                throw new InvalidOperationException(
                    $"Calibration run code {run.Code} was taken by another run at the same moment. Nothing was saved - submit the run again.");
            }
        }

        _logger.LogInformation(
            "Calibration run {Code} created for test {TestCode} ({Passed}/{Total} analytes passed)",
            run.Code, eval.Test.Code, run.AnalytesPassed, run.AnalytesTotal);

        return run;
    }

    public async Task<CalibrationRun> WithdrawAsync(
        int id,
        WithdrawCalibrationRunRequest request,
        int userId,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var run = await _db.CalibrationRuns
            .Include(r => r.TestDefinition)
            .Include(r => r.Section)
            .Include(r => r.Analytes)
                .ThenInclude(a => a.Checks)
            .Include(r => r.Document)
            .Include(r => r.Signature)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new InvalidOperationException($"Calibration run {id} not found.");

        await _scope.EnsureCalibrationRunAccessAsync(userId, run.Id, ct);

        if (run.Status == CalibrationRunStatus.Withdrawn)
            throw new InvalidOperationException("Calibration run is already withdrawn.");

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 10)
            throw new InvalidOperationException("Withdrawal reason must be at least 10 characters long.");

        var signature = await _signatureService.SignAsync(
            userId,
            request.Password,
            SignatureMeaning.CalibrationRunWithdrawn,
            "TestDefinition",
            run.TestDefinitionId,
            request.Reason.Trim(),
            ipAddress);


        run.Status = CalibrationRunStatus.Withdrawn;
        run.WithdrawnAt = _clock.UtcNow.UtcDateTime;
        run.WithdrawnByUserId = userId;
        run.WithdrawalReason = request.Reason.Trim();
        run.WithdrawalSignature = signature;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Calibration run {Code} (Id: {Id}) withdrawn by user {UserId}. Reason: {Reason}",
            run.Code, run.Id, userId, run.WithdrawalReason);

        return run;
    }

    public async Task<List<CalibrationRun>> GetAllAsync(
        CalibrationRunFilter filter,
        int userId,
        CancellationToken ct = default)
    {
        var query = _db.CalibrationRuns
            .Include(r => r.TestDefinition)
            .Include(r => r.Section)
            .Include(r => r.Equipment)
            .Include(r => r.CalibrationStandardMaterial)
            .Include(r => r.IcvStandardMaterial)
            .Include(r => r.PerformedByUser)
            .Include(r => r.Signature)
            .Include(r => r.WithdrawnByUser)
            .Include(r => r.WithdrawalSignature)
            .Include(r => r.Document)
            .Include(r => r.Analytes)
                .ThenInclude(a => a.Checks)
            .AsNoTracking();

        var scope = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
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

        if (filter.Status.HasValue)
            query = query.Where(r => r.Status == filter.Status.Value);

        if (filter.Date.HasValue)
        {
            var d = filter.Date.Value.Date;
            query = query.Where(r => r.PerformedAt.Date == d);
        }

        if (filter.FromDate.HasValue)
            query = query.Where(r => r.PerformedAt >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(r => r.PerformedAt <= filter.ToDate.Value);

        return await query.OrderByDescending(r => r.PerformedAt).ToListAsync(ct);
    }

    public async Task<CalibrationRun?> GetByIdAsync(
        int id,
        int userId,
        CancellationToken ct = default)
    {
        var run = await _db.CalibrationRuns
            .Include(r => r.TestDefinition)
            .Include(r => r.Section)
            .Include(r => r.Equipment)
            .Include(r => r.CalibrationStandardMaterial)
            .Include(r => r.IcvStandardMaterial)
            .Include(r => r.PerformedByUser)
            .Include(r => r.Signature)
            .Include(r => r.WithdrawnByUser)
            .Include(r => r.WithdrawalSignature)
            .Include(r => r.Document)
            .Include(r => r.Analytes)
                .ThenInclude(a => a.Checks)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (run is null) return null;

        await _scope.EnsureCalibrationRunAccessAsync(userId, run.Id, ct);

        return run;
    }

    public async Task<CalibrationRunReportDetailsDto> GetReportDetailsAsync(
        int runId,
        int userId,
        CancellationToken ct = default)
    {
        var run = await GetByIdAsync(runId, userId, ct)
            ?? throw new InvalidOperationException($"Calibration run {runId} not found.");

        var test = run.TestDefinition
            ?? await _db.TestDefinitions.AsNoTracking().FirstOrDefaultAsync(t => t.Id == run.TestDefinitionId, ct);

        var equip = run.Equipment
            ?? await _db.Equipment.AsNoTracking().FirstOrDefaultAsync(e => e.Id == run.EquipmentId, ct);

        SignatureDto? sigDto = null;
        if (run.Signature != null)
        {
            sigDto = new SignatureDto(
                run.Signature.UserFullNameSnapshot,
                run.Signature.UsernameSnapshot,
                run.Signature.RoleSnapshot,
                run.Signature.MeaningOfSignature.ToString(),
                run.Signature.SignedAt,
                run.Signature.Comment);
        }

        SignatureDto? withSigDto = null;
        if (run.WithdrawalSignature != null)
        {
            withSigDto = new SignatureDto(
                run.WithdrawalSignature.UserFullNameSnapshot,
                run.WithdrawalSignature.UsernameSnapshot,
                run.WithdrawalSignature.RoleSnapshot,
                run.WithdrawalSignature.MeaningOfSignature.ToString(),
                run.WithdrawalSignature.SignedAt,
                run.WithdrawalSignature.Comment);
        }

        var docView = run.Document != null ? CalibrationRunDocumentView.From(run.Document) : null;
        var analyteViews = run.Analytes.Select(CalibrationRunAnalyteView.From).ToList();

        return new CalibrationRunReportDetailsDto(
            test?.CalibrationEntryMode,
            test?.CalMinCorrelation,
            test?.CalCorrelationType,
            test?.CalMinStandards,
            test?.CalCheckRecoveryLowPercent,
            test?.CalCheckRecoveryHighPercent,
            test?.CalBlankMax,
            test?.CalIsRecoveryLowPercent,
            test?.CalIsRecoveryHighPercent,
            test?.CalRequireBlank,
            test?.CalRequireIcv,
            test?.CalRequireCcv,
            test?.CalRequireInternalStandard,
            test?.ReportedConcentrationBasis,
            test?.CalMaxRunAgeHours,
            run.CalibrationAt,
            run.Status,
            run.WithdrawnAt,
            run.WithdrawnByUser?.FullName ?? run.WithdrawalSignature?.UserFullNameSnapshot,
            run.WithdrawalReason,
            withSigDto,
            equip?.Vendor,
            equip?.CdsSoftware.ToString(),
            sigDto,
            docView,
            analyteViews);
    }

    public async Task<(CalibrationRunDocument Document, byte[] Content)> GetDocumentContentAsync(
        int runId,
        int userId,
        CancellationToken ct = default)
    {
        var run = await GetByIdAsync(runId, userId, ct)
            ?? throw new InvalidOperationException($"Calibration run {runId} not found.");

        if (run.Document == null)
            throw new InvalidOperationException("This calibration run has no report document attached.");

        var content = await _storage.ReadAsync(run.Document.StorageKey);

        using (var sha = SHA256.Create())
        {

            var computedHash = Convert.ToHexString(sha.ComputeHash(content)).ToLowerInvariant();
            if (!string.Equals(computedHash, run.Document.ContentSha256, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError(
                    "Integrity check failed for calibration run document {DocumentId}. Expected {Expected}, got {Actual}",
                    run.Document.Id, run.Document.ContentSha256, computedHash);
                throw new InvalidOperationException("Document integrity verification failed. The file on disk does not match the stored checksum.");
            }
        }

        return (run.Document, content);
    }
}
