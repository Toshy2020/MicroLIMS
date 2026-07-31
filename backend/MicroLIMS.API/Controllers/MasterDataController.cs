using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record CreateWaterSamplingPointRequest(string Code, string Location, List<string> AssignedTestCodes);
public record CreateDepartmentRequest(string Name, string Class, string TestingFrequency);
public record CreateRoomRequest(string Name, int DepartmentId, string GradeClassification);
public record CreateMachineRequest(string Name);
public record CreateMachinePartRequest(string Name, int MachineId);
public record CreateSpecificationRequest(int ItemId, string TestCode, string AlertLimit, string ActionLimit, string SpecLimit);
public record CreateDiluentTypeRequest(string Name, bool RequiresBatchTracking, int? MediaTypeId);
public record CreateEquipmentRequest(string Name, string Code, EquipmentType Type, string? Location, decimal? SetPointTemperature, DateTime? CalibrationDueDate);
public record CreateMediaTypeRequest(string Name, string Code, MediaClass Class, int IncubationMinHours, int IncubationMaxHours, decimal RequiredTemperatureMin, decimal RequiredTemperatureMax, List<string> ApprovedTestCodes, decimal? RecoveryPercentMin, decimal? RecoveryPercentMax);
public record CreateRoomTestConfigRequest(int RoomId, string TestType, string TestCode, string AlertLimit, string ActionLimit, string SpecLimit);
public record CreateMachinePartConfigRequest(int MachinePartId, string TestType, string TestCode, string AlertLimit, string ActionLimit, string SpecLimit, bool IsPathogenTest);
public record CreateExpectedIndicationRequest(int MediaTypeId, string OrganismName, string ExpectedDescription);

// Backs the Items Master's category-dependent dynamic forms: Product ->
// Specification, Water -> Sampling Points, EM -> Rooms, After Cleaning
// -> Machine Parts (gap analysis - "Dynamic Forms").
[ApiController]
[Route("api/masterdata")]
[Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
public class MasterDataController : ControllerBase
{
    private readonly MicroLimsDbContext _db;

    public MasterDataController(MicroLimsDbContext db)
    {
        _db = db;
    }

    // ---- Water Sampling Points ----
    [HttpGet("water-sampling-points")]
    public async Task<IActionResult> GetWaterSamplingPoints() =>
        Ok(ApiResponse<object>.Ok(await _db.WaterSamplingPoints.ToListAsync()));

    [HttpPost("water-sampling-points")]
    public async Task<IActionResult> CreateWaterSamplingPoint(CreateWaterSamplingPointRequest request)
    {
        var point = new WaterSamplingPoint { Code = request.Code, Location = request.Location, AssignedTestCodes = request.AssignedTestCodes };
        _db.WaterSamplingPoints.Add(point);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(point));
    }

    // ---- Departments & Rooms (EM) ----
    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments() => Ok(ApiResponse<object>.Ok(await _db.Departments.ToListAsync()));

    [HttpPost("departments")]
    public async Task<IActionResult> CreateDepartment(CreateDepartmentRequest request)
    {
        var dept = new Department { Name = request.Name, Class = request.Class, TestingFrequency = request.TestingFrequency };
        _db.Departments.Add(dept);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(dept));
    }

    [HttpGet("rooms")]
    public async Task<IActionResult> GetRooms() => Ok(ApiResponse<object>.Ok(await _db.Rooms.Include(r => r.Department).ToListAsync()));

    [HttpPost("rooms")]
    public async Task<IActionResult> CreateRoom(CreateRoomRequest request)
    {
        var room = new Room { Name = request.Name, DepartmentId = request.DepartmentId, GradeClassification = request.GradeClassification };
        _db.Rooms.Add(room);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(room));
    }

    // ---- Machines & Parts (After Cleaning) ----
    [HttpGet("machines")]
    public async Task<IActionResult> GetMachines() => Ok(ApiResponse<object>.Ok(await _db.Machines.Include(m => m.Parts).ToListAsync()));

    [HttpPost("machines")]
    public async Task<IActionResult> CreateMachine(CreateMachineRequest request)
    {
        var machine = new Machine { Name = request.Name };
        _db.Machines.Add(machine);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(machine));
    }

    [HttpPost("machine-parts")]
    public async Task<IActionResult> CreateMachinePart(CreateMachinePartRequest request)
    {
        var part = new MachinePart { Name = request.Name, MachineId = request.MachineId };
        _db.MachineParts.Add(part);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(part));
    }

    // ---- Specifications (Product) ----
    [HttpGet("specifications")]
    public async Task<IActionResult> GetSpecifications([FromQuery] int itemId) =>
        Ok(ApiResponse<object>.Ok(await _db.Specifications.Where(s => s.ItemId == itemId).ToListAsync()));

    [HttpPost("specifications")]
    public async Task<IActionResult> CreateSpecification(CreateSpecificationRequest request)
    {
        var spec = new Specification
        {
            ItemId = request.ItemId, TestCode = request.TestCode,
            AlertLimit = request.AlertLimit, ActionLimit = request.ActionLimit, SpecLimit = request.SpecLimit
        };
        _db.Specifications.Add(spec);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(spec));
    }

    // ---- Cause of Testing ----
    [HttpGet("causes-of-testing")]
    public async Task<IActionResult> GetCausesOfTesting() => Ok(ApiResponse<object>.Ok(await _db.CausesOfTesting.Where(c => c.IsActive).ToListAsync()));

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

    [HttpPost("diluent-types")]
    public async Task<IActionResult> CreateDiluentType(CreateDiluentTypeRequest request)
    {
        var entity = new DiluentType { Name = request.Name, RequiresBatchTracking = request.RequiresBatchTracking, MediaTypeId = request.MediaTypeId };
        _db.DiluentTypes.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    // ---- Neutralizers ----
    [HttpGet("neutralizers")]
    public async Task<IActionResult> GetNeutralizers() => Ok(ApiResponse<object>.Ok(await _db.Neutralizers.Where(n => n.IsActive).ToListAsync()));

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

    // ---- Media Types ----
    [HttpGet("media-types")]
    public async Task<IActionResult> GetMediaTypes() => Ok(ApiResponse<object>.Ok(await _db.MediaTypes.ToListAsync()));

    [HttpPost("media-types")]
    public async Task<IActionResult> CreateMediaType(CreateMediaTypeRequest request)
    {
        var entity = new MediaType
        {
            Name = request.Name, Code = request.Code, Class = request.Class,
            IncubationMinHours = request.IncubationMinHours, IncubationMaxHours = request.IncubationMaxHours,
            RequiredTemperatureMin = request.RequiredTemperatureMin, RequiredTemperatureMax = request.RequiredTemperatureMax,
            ApprovedTestCodes = request.ApprovedTestCodes,
            RecoveryPercentMin = request.RecoveryPercentMin, RecoveryPercentMax = request.RecoveryPercentMax
        };
        _db.MediaTypes.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    // ---- Room Test Configurations (EM) ----
    [HttpGet("room-test-configurations")]
    public async Task<IActionResult> GetRoomTestConfigurations([FromQuery] int roomId) =>
        Ok(ApiResponse<object>.Ok(await _db.RoomTestConfigurations.Where(c => c.RoomId == roomId).ToListAsync()));

    [HttpPost("room-test-configurations")]
    public async Task<IActionResult> CreateRoomTestConfiguration(CreateRoomTestConfigRequest request)
    {
        var entity = new RoomTestConfiguration
        {
            RoomId = request.RoomId, TestType = request.TestType, TestCode = request.TestCode,
            AlertLimit = request.AlertLimit, ActionLimit = request.ActionLimit, SpecLimit = request.SpecLimit
        };
        _db.RoomTestConfigurations.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    // ---- Machine Part Test Configurations (After Cleaning) ----
    [HttpGet("machine-part-configurations")]
    public async Task<IActionResult> GetMachinePartConfigurations([FromQuery] int machinePartId) =>
        Ok(ApiResponse<object>.Ok(await _db.MachinePartConfigurations.Where(c => c.MachinePartId == machinePartId).ToListAsync()));

    [HttpPost("machine-part-configurations")]
    public async Task<IActionResult> CreateMachinePartConfiguration(CreateMachinePartConfigRequest request)
    {
        var entity = new MachinePartConfiguration
        {
            MachinePartId = request.MachinePartId, TestType = request.TestType, TestCode = request.TestCode,
            AlertLimit = request.AlertLimit, ActionLimit = request.ActionLimit, SpecLimit = request.SpecLimit,
            IsPathogenTest = request.IsPathogenTest
        };
        _db.MachinePartConfigurations.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    // ---- Expected Indication Results (Selective Media GPT) ----
    [HttpGet("expected-indication-results")]
    public async Task<IActionResult> GetExpectedIndicationResults([FromQuery] int mediaTypeId) =>
        Ok(ApiResponse<object>.Ok(await _db.ExpectedIndicationResults.Where(e => e.MediaTypeId == mediaTypeId).ToListAsync()));

    [HttpPost("expected-indication-results")]
    public async Task<IActionResult> CreateExpectedIndicationResult(CreateExpectedIndicationRequest request)
    {
        var entity = new ExpectedIndicationResult
        {
            MediaTypeId = request.MediaTypeId, OrganismName = request.OrganismName, ExpectedDescription = request.ExpectedDescription
        };
        _db.ExpectedIndicationResults.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }
}
