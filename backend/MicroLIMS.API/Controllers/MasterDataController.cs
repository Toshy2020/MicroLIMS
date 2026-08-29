using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record CreateWaterSamplingPointRequest(string Code, string Location, string TestingFrequency, List<string> AssignedTestCodes, int? WaterDepartmentId);
public record UpdateWaterSamplingPointRequest(string Code, string Location, string TestingFrequency, List<string> AssignedTestCodes, int? WaterDepartmentId);
public record CreateWaterDepartmentRequest(string Name);
public record UpdateWaterDepartmentRequest(string Name);
public record CreateWaterSamplingConfigRequest(int WaterSamplingPointId, string TestCode, string AlertLimit, string ActionLimit, string SpecLimit, string? Unit = null);
public record UpdateWaterSamplingConfigRequest(string TestCode, string AlertLimit, string ActionLimit, string SpecLimit, string? Unit = null);
public record CreateDepartmentRequest(string Name, string Class, string TestingFrequency);
public record CreateRoomRequest(string Name, int DepartmentId, string GradeClassification);
public record UpdateDepartmentRequest(string Name, string Class, string TestingFrequency);
public record UpdateRoomRequest(string Name, int DepartmentId, string GradeClassification);
public record UpdateRoomTestConfigRequest(string TestType, string TestCode, string AlertLimit, string ActionLimit, string SpecLimit, string? Unit = null);
public record CreateMachineRequest(string Name);
public record UpdateMachineRequest(string Name);
public record CreateMachinePartRequest(string Name, int MachineId);
public record UpdateMachinePartRequest(string Name, int MachineId);
public record UpdateMachinePartConfigRequest(string TestType, string TestCode, string AlertLimit, string ActionLimit, string SpecLimit, bool IsPathogenTest, string? Unit = null);
public record CreateSpecificationRequest(int ItemId, string TestCode, string AlertLimit, string ActionLimit, string SpecLimit, string? Unit = null);
public record UpdateSpecificationRequest(string TestCode, string AlertLimit, string ActionLimit, string SpecLimit, string? Unit = null);
public record CreateDiluentTypeRequest(string Name, bool RequiresBatchTracking, int? MaterialId);
public record CreateEquipmentRequest(string Name, string Code, EquipmentType Type, string? Location, decimal? SetPointTemperature, DateTime? CalibrationDueDate);
public record CreateRoomTestConfigRequest(int RoomId, string TestType, string TestCode, string AlertLimit, string ActionLimit, string SpecLimit, string? Unit = null);
public record CreateMachinePartConfigRequest(int MachinePartId, string TestType, string TestCode, string AlertLimit, string ActionLimit, string SpecLimit, bool IsPathogenTest, string? Unit = null);
public record CreateMediaConfigurationChallengeRequest(int OrganismId, ChallengeRole? ChallengeRole, string? ExpectedDescription, string? InitialInoculum);
public record CreateMediaConfigurationRequest(string Name, EvaluationType EvaluationType, int IncubationMinHours, int IncubationMaxHours, decimal TemperatureMin, decimal TemperatureMax, decimal? RecoveryPercentMin, decimal? RecoveryPercentMax, List<CreateMediaConfigurationChallengeRequest>? Challenges);
public record UpdateMediaConfigurationRequest(string Name, EvaluationType EvaluationType, int IncubationMinHours, int IncubationMaxHours, decimal TemperatureMin, decimal TemperatureMax, decimal? RecoveryPercentMin, decimal? RecoveryPercentMax, List<CreateMediaConfigurationChallengeRequest>? Challenges);
public record CreateOrganismRequest(string ScientificName, string? AtccNumber, string? CommonName, string? Description);
public record UpdateOrganismRequest(string ScientificName, string? AtccNumber, string? CommonName, string? Description);
public record CreateTestDefinitionRequest(string Code, string DisplayName);
public record UpdateTestDefinitionRequest(string Code, string DisplayName);
public record UpdateWorkflowTypeRequest(WorkflowType WorkflowType);
public record StepMediaRequest(int MaterialId, decimal TempMin, decimal TempMax, bool IsRequired, int DisplayOrder, int? MediaConfigurationId, int IncubationMinHours, int IncubationMaxHours);
public record IncubationStageRequest(int StageNumber, decimal TempMin, decimal TempMax, int IncubationMinHours, int IncubationMaxHours);
// PhenotypicTestType (single) is kept alongside the new PhenotypicTestTypes
// list for backward compatibility - an older client sending only the single
// field still works. When the list is supplied, it drives the step's
// bundled phenotypic tests; the single field is still stored too (whatever
// the client sends) since some existing chained-step templates rely on it.
public record CreateTestWorkflowStepRequest(string StepName, int IncubationMinHours, int IncubationMaxHours, decimal TemperatureMin, decimal TemperatureMax, bool IsFinalStep, StepType StepType, int? TargetOrganismId, List<StepMediaRequest> StepMedia, bool RequiresIncubationTransfer, List<IncubationStageRequest>? IncubationStages, int? ConfirmatoryMediaCount, PhenotypicTestType? PhenotypicTestType, List<PhenotypicTestType>? PhenotypicTestTypes = null);
public record UpdateTestWorkflowStepRequest(string StepName, int IncubationMinHours, int IncubationMaxHours, decimal TemperatureMin, decimal TemperatureMax, bool IsFinalStep, StepType StepType, int? TargetOrganismId, List<StepMediaRequest> StepMedia, bool RequiresIncubationTransfer, List<IncubationStageRequest>? IncubationStages, int? ConfirmatoryMediaCount, PhenotypicTestType? PhenotypicTestType, List<PhenotypicTestType>? PhenotypicTestTypes = null);
public record MoveTestWorkflowStepRequest(string Direction);

// Backs the Items Master's category-dependent dynamic forms: Product ->
// Specification, Water -> Sampling Points, EM -> Rooms, After Cleaning
// -> Machine Parts (gap analysis - "Dynamic Forms").
public record SetAutoclaveProgramStatusHttpRequest(bool IsActive, string? Comment);

[ApiController]
[Route("api/masterdata")]
[Authorize]
public class MasterDataController : ControllerBase
{
    private readonly MicroLimsDbContext _db;
    private readonly EquipmentConfigurationService _configService;

    public MasterDataController(MicroLimsDbContext db, EquipmentConfigurationService configService)
    {
        _db = db;
        _configService = configService;
    }

    private int CurrentUserId => int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;

    // ---- Water Sampling Points ----
    [HttpGet("water-sampling-points")]
    public async Task<IActionResult> GetWaterSamplingPoints() =>
        Ok(ApiResponse<object>.Ok(await _db.WaterSamplingPoints.ToListAsync()));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("water-sampling-points")]
    public async Task<IActionResult> CreateWaterSamplingPoint(CreateWaterSamplingPointRequest request)
    {
        var point = new WaterSamplingPoint { Code = request.Code, Location = request.Location, TestingFrequency = request.TestingFrequency, AssignedTestCodes = request.AssignedTestCodes, WaterDepartmentId = request.WaterDepartmentId };
        _db.WaterSamplingPoints.Add(point);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(point));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("water-sampling-points/{id}")]
    public async Task<IActionResult> UpdateWaterSamplingPoint(int id, UpdateWaterSamplingPointRequest request)
    {
        var point = await _db.WaterSamplingPoints.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new InvalidOperationException($"Sampling point {id} not found.");
        point.Code = request.Code;
        point.Location = request.Location;
        point.TestingFrequency = request.TestingFrequency;
        point.AssignedTestCodes = request.AssignedTestCodes;
        point.WaterDepartmentId = request.WaterDepartmentId;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(point));
    }

    // Blocked if any Sample or SamplingConfiguration still references
    // this point - same "guard with a clear message" pattern as
    // ItemService.DeleteAsync.
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("water-sampling-points/{id}")]
    public async Task<IActionResult> DeleteWaterSamplingPoint(int id)
    {
        var point = await _db.WaterSamplingPoints.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new InvalidOperationException($"Sampling point {id} not found.");

        var sampleCount = await _db.Samples.CountAsync(s => s.WaterSamplingPointId == id);
        var configCount = await _db.SamplingConfigurations.CountAsync(c => c.WaterSamplingPointId == id);
        if (sampleCount > 0 || configCount > 0)
            throw new InvalidOperationException(
                $"Cannot delete '{point.Code}' - it is referenced by {sampleCount} sample(s) and {configCount} sampling configuration(s).");

        _db.WaterSamplingPoints.Remove(point);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    // ---- Water Departments ----
    [HttpGet("water-departments")]
    public async Task<IActionResult> GetWaterDepartments()
    {
        // Shaped projection to avoid the WaterDepartment.SamplingPoints <->
        // WaterSamplingPoint.WaterDepartment navigation cycle, same pattern
        // as GetDepartments.
        var departments = await _db.WaterDepartments
            .Select(d => new
            {
                d.Id, d.Name,
                SamplingPoints = d.SamplingPoints.Select(p => new
                {
                    p.Id, p.Code, p.Location, p.TestingFrequency, p.WaterDepartmentId, p.AssignedTestCodes
                })
            })
            .ToListAsync();
        return Ok(ApiResponse<object>.Ok(departments));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("water-departments")]
    public async Task<IActionResult> CreateWaterDepartment(CreateWaterDepartmentRequest request)
    {
        var dept = new WaterDepartment { Name = request.Name };
        _db.WaterDepartments.Add(dept);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(dept));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("water-departments/{id}")]
    public async Task<IActionResult> UpdateWaterDepartment(int id, UpdateWaterDepartmentRequest request)
    {
        var dept = await _db.WaterDepartments.FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new InvalidOperationException($"Water department {id} not found.");
        dept.Name = request.Name;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(dept));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("water-departments/{id}")]
    public async Task<IActionResult> DeleteWaterDepartment(int id)
    {
        var dept = await _db.WaterDepartments.FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new InvalidOperationException($"Water department {id} not found.");

        var pointCount = await _db.WaterSamplingPoints.CountAsync(p => p.WaterDepartmentId == id);
        if (pointCount > 0)
            throw new InvalidOperationException($"Cannot delete '{dept.Name}' - it still has {pointCount} sample location(s). Delete those first.");

        _db.WaterDepartments.Remove(dept);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    // ---- Water Sampling Configurations (per sample location x test limits) ----
    [HttpGet("water-sampling-configurations")]
    public async Task<IActionResult> GetWaterSamplingConfigurations([FromQuery] int pointId) =>
        Ok(ApiResponse<object>.Ok(await _db.SamplingConfigurations.Where(c => c.WaterSamplingPointId == pointId).ToListAsync()));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("water-sampling-configurations")]
    public async Task<IActionResult> CreateWaterSamplingConfiguration(CreateWaterSamplingConfigRequest request)
    {
        var entity = new SamplingConfiguration
        {
            WaterSamplingPointId = request.WaterSamplingPointId, TestCode = request.TestCode,
            AlertLimit = request.AlertLimit, ActionLimit = request.ActionLimit, SpecLimit = request.SpecLimit,
            Unit = request.Unit ?? string.Empty
        };
        _db.SamplingConfigurations.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("water-sampling-configurations/{id}")]
    public async Task<IActionResult> UpdateWaterSamplingConfiguration(int id, UpdateWaterSamplingConfigRequest request)
    {
        var entity = await _db.SamplingConfigurations.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new InvalidOperationException($"Water sampling configuration {id} not found.");
        entity.TestCode = request.TestCode;
        entity.AlertLimit = request.AlertLimit;
        entity.ActionLimit = request.ActionLimit;
        entity.SpecLimit = request.SpecLimit;
        entity.Unit = request.Unit ?? string.Empty;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("water-sampling-configurations/{id}")]
    public async Task<IActionResult> DeleteWaterSamplingConfiguration(int id)
    {
        var entity = await _db.SamplingConfigurations.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new InvalidOperationException($"Water sampling configuration {id} not found.");
        _db.SamplingConfigurations.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    // ---- Departments & Rooms (EM) ----
    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments()
    {
        // Shaped to avoid the Department.Rooms <-> Room.Department
        // navigation cycle EF's relationship fixup creates when both
        // sides are tracked in the same query - same pattern as GetMachines.
        var departments = await _db.Departments.Include(d => d.Rooms)
            .Select(d => new { d.Id, d.Name, d.Class, d.TestingFrequency, Rooms = d.Rooms.Select(r => new { r.Id, r.Name, r.DepartmentId, r.GradeClassification }) })
            .ToListAsync();
        return Ok(ApiResponse<object>.Ok(departments));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("departments")]
    public async Task<IActionResult> CreateDepartment(CreateDepartmentRequest request)
    {
        var dept = new Department { Name = request.Name, Class = request.Class, TestingFrequency = request.TestingFrequency };
        _db.Departments.Add(dept);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(dept));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("departments/{id}")]
    public async Task<IActionResult> UpdateDepartment(int id, UpdateDepartmentRequest request)
    {
        var dept = await _db.Departments.FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new InvalidOperationException($"Department {id} not found.");
        dept.Name = request.Name;
        dept.Class = request.Class;
        dept.TestingFrequency = request.TestingFrequency;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(dept));
    }

    // Blocked (not a raw FK error) if this department still has Rooms -
    // same "guard with a clear message" pattern as ItemService.DeleteAsync.
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("departments/{id}")]
    public async Task<IActionResult> DeleteDepartment(int id)
    {
        var dept = await _db.Departments.FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new InvalidOperationException($"Department {id} not found.");

        var roomCount = await _db.Rooms.CountAsync(r => r.DepartmentId == id);
        if (roomCount > 0)
            throw new InvalidOperationException($"Cannot delete '{dept.Name}' - it still has {roomCount} room(s). Delete those rooms first.");

        _db.Departments.Remove(dept);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpGet("rooms")]
    public async Task<IActionResult> GetRooms()
    {
        // Shaped to avoid the Department.Rooms <-> Room.Department
        // navigation cycle EF's relationship fixup creates when both
        // sides are tracked in the same query (raw entities would crash
        // JSON serialization here).
        var rooms = await _db.Rooms.Include(r => r.Department)
            .Select(r => new { r.Id, r.Name, r.DepartmentId, r.GradeClassification, Department = r.Department == null ? null : new { r.Department.Id, r.Department.Name } })
            .ToListAsync();
        return Ok(ApiResponse<object>.Ok(rooms));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("rooms")]
    public async Task<IActionResult> CreateRoom(CreateRoomRequest request)
    {
        var room = new Room { Name = request.Name, DepartmentId = request.DepartmentId, GradeClassification = request.GradeClassification };
        _db.Rooms.Add(room);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(room));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("rooms/{id}")]
    public async Task<IActionResult> UpdateRoom(int id, UpdateRoomRequest request)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new InvalidOperationException($"Room {id} not found.");
        room.Name = request.Name;
        room.DepartmentId = request.DepartmentId;
        room.GradeClassification = request.GradeClassification;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(room));
    }

    // Blocked if this room still has test configurations or monitoring
    // history - same reasoning as ItemService.DeleteAsync guarding on
    // Samples. Configurations have no downstream dependents of their own
    // (TestOrder.TestCode is a copied string, not an FK) so removing
    // those first is enough to unblock a room delete.
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("rooms/{id}")]
    public async Task<IActionResult> DeleteRoom(int id)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new InvalidOperationException($"Room {id} not found.");

        var configCount = await _db.RoomTestConfigurations.CountAsync(c => c.RoomId == id);
        if (configCount > 0)
            throw new InvalidOperationException($"Cannot delete '{room.Name}' - it still has {configCount} test configuration(s). Delete those first.");

        var monitoringCount = await _db.RoomMonitorings.CountAsync(m => m.RoomId == id);
        if (monitoringCount > 0)
            throw new InvalidOperationException($"Cannot delete '{room.Name}' - it has {monitoringCount} monitoring record(s) in its history.");

        _db.Rooms.Remove(room);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    // ---- Machines & Parts (After Cleaning) ----
    [HttpGet("machines")]
    public async Task<IActionResult> GetMachines()
    {
        // Shaped to avoid the Machine.Parts <-> MachinePart.Machine
        // navigation cycle EF's relationship fixup creates when both
        // sides are tracked in the same query.
        var machines = await _db.Machines.Include(m => m.Parts)
            .Select(m => new { m.Id, m.Name, Parts = m.Parts.Select(p => new { p.Id, p.Name, p.MachineId }) })
            .ToListAsync();
        return Ok(ApiResponse<object>.Ok(machines));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("machines")]
    public async Task<IActionResult> CreateMachine(CreateMachineRequest request)
    {
        var machine = new Machine { Name = request.Name };
        _db.Machines.Add(machine);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(machine));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("machines/{id}")]
    public async Task<IActionResult> UpdateMachine(int id, UpdateMachineRequest request)
    {
        var machine = await _db.Machines.FirstOrDefaultAsync(m => m.Id == id)
            ?? throw new InvalidOperationException($"Machine {id} not found.");
        machine.Name = request.Name;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(machine));
    }

    // Blocked if this machine still has parts, or any Sample has ever
    // referenced it directly (Sample.MachineId) - same "guard with a
    // clear message" pattern as ItemService.DeleteAsync.
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("machines/{id}")]
    public async Task<IActionResult> DeleteMachine(int id)
    {
        var machine = await _db.Machines.FirstOrDefaultAsync(m => m.Id == id)
            ?? throw new InvalidOperationException($"Machine {id} not found.");

        var partCount = await _db.MachineParts.CountAsync(p => p.MachineId == id);
        if (partCount > 0)
            throw new InvalidOperationException($"Cannot delete '{machine.Name}' - it still has {partCount} part(s). Delete those first.");

        var sampleCount = await _db.Samples.CountAsync(s => s.MachineId == id);
        if (sampleCount > 0)
            throw new InvalidOperationException($"Cannot delete '{machine.Name}' - it has been used to receive {sampleCount} sample(s).");

        _db.Machines.Remove(machine);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("machine-parts")]
    public async Task<IActionResult> CreateMachinePart(CreateMachinePartRequest request)
    {
        var part = new MachinePart { Name = request.Name, MachineId = request.MachineId };
        _db.MachineParts.Add(part);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(part));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("machine-parts/{id}")]
    public async Task<IActionResult> UpdateMachinePart(int id, UpdateMachinePartRequest request)
    {
        var part = await _db.MachineParts.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new InvalidOperationException($"Machine part {id} not found.");
        part.Name = request.Name;
        part.MachineId = request.MachineId;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(part));
    }

    // Blocked if this part still has test configurations - configurations
    // have no downstream dependents of their own (TestOrder.TestCode is a
    // copied string, not an FK), so removing those first is enough.
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("machine-parts/{id}")]
    public async Task<IActionResult> DeleteMachinePart(int id)
    {
        var part = await _db.MachineParts.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new InvalidOperationException($"Machine part {id} not found.");

        var configCount = await _db.MachinePartConfigurations.CountAsync(c => c.MachinePartId == id);
        if (configCount > 0)
            throw new InvalidOperationException($"Cannot delete '{part.Name}' - it still has {configCount} test configuration(s). Delete those first.");

        _db.MachineParts.Remove(part);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    // ---- Specifications (Product) ----
    [HttpGet("specifications")]
    public async Task<IActionResult> GetSpecifications([FromQuery] int itemId) =>
        Ok(ApiResponse<object>.Ok(await _db.Specifications.Where(s => s.ItemId == itemId).ToListAsync()));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("specifications")]
    public async Task<IActionResult> CreateSpecification(CreateSpecificationRequest request)
    {
        var spec = new Specification
        {
            ItemId = request.ItemId, TestCode = request.TestCode,
            AlertLimit = request.AlertLimit, ActionLimit = request.ActionLimit, SpecLimit = request.SpecLimit,
            Unit = request.Unit ?? string.Empty
        };
        _db.Specifications.Add(spec);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(spec));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("specifications/{id}")]
    public async Task<IActionResult> UpdateSpecification(int id, UpdateSpecificationRequest request)
    {
        var spec = await _db.Specifications.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new InvalidOperationException($"Specification {id} not found.");
        spec.TestCode = request.TestCode;
        spec.AlertLimit = request.AlertLimit;
        spec.ActionLimit = request.ActionLimit;
        spec.SpecLimit = request.SpecLimit;
        spec.Unit = request.Unit ?? string.Empty;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(spec));
    }

    // No downstream dependents to guard - Results/CountTestReadings copy
    // the Alert/Action/Spec values as plain strings at calculation time
    // rather than referencing the Specification row itself.
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("specifications/{id}")]
    public async Task<IActionResult> DeleteSpecification(int id)
    {
        var spec = await _db.Specifications.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new InvalidOperationException($"Specification {id} not found.");
        _db.Specifications.Remove(spec);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    // ---- Cause of Testing ----
    [HttpGet("causes-of-testing")]
    public async Task<IActionResult> GetCausesOfTesting() => Ok(ApiResponse<object>.Ok(await _db.CausesOfTesting.Where(c => c.IsActive).ToListAsync()));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("causes-of-testing")]
    public async Task<IActionResult> CreateCauseOfTesting([FromBody] string name)
    {
        var entity = new CauseOfTesting { Name = name };
        _db.CausesOfTesting.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    // ---- Diluent Types ----
    [HttpGet("diluent-types")]
    public async Task<IActionResult> GetDiluentTypes() => Ok(ApiResponse<object>.Ok(await _db.DiluentTypes.ToListAsync()));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("diluent-types")]
    public async Task<IActionResult> CreateDiluentType(CreateDiluentTypeRequest request)
    {
        var entity = new DiluentType { Name = request.Name, RequiresBatchTracking = request.RequiresBatchTracking, MaterialId = request.MaterialId };
        _db.DiluentTypes.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    // ---- Neutralizers ----
    [HttpGet("neutralizers")]
    public async Task<IActionResult> GetNeutralizers() => Ok(ApiResponse<object>.Ok(await _db.Neutralizers.Where(n => n.IsActive).ToListAsync()));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("neutralizers")]
    public async Task<IActionResult> CreateNeutralizer([FromBody] string name)
    {
        var entity = new Neutralizer { Name = name };
        _db.Neutralizers.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    // ---- Equipment ----
    [HttpGet("equipment")]
    public async Task<IActionResult> GetEquipment([FromQuery] EquipmentType? type)
    {
        var query = _db.Equipment.AsQueryable();
        if (type.HasValue) query = query.Where(e => e.Type == type.Value);
        return Ok(ApiResponse<object>.Ok(await query.ToListAsync()));
    }

    [HttpGet("equipment/configured-summary")]
    public async Task<IActionResult> GetConfiguredSummary() =>
        Ok(ApiResponse<object>.Ok(await _configService.GetConfiguredEquipmentSummaryAsync()));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("equipment/link-inventory/{inventoryId:int}")]
    public async Task<IActionResult> LinkInventory(int inventoryId)
    {
        var master = await _configService.LinkInventoryEquipmentToMasterAsync(inventoryId, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(master));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("equipment/{id:int}/set-point")]
    public async Task<IActionResult> UpdateSetPoint(int id, [FromBody] UpdateIncubatorSetPointRequest request)
    {
        try
        {
            var updated = await _configService.UpdateIncubatorSetPointAsync(id, request, CurrentUserId);
            return Ok(ApiResponse<object>.Ok(updated));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("equipment/{id:int}/set-point-history")]
    public async Task<IActionResult> GetSetPointHistory(int id) =>
        Ok(ApiResponse<object>.Ok(await _configService.GetIncubatorSetPointHistoryAsync(id)));

    [HttpGet("equipment/{id:int}/autoclave-programs")]
    public async Task<IActionResult> GetAutoclavePrograms(int id, [FromQuery] bool? activeOnly) =>
        Ok(ApiResponse<object>.Ok(await _configService.GetAutoclaveProgramsAsync(id, activeOnly)));

    [HttpGet("equipment/autoclave-programs/all")]
    public async Task<IActionResult> GetAllAutoclavePrograms([FromQuery] bool? activeOnly) =>
        Ok(ApiResponse<object>.Ok(await _configService.GetAutoclaveProgramsAsync(null, activeOnly)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("equipment/{id:int}/autoclave-programs")]
    public async Task<IActionResult> SaveAutoclaveProgram(int id, [FromBody] SaveAutoclaveProgramRequest request)
    {
        try
        {
            var req = request with { EquipmentId = id };
            var saved = await _configService.SaveAutoclaveProgramAsync(req, CurrentUserId);
            return Ok(ApiResponse<object>.Ok(saved));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("equipment/autoclave-programs/{programId:int}")]
    public async Task<IActionResult> UpdateAutoclaveProgram(int programId, [FromBody] SaveAutoclaveProgramRequest request)
    {
        try
        {
            var req = request with { Id = programId };
            var saved = await _configService.SaveAutoclaveProgramAsync(req, CurrentUserId);
            return Ok(ApiResponse<object>.Ok(saved));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("equipment/autoclave-programs/{programId:int}/status")]
    public async Task<IActionResult> SetAutoclaveProgramStatus(int programId, [FromBody] SetAutoclaveProgramStatusHttpRequest request)
    {
        try
        {
            await _configService.SetAutoclaveProgramStatusAsync(programId, request.IsActive, request.Comment ?? "", CurrentUserId);
            return Ok(ApiResponse<object>.Ok(new { }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("equipment/autoclave-programs/{programId:int}/history")]
    public async Task<IActionResult> GetAutoclaveProgramHistory(int programId) =>
        Ok(ApiResponse<object>.Ok(await _configService.GetAutoclaveProgramHistoryAsync(programId)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("equipment")]
    public async Task<IActionResult> CreateEquipment(CreateEquipmentRequest request)
    {
        var entity = new Equipment
        {
            Name = request.Name, Code = request.Code, Type = request.Type, Location = request.Location,
            SetPointTemperature = request.SetPointTemperature, CalibrationDueDate = request.CalibrationDueDate
        };
        _db.Equipment.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    // ---- Room Test Configurations (EM) ----
    [HttpGet("room-test-configurations")]
    public async Task<IActionResult> GetRoomTestConfigurations([FromQuery] int roomId) =>
        Ok(ApiResponse<object>.Ok(await _db.RoomTestConfigurations.Where(c => c.RoomId == roomId).ToListAsync()));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("room-test-configurations")]
    public async Task<IActionResult> CreateRoomTestConfiguration(CreateRoomTestConfigRequest request)
    {
        var entity = new RoomTestConfiguration
        {
            RoomId = request.RoomId, TestType = request.TestType, TestCode = request.TestCode,
            AlertLimit = request.AlertLimit, ActionLimit = request.ActionLimit, SpecLimit = request.SpecLimit,
            Unit = request.Unit ?? string.Empty
        };
        _db.RoomTestConfigurations.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("room-test-configurations/{id}")]
    public async Task<IActionResult> UpdateRoomTestConfiguration(int id, UpdateRoomTestConfigRequest request)
    {
        var entity = await _db.RoomTestConfigurations.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new InvalidOperationException($"Room test configuration {id} not found.");
        entity.TestType = request.TestType;
        entity.TestCode = request.TestCode;
        entity.AlertLimit = request.AlertLimit;
        entity.ActionLimit = request.ActionLimit;
        entity.SpecLimit = request.SpecLimit;
        entity.Unit = request.Unit ?? string.Empty;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    // No downstream dependents (TestOrder.TestCode is a copied string,
    // not an FK to this row) - always safe to hard-delete.
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("room-test-configurations/{id}")]
    public async Task<IActionResult> DeleteRoomTestConfiguration(int id)
    {
        var entity = await _db.RoomTestConfigurations.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new InvalidOperationException($"Room test configuration {id} not found.");
        _db.RoomTestConfigurations.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    // ---- Machine Part Test Configurations (After Cleaning) ----
    [HttpGet("machine-part-configurations")]
    public async Task<IActionResult> GetMachinePartConfigurations([FromQuery] int machinePartId) =>
        Ok(ApiResponse<object>.Ok(await _db.MachinePartConfigurations.Where(c => c.MachinePartId == machinePartId).ToListAsync()));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("machine-part-configurations")]
    public async Task<IActionResult> CreateMachinePartConfiguration(CreateMachinePartConfigRequest request)
    {
        var entity = new MachinePartConfiguration
        {
            MachinePartId = request.MachinePartId, TestType = request.TestType, TestCode = request.TestCode,
            AlertLimit = request.AlertLimit, ActionLimit = request.ActionLimit, SpecLimit = request.SpecLimit,
            IsPathogenTest = request.IsPathogenTest,
            Unit = request.Unit ?? string.Empty
        };
        _db.MachinePartConfigurations.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("machine-part-configurations/{id}")]
    public async Task<IActionResult> UpdateMachinePartConfiguration(int id, UpdateMachinePartConfigRequest request)
    {
        var entity = await _db.MachinePartConfigurations.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new InvalidOperationException($"Machine part configuration {id} not found.");
        entity.TestType = request.TestType;
        entity.TestCode = request.TestCode;
        entity.AlertLimit = request.AlertLimit;
        entity.ActionLimit = request.ActionLimit;
        entity.SpecLimit = request.SpecLimit;
        entity.IsPathogenTest = request.IsPathogenTest;
        entity.Unit = request.Unit ?? string.Empty;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    // No downstream dependents (TestOrder.TestCode is a copied string,
    // not an FK to this row) - always safe to hard-delete.
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("machine-part-configurations/{id}")]
    public async Task<IActionResult> DeleteMachinePartConfiguration(int id)
    {
        var entity = await _db.MachinePartConfigurations.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new InvalidOperationException($"Machine part configuration {id} not found.");
        _db.MachinePartConfigurations.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    // ---- Media Configurations (Media Configuration Migration) ----
    [HttpGet("media-configurations")]
    public async Task<IActionResult> GetMediaConfigurations()
    {
        var configs = await _db.MediaConfigurations
            .Include(m => m.Challenges)
            .ThenInclude(c => c.Organism)
            .OrderBy(m => m.Name)
            .ThenBy(m => m.IncubationMinHours)
            .Select(m => new
            {
                m.Id,
                m.Name,
                m.EvaluationType,
                m.IncubationMinHours,
                m.IncubationMaxHours,
                m.TemperatureMin,
                m.TemperatureMax,
                m.RecoveryPercentMin,
                m.RecoveryPercentMax,
                Challenges = m.Challenges.Select(c => new
                {
                    c.Id,
                    c.MediaConfigurationId,
                    c.OrganismId,
                    c.ChallengeRole,
                    c.ExpectedDescription,
                    c.InitialInoculum,
                    Organism = c.Organism == null ? null : new
                    {
                        c.Organism.Id,
                        c.Organism.ScientificName,
                        c.Organism.AtccNumber,
                        c.Organism.CommonName
                    }
                }).ToList()
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(configs));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("media-configurations")]
    public async Task<IActionResult> CreateMediaConfiguration(CreateMediaConfigurationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Media configuration name is required.");

        if (!Enum.IsDefined(typeof(EvaluationType), request.EvaluationType))
            throw new InvalidOperationException("Valid evaluation type is required.");

        if (request.IncubationMinHours < 0)
            throw new InvalidOperationException("Incubation min hours cannot be negative.");

        if (request.IncubationMinHours > request.IncubationMaxHours)
            throw new InvalidOperationException("Incubation min hours cannot exceed incubation max hours.");

        if (request.TemperatureMin > request.TemperatureMax)
            throw new InvalidOperationException("Temperature min cannot exceed temperature max.");

        if (request.EvaluationType == EvaluationType.GrowthPromotion)
        {
            if (request.RecoveryPercentMin.HasValue && request.RecoveryPercentMax.HasValue &&
                request.RecoveryPercentMin > request.RecoveryPercentMax)
            {
                throw new InvalidOperationException("Recovery percent min cannot exceed recovery percent max.");
            }
        }

        var exists = await _db.MediaConfigurations.AnyAsync(m =>
            m.Name == request.Name.Trim() &&
            m.IncubationMinHours == request.IncubationMinHours &&
            m.IncubationMaxHours == request.IncubationMaxHours &&
            m.TemperatureMin == request.TemperatureMin &&
            m.TemperatureMax == request.TemperatureMax);

        if (exists)
        {
            throw new InvalidOperationException(
                $"A media configuration for '{request.Name.Trim()}' with incubation {request.IncubationMinHours}–{request.IncubationMaxHours}h and temperature {request.TemperatureMin}–{request.TemperatureMax}°C already exists.");
        }

        var entity = new MediaConfiguration
        {
            Name = request.Name.Trim(),
            EvaluationType = request.EvaluationType,
            IncubationMinHours = request.IncubationMinHours,
            IncubationMaxHours = request.IncubationMaxHours,
            TemperatureMin = request.TemperatureMin,
            TemperatureMax = request.TemperatureMax,
            RecoveryPercentMin = request.EvaluationType == EvaluationType.GrowthPromotion ? request.RecoveryPercentMin : null,
            RecoveryPercentMax = request.EvaluationType == EvaluationType.GrowthPromotion ? request.RecoveryPercentMax : null
        };

        if (request.Challenges != null && request.Challenges.Count > 0)
        {
            foreach (var ch in request.Challenges)
            {
                var organismExists = await _db.Organisms.AnyAsync(o => o.Id == ch.OrganismId);
                if (!organismExists)
                    throw new InvalidOperationException($"Organism with ID {ch.OrganismId} not found.");

                if (request.EvaluationType == EvaluationType.IndicationInhibition)
                {
                    if (!ch.ChallengeRole.HasValue || !Enum.IsDefined(typeof(ChallengeRole), ch.ChallengeRole.Value))
                        throw new InvalidOperationException("Challenge role is required for Indication/Inhibition evaluation.");
                }

                entity.Challenges.Add(new MediaConfigurationChallenge
                {
                    OrganismId = ch.OrganismId,
                    ChallengeRole = request.EvaluationType == EvaluationType.IndicationInhibition ? ch.ChallengeRole : null,
                    ExpectedDescription = (request.EvaluationType == EvaluationType.IndicationInhibition && ch.ChallengeRole == ChallengeRole.Indication)
                        ? ch.ExpectedDescription
                        : null,
                    InitialInoculum = string.IsNullOrWhiteSpace(ch.InitialInoculum) ? null : ch.InitialInoculum.Trim()
                });
            }
        }

        _db.MediaConfigurations.Add(entity);
        await _db.SaveChangesAsync();

        var created = new
        {
            entity.Id,
            entity.Name,
            entity.EvaluationType,
            entity.IncubationMinHours,
            entity.IncubationMaxHours,
            entity.TemperatureMin,
            entity.TemperatureMax,
            entity.RecoveryPercentMin,
            entity.RecoveryPercentMax,
            Challenges = entity.Challenges.Select(c => new
            {
                c.Id,
                c.MediaConfigurationId,
                c.OrganismId,
                c.ChallengeRole,
                c.ExpectedDescription,
                c.InitialInoculum
            }).ToList()
        };

        return Ok(ApiResponse<object>.Ok(created));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("media-configurations/{id}")]
    public async Task<IActionResult> UpdateMediaConfiguration(int id, UpdateMediaConfigurationRequest request)
    {
        var entity = await _db.MediaConfigurations
            .Include(m => m.Challenges)
            .FirstOrDefaultAsync(m => m.Id == id)
            ?? throw new InvalidOperationException($"Media configuration {id} not found.");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Media configuration name is required.");

        if (!Enum.IsDefined(typeof(EvaluationType), request.EvaluationType))
            throw new InvalidOperationException("Valid evaluation type is required.");

        if (request.IncubationMinHours < 0)
            throw new InvalidOperationException("Incubation min hours cannot be negative.");

        if (request.IncubationMinHours > request.IncubationMaxHours)
            throw new InvalidOperationException("Incubation min hours cannot exceed incubation max hours.");

        if (request.TemperatureMin > request.TemperatureMax)
            throw new InvalidOperationException("Temperature min cannot exceed temperature max.");

        if (request.EvaluationType == EvaluationType.GrowthPromotion)
        {
            if (request.RecoveryPercentMin.HasValue && request.RecoveryPercentMax.HasValue &&
                request.RecoveryPercentMin > request.RecoveryPercentMax)
            {
                throw new InvalidOperationException("Recovery percent min cannot exceed recovery percent max.");
            }
        }

        var exists = await _db.MediaConfigurations.AnyAsync(m =>
            m.Id != id &&
            m.Name == request.Name.Trim() &&
            m.IncubationMinHours == request.IncubationMinHours &&
            m.IncubationMaxHours == request.IncubationMaxHours &&
            m.TemperatureMin == request.TemperatureMin &&
            m.TemperatureMax == request.TemperatureMax);

        if (exists)
        {
            throw new InvalidOperationException(
                $"A media configuration for '{request.Name.Trim()}' with incubation {request.IncubationMinHours}–{request.IncubationMaxHours}h and temperature {request.TemperatureMin}–{request.TemperatureMax}°C already exists.");
        }

        entity.Name = request.Name.Trim();
        entity.EvaluationType = request.EvaluationType;
        entity.IncubationMinHours = request.IncubationMinHours;
        entity.IncubationMaxHours = request.IncubationMaxHours;
        entity.TemperatureMin = request.TemperatureMin;
        entity.TemperatureMax = request.TemperatureMax;
        entity.RecoveryPercentMin = request.EvaluationType == EvaluationType.GrowthPromotion ? request.RecoveryPercentMin : null;
        entity.RecoveryPercentMax = request.EvaluationType == EvaluationType.GrowthPromotion ? request.RecoveryPercentMax : null;

        var incomingChallenges = request.Challenges ?? new List<CreateMediaConfigurationChallengeRequest>();

        foreach (var ch in incomingChallenges)
        {
            var organismExists = await _db.Organisms.AnyAsync(o => o.Id == ch.OrganismId);
            if (!organismExists)
                throw new InvalidOperationException($"Organism with ID {ch.OrganismId} not found.");

            if (request.EvaluationType == EvaluationType.IndicationInhibition)
            {
                if (!ch.ChallengeRole.HasValue || !Enum.IsDefined(typeof(ChallengeRole), ch.ChallengeRole.Value))
                    throw new InvalidOperationException("Challenge role is required for Indication/Inhibition evaluation.");
            }
        }

        var duplicateIncoming = incomingChallenges
            .GroupBy(c => new { c.OrganismId, Role = request.EvaluationType == EvaluationType.IndicationInhibition ? c.ChallengeRole : null })
            .Any(g => g.Count() > 1);
        if (duplicateIncoming)
            throw new InvalidOperationException("Duplicate challenge organism and role combination in request.");

        var toRemove = entity.Challenges.Where(existing =>
            !incomingChallenges.Any(inc =>
                inc.OrganismId == existing.OrganismId &&
                (request.EvaluationType != EvaluationType.IndicationInhibition || inc.ChallengeRole == existing.ChallengeRole)
            )).ToList();

        foreach (var rem in toRemove)
        {
            entity.Challenges.Remove(rem);
        }

        foreach (var inc in incomingChallenges)
        {
            var role = request.EvaluationType == EvaluationType.IndicationInhibition ? inc.ChallengeRole : null;
            var desc = (request.EvaluationType == EvaluationType.IndicationInhibition && inc.ChallengeRole == ChallengeRole.Indication)
                ? inc.ExpectedDescription
                : null;
            var inoculum = string.IsNullOrWhiteSpace(inc.InitialInoculum) ? null : inc.InitialInoculum.Trim();

            var existing = entity.Challenges.FirstOrDefault(e =>
                e.OrganismId == inc.OrganismId &&
                (request.EvaluationType != EvaluationType.IndicationInhibition || e.ChallengeRole == inc.ChallengeRole));

            if (existing != null)
            {
                existing.ChallengeRole = role;
                existing.ExpectedDescription = desc;
                existing.InitialInoculum = inoculum;
            }
            else
            {
                entity.Challenges.Add(new MediaConfigurationChallenge
                {
                    OrganismId = inc.OrganismId,
                    ChallengeRole = role,
                    ExpectedDescription = desc,
                    InitialInoculum = inoculum
                });
            }
        }

        await _db.SaveChangesAsync();

        var updated = new
        {
            entity.Id,
            entity.Name,
            entity.EvaluationType,
            entity.IncubationMinHours,
            entity.IncubationMaxHours,
            entity.TemperatureMin,
            entity.TemperatureMax,
            entity.RecoveryPercentMin,
            entity.RecoveryPercentMax,
            Challenges = entity.Challenges.Select(c => new
            {
                c.Id,
                c.MediaConfigurationId,
                c.OrganismId,
                c.ChallengeRole,
                c.ExpectedDescription,
                c.InitialInoculum
            }).ToList()
        };

        return Ok(ApiResponse<object>.Ok(updated));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("media-configurations/{id}")]
    public async Task<IActionResult> DeleteMediaConfiguration(int id)
    {
        var entity = await _db.MediaConfigurations.FirstOrDefaultAsync(m => m.Id == id)
            ?? throw new InvalidOperationException($"Media configuration {id} not found.");

        var referencingSteps = await _db.TestWorkflowStepMedias
            .Where(sm => sm.MediaConfigurationId == id)
            .Select(sm => sm.TestWorkflowStep!.StepName)
            .Distinct()
            .ToListAsync();

        if (referencingSteps.Count > 0)
        {
            throw new InvalidOperationException(
                $"Cannot delete '{entity.Name}' - it is used by {referencingSteps.Count} workflow step(s): {string.Join(", ", referencingSteps)}.");
        }

        _db.MediaConfigurations.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    // ---- Organisms (canonical master list backing every OrganismId FK -
    // MediaChallengeSpec, MediaEvaluationChallenge, Cryovial, Material) ----
    [HttpGet("organisms")]
    public async Task<IActionResult> GetOrganisms() =>
        Ok(ApiResponse<object>.Ok(await _db.Organisms.OrderBy(o => o.ScientificName).ToListAsync()));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("organisms")]
    public async Task<IActionResult> CreateOrganism(CreateOrganismRequest request)
    {
        if (await _db.Organisms.AnyAsync(o => o.ScientificName.ToLower() == request.ScientificName.ToLower()))
            throw new InvalidOperationException($"Organism \"{request.ScientificName}\" already exists in the Organism list.");

        var entity = new Organism { ScientificName = request.ScientificName, AtccNumber = request.AtccNumber, CommonName = request.CommonName, Description = request.Description };
        _db.Organisms.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("organisms/{id}")]
    public async Task<IActionResult> UpdateOrganism(int id, UpdateOrganismRequest request)
    {
        var entity = await _db.Organisms.FirstOrDefaultAsync(o => o.Id == id)
            ?? throw new InvalidOperationException($"Organism {id} not found.");

        if (await _db.Organisms.AnyAsync(o => o.Id != id && o.ScientificName.ToLower() == request.ScientificName.ToLower()))
            throw new InvalidOperationException($"Organism \"{request.ScientificName}\" already exists in the Organism list.");

        entity.ScientificName = request.ScientificName;
        entity.AtccNumber = request.AtccNumber;
        entity.CommonName = request.CommonName;
        entity.Description = request.Description;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    // Blocked (not a raw FK error) if any MediaConfigurationChallenge,
    // MediaEvaluationChallenge, Cryovial, or Material still references
    // this organism - all four are Restrict FKs (see OrganismConfiguration
    // and friends), same "guard with a clear message" pattern as
    // ItemService.DeleteAsync.
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("organisms/{id}")]
    public async Task<IActionResult> DeleteOrganism(int id)
    {
        var entity = await _db.Organisms.FirstOrDefaultAsync(o => o.Id == id)
            ?? throw new InvalidOperationException($"Organism {id} not found.");

        var configChallengeCount = await _db.MediaConfigurationChallenges.CountAsync(c => c.OrganismId == id);
        var challengeCount = await _db.MediaEvaluationChallenges.CountAsync(c => c.OrganismId == id);
        var cryovialCount = await _db.Cryovials.CountAsync(c => c.OrganismId == id);
        var materialCount = await _db.Materials.CountAsync(m => m.OrganismId == id);
        var totalUses = configChallengeCount + challengeCount + cryovialCount + materialCount;

        if (totalUses > 0)
            throw new InvalidOperationException(
                $"Cannot delete '{entity.ScientificName}' - it is referenced by {totalUses} record(s) " +
                $"(Media Configuration Challenges: {configChallengeCount}, Media Evaluation Challenges: {challengeCount}, Cryovials: {cryovialCount}, Materials: {materialCount}).");

        _db.Organisms.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    // ---- Test Master ----
    // The canonical Code/DisplayName list backing every TestCode picker
    // in the app (Items, Water Sampling Points, Room Test Configurations,
    // Machine Part Configurations). See TestDefinition.cs for why this
    // exists.
    [HttpGet("test-definitions")]
    public async Task<IActionResult> GetTestDefinitions() =>
        Ok(ApiResponse<object>.Ok(await _db.TestDefinitions.OrderBy(t => t.Code).ToListAsync()));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("test-definitions")]
    public async Task<IActionResult> CreateTestDefinition(CreateTestDefinitionRequest request)
    {
        if (await _db.TestDefinitions.AnyAsync(t => t.Code == request.Code))
            throw new InvalidOperationException($"Test code \"{request.Code}\" already exists in the Test Master.");

        var entity = new TestDefinition { Code = request.Code, DisplayName = request.DisplayName };
        _db.TestDefinitions.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("test-definitions/{id}")]
    public async Task<IActionResult> UpdateTestDefinition(int id, UpdateTestDefinitionRequest request)
    {
        var entity = await _db.TestDefinitions.FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new InvalidOperationException($"Test {id} not found.");

        if (await _db.TestDefinitions.AnyAsync(t => t.Code == request.Code && t.Id != id))
            throw new InvalidOperationException($"Test code \"{request.Code}\" already exists in the Test Master.");

        entity.Code = request.Code;
        entity.DisplayName = request.DisplayName;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("test-definitions/{id}/freeze")]
    public async Task<IActionResult> FreezeTestDefinition(int id)
    {
        var entity = await _db.TestDefinitions.FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new InvalidOperationException($"Test {id} not found.");
        entity.IsActive = false;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("test-definitions/{id}/unfreeze")]
    public async Task<IActionResult> UnfreezeTestDefinition(int id)
    {
        var entity = await _db.TestDefinitions.FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new InvalidOperationException($"Test {id} not found.");
        entity.IsActive = true;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    // ---- Test Workflow Steps (TestWorkflowEngine's configurable
    // template - replaces what used to be hardcoded chains in
    // PathogenWorkflowEngine/CountTestWorkflowEngine) ----
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("test-definitions/{id}/workflow-type")]
    public async Task<IActionResult> UpdateWorkflowType(int id, UpdateWorkflowTypeRequest request)
    {
        var entity = await _db.TestDefinitions.FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new InvalidOperationException($"Test {id} not found.");
        entity.WorkflowType = request.WorkflowType;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    [HttpGet("test-definitions/{id}/steps")]
    public async Task<IActionResult> GetTestWorkflowSteps(int id) =>
        Ok(ApiResponse<object>.Ok(await _db.TestWorkflowSteps
            .Include(s => s.TargetOrganism)
            .Include(s => s.StepMedia).ThenInclude(m => m.Material)
            .Include(s => s.StepMedia).ThenInclude(m => m.MediaConfiguration)
            .Include(s => s.IncubationStages)
            .Include(s => s.PhenotypicTests)
            .Where(s => s.TestDefinitionId == id)
            .OrderBy(s => s.StepOrder)
            .Select(s => new
            {
                s.Id, s.StepOrder, s.StepName, s.PhenotypicTestType,
                phenotypicTestTypes = s.PhenotypicTests.OrderBy(t => t.DisplayOrder).Select(t => t.PhenotypicTestType),
                s.IncubationMinHours, s.IncubationMaxHours, s.TemperatureMin, s.TemperatureMax,
                s.IsFinalStep,
                stepType = s.StepType.ToString(),
                s.TargetOrganismId,
                targetOrganism = s.TargetOrganism == null ? null : new { s.TargetOrganism.Id, name = s.TargetOrganism.ScientificName },
                s.ConfirmatoryMediaCount,
                stepMedia = s.StepMedia.OrderBy(m => m.DisplayOrder).Select(m => new
                {
                    stepMediaId = m.Id, m.MaterialId, materialName = m.Material!.MaterialName,
                    // The operative incubation window/temperature for this
                    // medium at execution time (see TestWorkflowEngine.cs) -
                    // not the step's own IncubationMinHours/MaxHours/
                    // TemperatureMin/Max below, which are no longer read.
                    m.TempMin, m.TempMax, m.IncubationMinHours, m.IncubationMaxHours,
                    m.IsRequired, m.DisplayOrder,
                    m.MediaConfigurationId,
                    mediaConfigurationName = m.MediaConfiguration == null ? null : m.MediaConfiguration.Name
                }),
                s.RequiresIncubationTransfer,
                incubationStages = s.IncubationStages.OrderBy(x => x.StageNumber).Select(x => new
                {
                    x.StageNumber, x.TempMin, x.TempMax, x.IncubationMinHours, x.IncubationMaxHours
                })
            })
            .ToListAsync()));

    // TempMin/Max are server-derived from the picked MediaConfiguration,
    // not trusted from the client, whenever one is supplied - single
    // source of truth, no drift between what's shown and what's stored.
    // MediaConfigurationId stays optional: MediaConfiguration has no
    // MaterialId FK of its own (out of scope for this pass - see the
    // Media Configuration Migration plan §7), so MaterialId remains a
    // separate, independently-required field on every row regardless.
    private async Task<List<TestWorkflowStepMedia>> BuildStepMediaAsync(List<StepMediaRequest> requests)
    {
        var configIds = requests.Where(m => m.MediaConfigurationId.HasValue).Select(m => m.MediaConfigurationId!.Value).Distinct().ToList();
        var configs = await _db.MediaConfigurations.Where(c => configIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id);

        var result = new List<TestWorkflowStepMedia>();
        foreach (var m in requests)
        {
            var entity = new TestWorkflowStepMedia { MaterialId = m.MaterialId, IsRequired = m.IsRequired, DisplayOrder = m.DisplayOrder };
            if (m.MediaConfigurationId.HasValue)
            {
                if (!configs.TryGetValue(m.MediaConfigurationId.Value, out var config))
                    throw new InvalidOperationException($"Media configuration {m.MediaConfigurationId.Value} not found.");
                entity.MediaConfigurationId = config.Id;
                entity.TempMin = config.TemperatureMin;
                entity.TempMax = config.TemperatureMax;
                entity.IncubationMinHours = config.IncubationMinHours;
                entity.IncubationMaxHours = config.IncubationMaxHours;
            }
            else
            {
                entity.TempMin = m.TempMin;
                entity.TempMax = m.TempMax;
                entity.IncubationMinHours = m.IncubationMinHours;
                entity.IncubationMaxHours = m.IncubationMaxHours;
            }
            result.Add(entity);
        }
        return result;
    }

    // Structural rules come from WorkflowTemplateValidator; this adds the
    // one rule that spans the whole template rather than a single step.
    private async Task ValidateStepRulesAsync(int testDefinitionId, int? excludeStepId, TestWorkflowStep candidate)
    {
        var errors = WorkflowTemplateValidator.Validate(candidate);
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ",
                errors.Select(e => $"Rule {e.RuleNumber} ({e.StepName}): {e.Message}")));

        if (candidate.IsFinalStep)
        {
            var otherFinalExists = await _db.TestWorkflowSteps
                .AnyAsync(s => s.TestDefinitionId == testDefinitionId && s.IsFinalStep && s.Id != (excludeStepId ?? -1));
            if (otherFinalExists)
                throw new InvalidOperationException("Only one step per test can be marked as the final step.");
        }
    }

    // Post-condition guard for the "no gaps, no duplicates" invariant -
    // Create always appends and Move always swaps two adjacent orders,
    // both of which preserve contiguity by construction, but this makes
    // that invariant an enforced, checked fact rather than an assumption.
    private async Task ValidateContiguousStepOrderAsync(int testDefinitionId)
    {
        var orders = await _db.TestWorkflowSteps.Where(s => s.TestDefinitionId == testDefinitionId)
            .OrderBy(s => s.StepOrder).Select(s => s.StepOrder).ToListAsync();
        for (var i = 0; i < orders.Count; i++)
        {
            if (orders[i] != i + 1)
                throw new InvalidOperationException("Workflow steps must have contiguous step numbers starting from 1.");
        }
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("test-definitions/{id}/steps")]
    public async Task<IActionResult> CreateTestWorkflowStep(int id, CreateTestWorkflowStepRequest request)
    {
        _ = await _db.TestDefinitions.FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new InvalidOperationException($"Test {id} not found.");

        var nextOrder = 1 + await _db.TestWorkflowSteps.Where(s => s.TestDefinitionId == id)
            .Select(s => (int?)s.StepOrder).MaxAsync() ?? 1;

        var stepMedias = await BuildStepMediaAsync(request.StepMedia);
        var firstMedia = stepMedias.OrderBy(m => m.DisplayOrder).FirstOrDefault();
        var tempMin = request.TemperatureMin > 0 ? request.TemperatureMin : (firstMedia?.TempMin ?? 0);
        var tempMax = request.TemperatureMax > 0 ? request.TemperatureMax : (firstMedia?.TempMax ?? 0);
        var incMin = request.IncubationMinHours > 0 ? request.IncubationMinHours : (firstMedia?.IncubationMinHours ?? 0);
        var incMax = request.IncubationMaxHours > 0 ? request.IncubationMaxHours : (firstMedia?.IncubationMaxHours ?? 0);

        var entity = new TestWorkflowStep
        {
            TestDefinitionId = id, StepOrder = nextOrder, StepName = request.StepName,
            IncubationMinHours = incMin, IncubationMaxHours = incMax,
            TemperatureMin = tempMin, TemperatureMax = tempMax,
            IsFinalStep = request.IsFinalStep, StepType = request.StepType, TargetOrganismId = request.TargetOrganismId,
            RequiresIncubationTransfer = request.RequiresIncubationTransfer,
            ConfirmatoryMediaCount = request.ConfirmatoryMediaCount ?? 1,
            PhenotypicTestType = request.PhenotypicTestType
        };
        entity.StepMedia.AddRange(stepMedias);
        entity.IncubationStages.AddRange((request.IncubationStages ?? new()).Select(s => new TestWorkflowStepIncubationStage
        {
            StageNumber = s.StageNumber, TempMin = s.TempMin, TempMax = s.TempMax,
            IncubationMinHours = s.IncubationMinHours, IncubationMaxHours = s.IncubationMaxHours
        }));
        entity.PhenotypicTests.AddRange((request.PhenotypicTestTypes ?? new()).Select((t, i) => new TestWorkflowStepPhenotypicTest
        {
            PhenotypicTestType = t, DisplayOrder = i
        }));

        await ValidateStepRulesAsync(id, excludeStepId: null, entity);

        _db.TestWorkflowSteps.Add(entity);
        await _db.SaveChangesAsync();
        await ValidateContiguousStepOrderAsync(id);
        return Ok(ApiResponse<object>.Ok(new { entity.Id, entity.StepOrder, entity.StepName }));
    }

    // Editing is always allowed, even for a step already used by a real
    // TestOrder - unlike delete, it can't corrupt anything, since a
    // completed Incubation row already has its own copied-at-the-time
    // Temperature/Duration and isn't affected retroactively.
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("test-definitions/steps/{stepId}")]
    public async Task<IActionResult> UpdateTestWorkflowStep(int stepId, UpdateTestWorkflowStepRequest request)
    {
        var step = await _db.TestWorkflowSteps.Include(s => s.StepMedia).Include(s => s.IncubationStages).Include(s => s.PhenotypicTests)
            .FirstOrDefaultAsync(s => s.Id == stepId)
            ?? throw new InvalidOperationException($"Workflow step {stepId} not found.");

        var rebuiltStepMedia = await BuildStepMediaAsync(request.StepMedia);
        var firstMedia = rebuiltStepMedia.OrderBy(m => m.DisplayOrder).FirstOrDefault();
        var tempMin = request.TemperatureMin > 0 ? request.TemperatureMin : (firstMedia?.TempMin ?? 0);
        var tempMax = request.TemperatureMax > 0 ? request.TemperatureMax : (firstMedia?.TempMax ?? 0);
        var incMin = request.IncubationMinHours > 0 ? request.IncubationMinHours : (firstMedia?.IncubationMinHours ?? 0);
        var incMax = request.IncubationMaxHours > 0 ? request.IncubationMaxHours : (firstMedia?.IncubationMaxHours ?? 0);

        step.StepName = request.StepName;
        step.IncubationMinHours = incMin;
        step.IncubationMaxHours = incMax;
        step.TemperatureMin = tempMin;
        step.TemperatureMax = tempMax;
        step.IsFinalStep = request.IsFinalStep;
        step.StepType = request.StepType;
        step.TargetOrganismId = request.TargetOrganismId;
        step.RequiresIncubationTransfer = request.RequiresIncubationTransfer;
        step.ConfirmatoryMediaCount = request.ConfirmatoryMediaCount ?? 1;
        step.PhenotypicTestType = request.PhenotypicTestType;

        // StepMedia is replaced wholesale on update - the analyst edits the
        // panel as a set, and the unique index makes incremental merging
        // error-prone for no benefit.
        _db.TestWorkflowStepMedias.RemoveRange(step.StepMedia);
        step.StepMedia.Clear();
        foreach (var m in rebuiltStepMedia) m.TestWorkflowStepId = step.Id;
        step.StepMedia.AddRange(rebuiltStepMedia);

        _db.TestWorkflowStepIncubationStages.RemoveRange(step.IncubationStages);
        step.IncubationStages.Clear();
        step.IncubationStages.AddRange((request.IncubationStages ?? new()).Select(s => new TestWorkflowStepIncubationStage
        {
            TestWorkflowStepId = step.Id, StageNumber = s.StageNumber, TempMin = s.TempMin, TempMax = s.TempMax,
            IncubationMinHours = s.IncubationMinHours, IncubationMaxHours = s.IncubationMaxHours
        }));

        // Replaced wholesale, same reasoning as StepMedia above - only when
        // the client actually sends the new list field. A null
        // PhenotypicTestTypes (an older client still using only the single
        // field) leaves the existing bundled list untouched rather than
        // wiping it on every edit.
        if (request.PhenotypicTestTypes is not null)
        {
            _db.TestWorkflowStepPhenotypicTests.RemoveRange(step.PhenotypicTests);
            step.PhenotypicTests.Clear();
            step.PhenotypicTests.AddRange(request.PhenotypicTestTypes.Select((t, i) => new TestWorkflowStepPhenotypicTest
            {
                TestWorkflowStepId = step.Id, PhenotypicTestType = t, DisplayOrder = i
            }));
        }

        await ValidateStepRulesAsync(step.TestDefinitionId, excludeStepId: stepId, step);
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { step.Id, step.StepOrder, step.StepName }));
    }

    // Swaps StepOrder with the adjacent step - simpler and safer than
    // accepting an arbitrary new position from the client (no risk of
    // gaps or duplicate StepOrder values from a bad request). Staged
    // through a temporary out-of-range value across two SaveChanges
    // calls - a single-batch swap of two rows sharing the unique
    // (TestDefinitionId, StepOrder) index makes EF's change tracker
    // throw "circular dependency detected" since it can't find a safe
    // statement order for a direct swap against that index.
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("test-definitions/steps/{stepId}/move")]
    public async Task<IActionResult> MoveTestWorkflowStep(int stepId, MoveTestWorkflowStepRequest request)
    {
        var step = await _db.TestWorkflowSteps.FirstOrDefaultAsync(s => s.Id == stepId)
            ?? throw new InvalidOperationException($"Workflow step {stepId} not found.");

        var neighborOrder = request.Direction == "up" ? step.StepOrder - 1 : step.StepOrder + 1;
        var neighbor = await _db.TestWorkflowSteps.FirstOrDefaultAsync(s => s.TestDefinitionId == step.TestDefinitionId && s.StepOrder == neighborOrder);
        if (neighbor is null)
            throw new InvalidOperationException("This step is already at that end of the sequence.");

        var stepOrder = step.StepOrder;
        var neighborStepOrder = neighbor.StepOrder;

        step.StepOrder = -1; // StepOrder is always >= 1, so this never collides
        await _db.SaveChangesAsync();

        neighbor.StepOrder = stepOrder;
        step.StepOrder = neighborStepOrder;
        await _db.SaveChangesAsync();
        await ValidateContiguousStepOrderAsync(step.TestDefinitionId);

        return Ok(ApiResponse<object>.Ok(new { }));
    }

    // Blocked if any real TestOrder for this test code has already used
    // this step (an Incubation row referencing it exists) - same
    // "guard with a clear message" pattern as ItemService.DeleteAsync.
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("test-definitions/steps/{stepId}")]
    public async Task<IActionResult> DeleteTestWorkflowStep(int stepId)
    {
        var step = await _db.TestWorkflowSteps.Include(s => s.TestDefinition).FirstOrDefaultAsync(s => s.Id == stepId)
            ?? throw new InvalidOperationException($"Workflow step {stepId} not found.");

        var inUse = await _db.Incubations.Include(i => i.TestOrder)
            .AnyAsync(i => i.StepName == step.StepName && i.TestOrder!.TestCode == step.TestDefinition!.Code);
        if (inUse)
            throw new InvalidOperationException($"Cannot delete step \"{step.StepName}\" - it has already been used by a test order.");

        _db.TestWorkflowSteps.Remove(step);

        // Close the gap left behind so "contiguous from 1" (Gap 4) stays
        // true afterward instead of only being checked at create/move time.
        var laterSteps = await _db.TestWorkflowSteps
            .Where(s => s.TestDefinitionId == step.TestDefinitionId && s.StepOrder > step.StepOrder)
            .OrderBy(s => s.StepOrder)
            .ToListAsync();
        foreach (var laterStep in laterSteps)
            laterStep.StepOrder -= 1;

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { }));
    }
}
