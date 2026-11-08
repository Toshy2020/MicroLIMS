using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.Monitoring;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.MasterData;

// Master data: water. Behind WaterMasterDataController
// (api/masterdata/...); the controller only maps HTTP to these calls.
public class WaterMasterDataService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;

    public WaterMasterDataService(IMicroLimsDbContext db, IUserSectionScopeService scope)
    {
        _db = db;
        _scope = scope;
    }

    public async Task<List<WaterSamplingPointResponse>> GetWaterSamplingPointsAsync() =>
        (await _db.WaterSamplingPoints.AsNoTracking()
            .Include(p => p.LabFrequencies).ThenInclude(f => f.Section)
            .ToListAsync()).Select(WaterSamplingPointResponse.From).ToList();

    public async Task<WaterPointLabFrequencyResponse> GetWaterPointLabFrequencyAsync(int currentUserId, int id, int sectionId)
    {
        var point = await _db.WaterSamplingPoints.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NotFoundException($"Sampling point {id} not found.");

        var section = await _db.DocumentSections.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sectionId)
            ?? throw new NotFoundException($"Section {sectionId} not found.");

        var existing = await _db.WaterPointLabFrequencies.AsNoTracking()
            .Include(f => f.Section)
            .FirstOrDefaultAsync(f => f.WaterSamplingPointId == id && f.SectionId == sectionId);

        if (existing is null)
            throw new NotFoundException($"No frequency override found for point {id} and section {sectionId}.");

        return WaterPointLabFrequencyResponse.From(existing);
    }

    public async Task<WaterPointLabFrequencyResponse> SetWaterPointLabFrequencyAsync(
        int currentUserId, int id, int sectionId, SetWaterPointLabFrequencyRequest request)
    {
        var point = await _db.WaterSamplingPoints.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NotFoundException($"Sampling point {id} not found.");

        var section = await _db.DocumentSections.FirstOrDefaultAsync(s => s.Id == sectionId)
            ?? throw new NotFoundException($"Section {sectionId} not found.");

        var configurable = await _scope.GetConfigurableSectionIdsAsync(currentUserId);
        if (configurable is not null && !configurable.Contains(sectionId))
            throw new UnauthorizedAccessException($"You do not have permission to configure section {sectionId}.");

        var rule = request.ToRuleDto();
        if (rule is not null && FrequencyRules.Validate(rule, weekdayRequired: true) is { } problem)
            throw new BusinessRuleException(problem);

        var existing = await _db.WaterPointLabFrequencies
            .FirstOrDefaultAsync(f => f.WaterSamplingPointId == id && f.SectionId == sectionId);

        if (existing is null)
        {
            existing = new WaterPointLabFrequency
            {
                WaterSamplingPointId = id,
                SectionId = sectionId,
            };
            ApplyFrequency(existing, rule);
            _db.WaterPointLabFrequencies.Add(existing);
        }
        else
        {
            RecordVersion.EnsureCurrent(_db, existing);
            ApplyFrequency(existing, rule);
        }

        await _db.SaveChangesAsync();
        existing.Section = section;
        return WaterPointLabFrequencyResponse.From(existing);
    }

    public async Task<object> DeleteWaterPointLabFrequencyAsync(int currentUserId, int id, int sectionId)
    {
        var point = await _db.WaterSamplingPoints.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NotFoundException($"Sampling point {id} not found.");

        var configurable = await _scope.GetConfigurableSectionIdsAsync(currentUserId);
        if (configurable is not null && !configurable.Contains(sectionId))
            throw new UnauthorizedAccessException($"You do not have permission to configure section {sectionId}.");

        var existing = await _db.WaterPointLabFrequencies
            .FirstOrDefaultAsync(f => f.WaterSamplingPointId == id && f.SectionId == sectionId);

        if (existing is not null)
        {
            _db.WaterPointLabFrequencies.Remove(existing);
            await _db.SaveChangesAsync();
        }

        return new { };
    }

    // A weekly Water point must say which weekday it is sampled on.
    private static void ApplyFrequency(IHasFrequency target, FrequencyRuleDto? rule)
    {
        if (rule is not null && FrequencyRules.Validate(rule, weekdayRequired: true) is { } problem)
            throw new BusinessRuleException(problem);
        FrequencyRules.Apply(target, rule);
    }

    public async Task<WaterSamplingPointResponse> CreateWaterSamplingPointAsync(int currentUserId, CreateWaterSamplingPointRequest request)
    {
        if (request.AssignedTestCodes is { Count: > 0 })
        {
            await TestCodeOwnership.EnsureCanEditAsync(_db, _scope, currentUserId, request.AssignedTestCodes, "Water sampling point");
        }

        var point = new WaterSamplingPoint
        {
            Code = request.Code,
            Location = request.Location,
            TestingFrequency = request.TestingFrequency,
            AssignedTestCodes = request.AssignedTestCodes ?? new List<string>(),
            WaterDepartmentId = request.WaterDepartmentId
        };
        ApplyFrequency(point, request.Frequency);
        _db.WaterSamplingPoints.Add(point);
        await _db.SaveChangesAsync();
        return WaterSamplingPointResponse.From(point);
    }

    public async Task<WaterSamplingPointResponse> UpdateWaterSamplingPointAsync(int currentUserId, int id, UpdateWaterSamplingPointRequest request)
    {
        var point = await _db.WaterSamplingPoints
            .Include(p => p.LabFrequencies).ThenInclude(f => f.Section)
            .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NotFoundException($"Sampling point {id} not found.");
        RecordVersion.EnsureCurrent(_db, point);

        var storedCodes = new HashSet<string>(point.AssignedTestCodes ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        var newCodes = new HashSet<string>(request.AssignedTestCodes ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        var changedCodes = newCodes.Except(storedCodes, StringComparer.OrdinalIgnoreCase)
            .Concat(storedCodes.Except(newCodes, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (changedCodes.Count > 0)
        {
            await TestCodeOwnership.EnsureCanEditAsync(_db, _scope, currentUserId, changedCodes, "Water sampling point");
        }

        if (request.IsActive.HasValue) point.IsActive = request.IsActive.Value;
        point.Code = request.Code;
        point.Location = request.Location;
        point.TestingFrequency = request.TestingFrequency;
        point.AssignedTestCodes = request.AssignedTestCodes ?? new List<string>();
        point.WaterDepartmentId = request.WaterDepartmentId;
        ApplyFrequency(point, request.Frequency);
        await _db.SaveChangesAsync();
        return WaterSamplingPointResponse.From(point);
    }

    // Blocked if any Sample, SampleLocation or SamplingConfiguration still references
    // this point - same "guard with a clear message" pattern as
    // ItemService.DeleteAsync.
    public async Task<object> DeleteWaterSamplingPointAsync(int currentUserId, int id)
    {
        var point = await _db.WaterSamplingPoints.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NotFoundException($"Sampling point {id} not found.");

        if (point.AssignedTestCodes is { Count: > 0 })
        {
            var editable = await TestCodeOwnership.GetEditableCodesAsync(_db, _scope, currentUserId);
            if (editable is not null)
            {
                var existingCodes = await _db.TestDefinitions
                    .AsNoTracking()
                    .Where(t => point.AssignedTestCodes.Contains(t.Code))
                    .Select(t => t.Code)
                    .ToListAsync();

                if (existingCodes.Any(c => !editable.Contains(c)))
                {
                    throw new UnauthorizedAccessException(
                        $"Cannot delete '{point.Code}' - it still has tests from another laboratory assigned.");
                }
            }
        }

        var sampleCount = await _db.Samples.CountAsync(s => s.WaterSamplingPointId == id);
        var configCount = await _db.SamplingConfigurations.CountAsync(c => c.WaterSamplingPointId == id);
        var locationCount = await _db.SampleLocations.CountAsync(l => l.WaterSamplingPointId == id);
        if (sampleCount > 0 || locationCount > 0 || configCount > 0)
            throw new InvalidOperationException(
                $"Cannot delete '{point.Code}' - it is referenced by {sampleCount} sample(s), {locationCount} sample location(s) and {configCount} sampling configuration(s).");

        _db.WaterSamplingPoints.Remove(point);
        await _db.SaveChangesAsync();
        return new { };
    }

    public async Task<object> GetWaterDepartmentsAsync()
    {
        // Shaped projection to avoid the WaterDepartment.SamplingPoints <->
        // WaterSamplingPoint.WaterDepartment navigation cycle, same pattern
        // as GetDepartments.
        var departments = await _db.WaterDepartments
            .Select(d => new
            {
                d.Id, d.Version, d.Name,
                SamplingPoints = d.SamplingPoints.Select(p => new
                {
                    p.Id, p.Version, p.Code, p.Location, p.TestingFrequency, p.WaterDepartmentId, p.AssignedTestCodes, p.IsActive, p.FrequencyType, p.FrequencyInterval, p.SamplingWeekday, p.FrequencyAnchorMonth, p.FrequencyWeekOfMonth
                })
            })
            .ToListAsync();
        return departments;
    }

    public async Task<WaterDepartmentResponse> CreateWaterDepartmentAsync(CreateWaterDepartmentRequest request)
    {
        var dept = new WaterDepartment { Name = request.Name };
        _db.WaterDepartments.Add(dept);
        await _db.SaveChangesAsync();
        return WaterDepartmentResponse.From(dept);
    }

    public async Task<WaterDepartmentResponse> UpdateWaterDepartmentAsync(int id, UpdateWaterDepartmentRequest request)
    {
        var dept = await _db.WaterDepartments.FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new NotFoundException($"Water department {id} not found.");
        RecordVersion.EnsureCurrent(_db, dept);
        if (request.IsActive.HasValue) dept.IsActive = request.IsActive.Value;
        dept.Name = request.Name;
        await _db.SaveChangesAsync();
        return WaterDepartmentResponse.From(dept);
    }

    public async Task<object> DeleteWaterDepartmentAsync(int id)
    {
        var dept = await _db.WaterDepartments.FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new NotFoundException($"Water department {id} not found.");

        var pointCount = await _db.WaterSamplingPoints.CountAsync(p => p.WaterDepartmentId == id);
        if (pointCount > 0)
            throw new InvalidOperationException($"Cannot delete '{dept.Name}' - it still has {pointCount} sample location(s). Delete those first.");

        _db.WaterDepartments.Remove(dept);
        await _db.SaveChangesAsync();
        return new { };
    }

    public async Task<List<SamplingConfigurationResponse>> GetWaterSamplingConfigurationsAsync(int pointId) =>
        (await _db.SamplingConfigurations.AsNoTracking().Where(c => c.WaterSamplingPointId == pointId).ToListAsync()).Select(SamplingConfigurationResponse.From).ToList();

    public async Task<SamplingConfigurationResponse> CreateWaterSamplingConfigurationAsync(int currentUserId, CreateWaterSamplingConfigRequest request)
    {
        await TestCodeOwnership.EnsureCanEditAsync(_db, _scope, currentUserId, request.TestCode, "Water sampling configuration");
        LocationChemistryLimitRules.Validate(request.LimitType, request.LowerLimit, request.UpperLimit, request.ExpectedResultText);

        var entity = new SamplingConfiguration
        {
            WaterSamplingPointId = request.WaterSamplingPointId,
            TestCode = request.TestCode,
            AlertLimit = request.AlertLimit ?? string.Empty,
            ActionLimit = request.ActionLimit ?? string.Empty,
            SpecLimit = request.SpecLimit ?? string.Empty,
            Unit = request.Unit ?? string.Empty,
            LimitType = request.LimitType,
            LowerLimit = request.LowerLimit,
            UpperLimit = request.UpperLimit,
            LowerInclusive = request.LowerInclusive,
            UpperInclusive = request.UpperInclusive,
            ExpectedResultText = request.ExpectedResultText
        };
        _db.SamplingConfigurations.Add(entity);
        await _db.SaveChangesAsync();
        return SamplingConfigurationResponse.From(entity);
    }

    public async Task<SamplingConfigurationResponse> UpdateWaterSamplingConfigurationAsync(int currentUserId, int id, UpdateWaterSamplingConfigRequest request)
    {
        var entity = await _db.SamplingConfigurations.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException($"Water sampling configuration {id} not found.");
        RecordVersion.EnsureCurrent(_db, entity);

        await TestCodeOwnership.EnsureCanEditAsync(_db, _scope, currentUserId, new[] { entity.TestCode, request.TestCode }, "Water sampling configuration");
        LocationChemistryLimitRules.Validate(request.LimitType, request.LowerLimit, request.UpperLimit, request.ExpectedResultText);

        entity.TestCode = request.TestCode;
        entity.AlertLimit = request.AlertLimit ?? string.Empty;
        entity.ActionLimit = request.ActionLimit ?? string.Empty;
        entity.SpecLimit = request.SpecLimit ?? string.Empty;
        entity.Unit = request.Unit ?? string.Empty;
        entity.LimitType = request.LimitType;
        entity.LowerLimit = request.LowerLimit;
        entity.UpperLimit = request.UpperLimit;
        entity.LowerInclusive = request.LowerInclusive;
        entity.UpperInclusive = request.UpperInclusive;
        entity.ExpectedResultText = request.ExpectedResultText;
        await _db.SaveChangesAsync();
        return SamplingConfigurationResponse.From(entity);
    }

    public async Task<object> DeleteWaterSamplingConfigurationAsync(int currentUserId, int id)
    {
        var entity = await _db.SamplingConfigurations.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException($"Water sampling configuration {id} not found.");

        await TestCodeOwnership.EnsureCanEditAsync(_db, _scope, currentUserId, entity.TestCode, "Water sampling configuration");

        _db.SamplingConfigurations.Remove(entity);
        await _db.SaveChangesAsync();
        return new { };
    }
}
