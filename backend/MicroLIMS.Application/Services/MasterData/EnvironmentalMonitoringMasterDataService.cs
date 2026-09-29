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

// Master data: environmental monitoring. Behind EnvironmentalMonitoringMasterDataController
// (api/masterdata/...); the controller only maps HTTP to these calls.
public class EnvironmentalMonitoringMasterDataService
{
    private readonly IMicroLimsDbContext _db;

    public EnvironmentalMonitoringMasterDataService(IMicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<object> GetDepartmentsAsync()
    {
        // Shaped to avoid the Department.Rooms <-> Room.Department
        // navigation cycle EF's relationship fixup creates when both
        // sides are tracked in the same query - same pattern as GetMachines.
        var departments = await _db.Departments
            .Select(d => new { d.Id, d.Name, d.Class, d.TestingFrequency, Rooms = d.Rooms.Select(r => new { r.Id, r.Name, r.DepartmentId, r.GradeClassification }) })
            .ToListAsync();
        return departments;
    }

    public async Task<EmDepartmentResponse> CreateDepartmentAsync(CreateDepartmentRequest request)
    {
        var dept = new Department { Name = request.Name, Class = request.Class, TestingFrequency = request.TestingFrequency };
        _db.Departments.Add(dept);
        await _db.SaveChangesAsync();
        return EmDepartmentResponse.From(dept);
    }

    public async Task<EmDepartmentResponse> UpdateDepartmentAsync(int id, UpdateDepartmentRequest request)
    {
        var dept = await _db.Departments.FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new NotFoundException($"Department {id} not found.");
        dept.Name = request.Name;
        dept.Class = request.Class;
        dept.TestingFrequency = request.TestingFrequency;
        await _db.SaveChangesAsync();
        return EmDepartmentResponse.From(dept);
    }

    // Blocked (not a raw FK error) if this department still has Rooms -
    // same "guard with a clear message" pattern as ItemService.DeleteAsync.
    public async Task<object> DeleteDepartmentAsync(int id)
    {
        var dept = await _db.Departments.FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new NotFoundException($"Department {id} not found.");

        var roomCount = await _db.Rooms.CountAsync(r => r.DepartmentId == id);
        if (roomCount > 0)
            throw new InvalidOperationException($"Cannot delete '{dept.Name}' - it still has {roomCount} room(s). Delete those rooms first.");

        _db.Departments.Remove(dept);
        await _db.SaveChangesAsync();
        return new { };
    }

    public async Task<object> GetRoomsAsync()
    {
        // Shaped to avoid the Department.Rooms <-> Room.Department
        // navigation cycle EF's relationship fixup creates when both
        // sides are tracked in the same query (raw entities would crash
        // JSON serialization here).
        var rooms = await _db.Rooms
            .Select(r => new { r.Id, r.Name, r.DepartmentId, r.GradeClassification, Department = r.Department == null ? null : new { r.Department.Id, r.Department.Name } })
            .ToListAsync();
        return rooms;
    }

    public async Task<RoomResponse> CreateRoomAsync(CreateRoomRequest request)
    {
        var room = new Room { Name = request.Name, DepartmentId = request.DepartmentId, GradeClassification = request.GradeClassification };
        _db.Rooms.Add(room);
        await _db.SaveChangesAsync();
        return RoomResponse.From(room);
    }

    public async Task<RoomResponse> UpdateRoomAsync(int id, UpdateRoomRequest request)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new NotFoundException($"Room {id} not found.");
        room.Name = request.Name;
        room.DepartmentId = request.DepartmentId;
        room.GradeClassification = request.GradeClassification;
        await _db.SaveChangesAsync();
        return RoomResponse.From(room);
    }

    // Blocked if this room still has test configurations or monitoring
    // history - same reasoning as ItemService.DeleteAsync guarding on
    // Samples. Configurations have no downstream dependents of their own
    // (TestOrder.TestCode is a copied string, not an FK) so removing
    // those first is enough to unblock a room delete.
    public async Task<object> DeleteRoomAsync(int id)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new NotFoundException($"Room {id} not found.");

        var configCount = await _db.RoomTestConfigurations.CountAsync(c => c.RoomId == id);
        if (configCount > 0)
            throw new InvalidOperationException($"Cannot delete '{room.Name}' - it still has {configCount} test configuration(s). Delete those first.");

        var monitoringCount = await _db.RoomMonitorings.CountAsync(m => m.RoomId == id);
        if (monitoringCount > 0)
            throw new InvalidOperationException($"Cannot delete '{room.Name}' - it has {monitoringCount} monitoring record(s) in its history.");

        _db.Rooms.Remove(room);
        await _db.SaveChangesAsync();
        return new { };
    }

    public async Task<List<RoomTestConfigurationResponse>> GetRoomTestConfigurationsAsync(int roomId) =>
        (await _db.RoomTestConfigurations.AsNoTracking().Where(c => c.RoomId == roomId).ToListAsync()).Select(RoomTestConfigurationResponse.From).ToList();

    public async Task<RoomTestConfigurationResponse> CreateRoomTestConfigurationAsync(CreateRoomTestConfigRequest request)
    {
        var entity = new RoomTestConfiguration
        {
            RoomId = request.RoomId, TestType = request.TestType, TestCode = request.TestCode,
            AlertLimit = request.AlertLimit, ActionLimit = request.ActionLimit, SpecLimit = request.SpecLimit,
            Unit = request.Unit ?? string.Empty
        };
        _db.RoomTestConfigurations.Add(entity);
        await _db.SaveChangesAsync();
        return RoomTestConfigurationResponse.From(entity);
    }

    public async Task<RoomTestConfigurationResponse> UpdateRoomTestConfigurationAsync(int id, UpdateRoomTestConfigRequest request)
    {
        var entity = await _db.RoomTestConfigurations.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException($"Room test configuration {id} not found.");
        entity.TestType = request.TestType;
        entity.TestCode = request.TestCode;
        entity.AlertLimit = request.AlertLimit;
        entity.ActionLimit = request.ActionLimit;
        entity.SpecLimit = request.SpecLimit;
        entity.Unit = request.Unit ?? string.Empty;
        await _db.SaveChangesAsync();
        return RoomTestConfigurationResponse.From(entity);
    }

    // No downstream dependents (TestOrder.TestCode is a copied string,
    // not an FK to this row) - always safe to hard-delete.
    public async Task<object> DeleteRoomTestConfigurationAsync(int id)
    {
        var entity = await _db.RoomTestConfigurations.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException($"Room test configuration {id} not found.");
        _db.RoomTestConfigurations.Remove(entity);
        await _db.SaveChangesAsync();
        return new { };
    }
}
