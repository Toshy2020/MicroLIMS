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

// Master data: test analyte. Behind TestAnalyteMasterDataController
// (api/masterdata/...); the controller only maps HTTP to these calls.
public class TestAnalyteMasterDataService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;

    public TestAnalyteMasterDataService(IMicroLimsDbContext db, IUserSectionScopeService scope)
    {
        _db = db;
        _scope = scope;
    }

    public async Task<object> GetTestAnalytesAsync(int id)
    {
        var test = await _db.TestDefinitions.FindAsync(id)
            ?? throw new NotFoundException($"Test {id} not found.");

        var analytes = await _db.TestAnalytes
            .Where(a => a.TestDefinitionId == id)
            .OrderBy(a => a.DisplayOrder)
            .ThenBy(a => a.Id)
            .ToListAsync();

        return analytes.Select(TestAnalyteDto.From);
    }

    public async Task<object> CreateTestAnalyteAsync(int currentUserId, int id, CreateTestAnalyteRequest request)
    {
        var test = await _db.TestDefinitions.FindAsync(id)
            ?? throw new NotFoundException($"Test {id} not found.");

        var scope = await _scope.GetAccessibleSectionIdsAsync(currentUserId);
        if (scope is not null && !scope.Contains(test.SectionId))
            throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");

        var isAnalyteBasedSst = test.WorkflowType == WorkflowType.StandardComparison || test.EquationType == EquationType.StandardComparison;

        if (string.IsNullOrWhiteSpace(request.Element))
            throw new InvalidOperationException("Element is required.");

        var element = request.Element.Trim();
        if (element.Length > 20)
            throw new InvalidOperationException(isAnalyteBasedSst ? "Analyte name cannot exceed 20 characters." : "Element symbol cannot exceed 20 characters.");

        if (request.WavelengthNm <= 0)
            throw new InvalidOperationException("Wavelength must be greater than 0.");

        if (isAnalyteBasedSst)
        {
            if (request.View.HasValue)
                throw new InvalidOperationException("View is not allowed for analyte-based tests.");

            if (request.LoqMgPerL.HasValue && request.LoqMgPerL.Value <= 0)
                throw new InvalidOperationException("LOQ must be greater than 0.");

            if (request.SstMaxRsdPercent.HasValue && request.SstMaxRsdPercent.Value <= 0)
                throw new InvalidOperationException("SST max RSD percent must be greater than 0.");
            if (request.SstMinResolution.HasValue && request.SstMinResolution.Value <= 0)
                throw new InvalidOperationException("SST min resolution must be greater than 0.");
            if (request.SstMaxTailingFactor.HasValue && request.SstMaxTailingFactor.Value <= 0)
                throw new InvalidOperationException("SST max tailing factor must be greater than 0.");
            if (request.SstMinTheoreticalPlates.HasValue && request.SstMinTheoreticalPlates.Value <= 0)
                throw new InvalidOperationException("SST min theoretical plates must be greater than 0.");
        }
        else
        {
            // AAS calibration-curve tests have no plasma view (torch-only ICP-OES
            // concept); only require it for ICP-OES (CalInstrumentType null defaults
            // to ICP-OES for legacy tests).
            var isAas = test.CalInstrumentType == EquipmentType.Aas;
            if (!isAas && !request.View.HasValue)
                throw new InvalidOperationException("View is required for calibration curve tests.");

            if (!request.LoqMgPerL.HasValue || request.LoqMgPerL.Value <= 0)
                throw new InvalidOperationException("LOQ must be greater than 0.");

            if (request.SstMaxRsdPercent.HasValue || request.SstMinResolution.HasValue ||
                request.SstMaxTailingFactor.HasValue || request.SstMinTheoreticalPlates.HasValue)
            {
                throw new InvalidOperationException("SST criteria are not allowed for calibration curve tests.");
            }
        }

        if (await _db.TestAnalytes.AnyAsync(a => a.TestDefinitionId == id && a.Element == element && a.WavelengthNm == request.WavelengthNm))
            throw new InvalidOperationException($"Analyte {element} at {request.WavelengthNm} nm already exists for this test definition.");

        var entity = new TestAnalyte
        {
            TestDefinitionId = id,
            Element = element,
            WavelengthNm = request.WavelengthNm,
            View = (isAnalyteBasedSst || test.CalInstrumentType == EquipmentType.Aas) ? null : request.View,
            LoqMgPerL = request.LoqMgPerL,
            DisplayOrder = request.DisplayOrder,
            SstMaxRsdPercent = isAnalyteBasedSst ? request.SstMaxRsdPercent : null,
            SstMinResolution = isAnalyteBasedSst ? request.SstMinResolution : null,
            SstMaxTailingFactor = isAnalyteBasedSst ? request.SstMaxTailingFactor : null,
            SstMinTheoreticalPlates = isAnalyteBasedSst ? request.SstMinTheoreticalPlates : null,
            IsActive = true
        };

        _db.TestAnalytes.Add(entity);
        await _db.SaveChangesAsync();
        return TestAnalyteDto.From(entity);
    }

    public async Task<object> UpdateTestAnalyteAsync(int currentUserId, int id, int analyteId, UpdateTestAnalyteRequest request)
    {
        var test = await _db.TestDefinitions.FindAsync(id)
            ?? throw new NotFoundException($"Test {id} not found.");

        var scope = await _scope.GetAccessibleSectionIdsAsync(currentUserId);
        if (scope is not null && !scope.Contains(test.SectionId))
            throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");

        var analyte = await _db.TestAnalytes.FirstOrDefaultAsync(a => a.Id == analyteId && a.TestDefinitionId == id)
            ?? throw new NotFoundException($"Analyte {analyteId} not found for test {id}.");
        RecordVersion.EnsureCurrent(_db, analyte);

        var isAnalyteBasedSst = test.WorkflowType == WorkflowType.StandardComparison || test.EquationType == EquationType.StandardComparison;

        var effectiveElement = request.Element != null ? request.Element.Trim() : analyte.Element;
        var effectiveWavelength = request.WavelengthNm ?? analyte.WavelengthNm;

        if (string.IsNullOrWhiteSpace(effectiveElement))
            throw new InvalidOperationException("Element is required.");
        if (effectiveElement.Length > 20)
            throw new InvalidOperationException(isAnalyteBasedSst ? "Analyte name cannot exceed 20 characters." : "Element symbol cannot exceed 20 characters.");
        if (effectiveWavelength <= 0)
            throw new InvalidOperationException("Wavelength must be greater than 0.");

        if (isAnalyteBasedSst)
        {
            if (request.View.HasValue)
                throw new InvalidOperationException("View is not allowed for analyte-based tests.");

            if (request.LoqMgPerL.HasValue)
            {
                if (request.LoqMgPerL.Value <= 0)
                    throw new InvalidOperationException("LOQ must be greater than 0.");
                analyte.LoqMgPerL = request.LoqMgPerL.Value;
            }

            if (request.SstMaxRsdPercent.HasValue)
            {
                if (request.SstMaxRsdPercent.Value <= 0)
                    throw new InvalidOperationException("SST max RSD percent must be greater than 0.");
                analyte.SstMaxRsdPercent = request.SstMaxRsdPercent.Value;
            }
            if (request.SstMinResolution.HasValue)
            {
                if (request.SstMinResolution.Value <= 0)
                    throw new InvalidOperationException("SST min resolution must be greater than 0.");
                analyte.SstMinResolution = request.SstMinResolution.Value;
            }
            if (request.SstMaxTailingFactor.HasValue)
            {
                if (request.SstMaxTailingFactor.Value <= 0)
                    throw new InvalidOperationException("SST max tailing factor must be greater than 0.");
                analyte.SstMaxTailingFactor = request.SstMaxTailingFactor.Value;
            }
            if (request.SstMinTheoreticalPlates.HasValue)
            {
                if (request.SstMinTheoreticalPlates.Value <= 0)
                    throw new InvalidOperationException("SST min theoretical plates must be greater than 0.");
                analyte.SstMinTheoreticalPlates = request.SstMinTheoreticalPlates.Value;
            }
        }
        else
        {
            if (request.View.HasValue) analyte.View = request.View.Value;
            if (request.LoqMgPerL.HasValue)
            {
                if (request.LoqMgPerL.Value <= 0)
                    throw new InvalidOperationException("LOQ must be greater than 0.");
                analyte.LoqMgPerL = request.LoqMgPerL.Value;
            }
            if (request.SstMaxRsdPercent.HasValue || request.SstMinResolution.HasValue ||
                request.SstMaxTailingFactor.HasValue || request.SstMinTheoreticalPlates.HasValue)
            {
                throw new InvalidOperationException("SST criteria are not allowed for calibration curve tests.");
            }
        }

        if (await _db.TestAnalytes.AnyAsync(a => a.TestDefinitionId == id && a.Id != analyteId && a.Element == effectiveElement && a.WavelengthNm == effectiveWavelength))
            throw new InvalidOperationException($"Analyte {effectiveElement} at {effectiveWavelength} nm already exists for this test definition.");

        analyte.Element = effectiveElement;
        analyte.WavelengthNm = effectiveWavelength;
        if (request.DisplayOrder.HasValue) analyte.DisplayOrder = request.DisplayOrder.Value;
        if (request.IsActive.HasValue) analyte.IsActive = request.IsActive.Value;

        await _db.SaveChangesAsync();
        return TestAnalyteDto.From(analyte);
    }

    public async Task<object> DeleteTestAnalyteAsync(int currentUserId, int id, int analyteId)
    {
        var test = await _db.TestDefinitions.FindAsync(id)
            ?? throw new NotFoundException($"Test {id} not found.");

        var scope = await _scope.GetAccessibleSectionIdsAsync(currentUserId);
        if (scope is not null && !scope.Contains(test.SectionId))
            throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");

        var analyte = await _db.TestAnalytes.FirstOrDefaultAsync(a => a.Id == analyteId && a.TestDefinitionId == id)
            ?? throw new NotFoundException($"Analyte {analyteId} not found for test {id}.");

        var inUseCalibration = await _db.CalibrationRunAnalytes.AnyAsync(r => r.TestAnalyteId == analyteId);
        var inUseSuitability = await _db.SystemSuitabilityRunAnalytes.AnyAsync(r => r.TestAnalyteId == analyteId);
        if (inUseCalibration || inUseSuitability)
        {
            analyte.IsActive = false;
            await _db.SaveChangesAsync();
            var runType = inUseCalibration && inUseSuitability ? "calibration and suitability runs"
                : inUseCalibration ? "calibration runs"
                : "suitability runs";
            return new
            {
                message = $"Analyte {analyte.Element} ({analyte.WavelengthNm} nm) is referenced by {runType} and has been deactivated instead of deleted.",
                deactivated = true
            };
        }

        _db.TestAnalytes.Remove(analyte);
        await _db.SaveChangesAsync();
        return new
        {
            message = $"Analyte {analyte.Element} ({analyte.WavelengthNm} nm) deleted successfully.",
            deleted = true
        };
    }
}
