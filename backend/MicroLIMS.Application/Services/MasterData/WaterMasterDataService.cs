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

namespace MicroLIMS.Application.Services.MasterData;

// Master data: water. Behind WaterMasterDataController
// (api/masterdata/...); the controller only maps HTTP to these calls.
public class WaterMasterDataService
{
    private readonly IMicroLimsDbContext _db;

    public WaterMasterDataService(IMicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<List<WaterSamplingPointResponse>> GetWaterSamplingPointsAsync() =>
        (await _db.WaterSamplingPoints.AsNoTracking().ToListAsync()).Select(WaterSamplingPointResponse.From).ToList();

    public async Task<WaterSamplingPointResponse> CreateWaterSamplingPointAsync(CreateWaterSamplingPointRequest request)
    {
        var point = new WaterSamplingPoint { Code = request.Code, Location = request.Location, TestingFrequency = request.TestingFrequency, AssignedTestCodes = request.AssignedTestCodes, WaterDepartmentId = request.WaterDepartmentId };
        _db.WaterSamplingPoints.Add(point);
        await _db.SaveChangesAsync();
        return WaterSamplingPointResponse.From(point);
    }

    public async Task<WaterSamplingPointResponse> UpdateWaterSamplingPointAsync(int id, UpdateWaterSamplingPointRequest request)
    {
        var point = await _db.WaterSamplingPoints.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NotFoundException($"Sampling point {id} not found.");
        RecordVersion.EnsureCurrent(_db, point);
        point.Code = request.Code;
        point.Location = request.Location;
        point.TestingFrequency = request.TestingFrequency;
        point.AssignedTestCodes = request.AssignedTestCodes;
        point.WaterDepartmentId = request.WaterDepartmentId;
        await _db.SaveChangesAsync();
        return WaterSamplingPointResponse.From(point);
    }

    // Blocked if any Sample or SamplingConfiguration still references
    // this point - same "guard with a clear message" pattern as
    // ItemService.DeleteAsync.
    public async Task<object> DeleteWaterSamplingPointAsync(int id)
    {
        var point = await _db.WaterSamplingPoints.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NotFoundException($"Sampling point {id} not found.");

        var sampleCount = await _db.Samples.CountAsync(s => s.WaterSamplingPointId == id);
        var configCount = await _db.SamplingConfigurations.CountAsync(c => c.WaterSamplingPointId == id);
        if (sampleCount > 0 || configCount > 0)
            throw new InvalidOperationException(
                $"Cannot delete '{point.Code}' - it is referenced by {sampleCount} sample(s) and {configCount} sampling configuration(s).");

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
                    p.Id, p.Version, p.Code, p.Location, p.TestingFrequency, p.WaterDepartmentId, p.AssignedTestCodes
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

    public async Task<SamplingConfigurationResponse> CreateWaterSamplingConfigurationAsync(CreateWaterSamplingConfigRequest request)
    {
        var entity = new SamplingConfiguration
        {
            WaterSamplingPointId = request.WaterSamplingPointId, TestCode = request.TestCode,
            AlertLimit = request.AlertLimit, ActionLimit = request.ActionLimit, SpecLimit = request.SpecLimit,
            Unit = request.Unit ?? string.Empty
        };
        _db.SamplingConfigurations.Add(entity);
        await _db.SaveChangesAsync();
        return SamplingConfigurationResponse.From(entity);
    }

    public async Task<SamplingConfigurationResponse> UpdateWaterSamplingConfigurationAsync(int id, UpdateWaterSamplingConfigRequest request)
    {
        var entity = await _db.SamplingConfigurations.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException($"Water sampling configuration {id} not found.");
        RecordVersion.EnsureCurrent(_db, entity);
        entity.TestCode = request.TestCode;
        entity.AlertLimit = request.AlertLimit;
        entity.ActionLimit = request.ActionLimit;
        entity.SpecLimit = request.SpecLimit;
        entity.Unit = request.Unit ?? string.Empty;
        await _db.SaveChangesAsync();
        return SamplingConfigurationResponse.From(entity);
    }

    public async Task<object> DeleteWaterSamplingConfigurationAsync(int id)
    {
        var entity = await _db.SamplingConfigurations.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException($"Water sampling configuration {id} not found.");
        _db.SamplingConfigurations.Remove(entity);
        await _db.SaveChangesAsync();
        return new { };
    }
}
