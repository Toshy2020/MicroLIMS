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

        if (string.IsNullOrWhiteSpace(request.Element))
            throw new InvalidOperationException("Element is required.");

        var element = request.Element.Trim();
        if (element.Length > 20)
            throw new InvalidOperationException("Element symbol cannot exceed 20 characters.");

        if (request.WavelengthNm <= 0)
            throw new InvalidOperationException("Wavelength must be greater than 0.");

        if (!request.View.HasValue)
            throw new InvalidOperationException("View is required for calibration curve tests.");

        if (!request.LoqMgPerL.HasValue || request.LoqMgPerL.Value <= 0)
            throw new InvalidOperationException("LOQ must be greater than 0.");

        if (request.SstMaxRsdPercent.HasValue || request.SstMinResolution.HasValue ||
            request.SstMaxTailingFactor.HasValue || request.SstMinTheoreticalPlates.HasValue)
        {
            throw new InvalidOperationException("SST criteria are not allowed for calibration curve tests.");
        }

        if (await _db.TestAnalytes.AnyAsync(a => a.TestDefinitionId == id && a.Element == element && a.WavelengthNm == request.WavelengthNm))
            throw new InvalidOperationException($"Analyte {element} at {request.WavelengthNm} nm already exists for this test definition.");

        var entity = new TestAnalyte
        {
            TestDefinitionId = id,
            Element = element,
            WavelengthNm = request.WavelengthNm,
            View = request.View,
            LoqMgPerL = request.LoqMgPerL,
            DisplayOrder = request.DisplayOrder,
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

        var effectiveElement = request.Element != null ? request.Element.Trim() : analyte.Element;
        var effectiveWavelength = request.WavelengthNm ?? analyte.WavelengthNm;

        if (string.IsNullOrWhiteSpace(effectiveElement))
            throw new InvalidOperationException("Element is required.");
        if (effectiveElement.Length > 20)
            throw new InvalidOperationException("Element symbol cannot exceed 20 characters.");
        if (effectiveWavelength <= 0)
            throw new InvalidOperationException("Wavelength must be greater than 0.");

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

        _db.TestAnalytes.Remove(analyte);
        await _db.SaveChangesAsync();
        return new
        {
            message = $"Analyte {analyte.Element} ({analyte.WavelengthNm} nm) deleted successfully.",
            deleted = true
        };
    }
}
