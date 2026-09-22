using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
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
public record SpecificationStageDto(int? Id, int StageNumber, string StageLabel, string AcceptanceCriteriaText);
public record CreateSpecificationRequest(
    int ItemId,
    string TestCode,
    string? AlertLimit = null,
    string? ActionLimit = null,
    string? SpecLimit = null,
    string? Unit = null,
    decimal? DilutionFactor = null,
    string? ParameterName = null,
    int? DisplayOrder = null,
    LimitType? LimitType = null,
    string? ReferenceStandard = null,
    decimal? LowerLimit = null,
    decimal? UpperLimit = null,
    bool? LowerInclusive = null,
    bool? UpperInclusive = null,
    decimal? Target = null,
    decimal? Tolerance = null,
    ToleranceMode? ToleranceMode = null,
    string? ExpectedResultText = null,
    ExpectedPresence? ExpectedState = null,
    decimal? SampleQuantity = null,
    string? SampleQuantityUnit = null,
    List<SpecificationStageDto>? Stages = null,
    int? TestAnalyteId = null,
    ResultBasis? ResultBasis = null,
    SampleMatrix? SampleMatrix = null,
    decimal? LabelClaim = null,
    string? LabelClaimUnit = null,
    decimal? ConversionFactor = null,
    DosageForm? DosageForm = null);

public record UpdateSpecificationRequest(
    string TestCode,
    string? AlertLimit = null,
    string? ActionLimit = null,
    string? SpecLimit = null,
    string? Unit = null,
    decimal? DilutionFactor = null,
    string? ParameterName = null,
    int? DisplayOrder = null,
    LimitType? LimitType = null,
    string? ReferenceStandard = null,
    decimal? LowerLimit = null,
    decimal? UpperLimit = null,
    bool? LowerInclusive = null,
    bool? UpperInclusive = null,
    decimal? Target = null,
    decimal? Tolerance = null,
    ToleranceMode? ToleranceMode = null,
    string? ExpectedResultText = null,
    ExpectedPresence? ExpectedState = null,
    decimal? SampleQuantity = null,
    string? SampleQuantityUnit = null,
    List<SpecificationStageDto>? Stages = null,
    int? TestAnalyteId = null,
    ResultBasis? ResultBasis = null,
    SampleMatrix? SampleMatrix = null,
    decimal? LabelClaim = null,
    string? LabelClaimUnit = null,
    decimal? ConversionFactor = null,
    DosageForm? DosageForm = null);
public record CreateDiluentTypeRequest(string Name, bool RequiresBatchTracking, int? MaterialId);
public record CreateProductionStageRequest(string Name, ProductionStageRole Role);
public record UpdateProductionStageRequest(string Name, ProductionStageRole Role);
public record CreateEquipmentRequest(
    string Name,
    string Code,
    EquipmentType Type,
    string? Location,
    decimal? SetPointTemperature,
    DateTime? CalibrationDueDate,
    string? Vendor = null,
    CdsSoftware? CdsSoftware = null,
    string? ConnectionSettings = null,
    int? SectionId = null);

public record UpdateEquipmentRequest(
    string Name,
    string Code,
    EquipmentType Type,
    string? Location,
    decimal? SetPointTemperature,
    DateTime? CalibrationDueDate,
    string? Vendor = null,
    CdsSoftware? CdsSoftware = null,
    string? ConnectionSettings = null,
    int? SectionId = null);
public record CreateRoomTestConfigRequest(int RoomId, string TestType, string TestCode, string AlertLimit, string ActionLimit, string SpecLimit, string? Unit = null);
public record CreateMachinePartConfigRequest(int MachinePartId, string TestType, string TestCode, string AlertLimit, string ActionLimit, string SpecLimit, bool IsPathogenTest, string? Unit = null);
public record CreateMediaProductRequest(string Name, string Code);
public record UpdateMediaProductRequest(string Name);
public record ChangeMediaProductCodeRequest(string Code, string Reason, string Password);
public record CreateMediaIncubationConditionRequest(int MediaProductId, int IncubationMinHours, int IncubationMaxHours, decimal TemperatureMin, decimal TemperatureMax);
public record UpdateMediaIncubationConditionRequest(int IncubationMinHours, int IncubationMaxHours, decimal TemperatureMin, decimal TemperatureMax);
public record CreateMediaConfigurationChallengeRequest(int OrganismId, ChallengeRole? ChallengeRole, string? ExpectedDescription, string? InitialInoculum);
public record CreateMediaConfigurationRequest(int MediaProductId, EvaluationType EvaluationType, int MediaIncubationConditionId, decimal? RecoveryPercentMin, decimal? RecoveryPercentMax, List<CreateMediaConfigurationChallengeRequest>? Challenges);
public record UpdateMediaConfigurationRequest(int MediaProductId, EvaluationType EvaluationType, int MediaIncubationConditionId, decimal? RecoveryPercentMin, decimal? RecoveryPercentMax, List<CreateMediaConfigurationChallengeRequest>? Challenges);
public record CreateOrganismRequest(string ScientificName, string? AtccNumber, string? CommonName, string? Description);
public record UpdateOrganismRequest(string ScientificName, string? AtccNumber, string? CommonName, string? Description);
public record CreateTestDefinitionRequest(
    string Code,
    string DisplayName,
    int? SectionId = null,
    WorkflowType WorkflowType = WorkflowType.Observation,
    EquationType EquationType = EquationType.None,
    bool RequiresSystemSuitability = false,
    string? MethodAbbreviation = null,
    decimal? SstMaxRsdPercent = null,
    decimal? SstMinResolution = null,
    decimal? SstMaxTailingFactor = null,
    decimal? SstMinTheoreticalPlates = null,
    CalibrationEntryMode? CalibrationEntryMode = null,
    decimal? CalMinCorrelation = null,
    CorrelationType? CalCorrelationType = null,
    int? CalMinStandards = null,
    decimal? CalCheckRecoveryLowPercent = null,
    decimal? CalCheckRecoveryHighPercent = null,
    decimal? CalBlankMax = null,
    decimal? CalIsRecoveryLowPercent = null,
    decimal? CalIsRecoveryHighPercent = null,
    bool? CalRequireBlank = null,
    bool? CalRequireIcv = null,
    bool? CalRequireCcv = null,
    bool? CalRequireInternalStandard = null,
    ReportedConcentrationBasis? ReportedConcentrationBasis = null,
    int? CalMaxRunAgeHours = null,
    int? ReplicateCount = null,
    MeasurementEvaluationBasis? EvaluationBasis = null,
    string? ConditionFields = null,
    bool? UsesTare = null,
    decimal? DissolutionS1Offset = null,
    decimal? DissolutionS2MinOffset = null,
    decimal? DissolutionS3MinOffset = null,
    decimal? DissolutionS3MaxBelowS2Min = null,
    int? DisintegrationStage1Units = null,
    int? DisintegrationStage2Units = null,
    int? DisintegrationMaxStage1Failures = null,
    int? DisintegrationMinPassTotal = null,
    int? WvUnitCount = null,
    decimal? WvTabletBand1MaxMg = null,
    decimal? WvTabletBand1Percent = null,
    decimal? WvTabletBand2MaxMg = null,
    decimal? WvTabletBand2Percent = null,
    decimal? WvTabletBand3Percent = null,
    int? WvTabletMaxOutside = null,
    decimal? WvCapsuleInnerPercent = null,
    decimal? WvCapsuleOuterPercent = null,
    int? WvCapsuleS1MaxOutside = null,
    int? WvCapsuleS1MaxForRetest = null,
    int? WvCapsuleS2ExtraUnits = null,
    int? WvCapsuleS2MaxOutside = null,
    int? HplcPreparations = null,
    int? HplcInjectionsPerPreparation = null,
    decimal? HplcMaxPreparationRsdPercent = null);
// SectionId: move the test to another laboratory section (null = keep). Test
// orders already created keep the section they were created with.
public record UpdateTestDefinitionRequest(
    string Code,
    string DisplayName,
    int? SectionId = null,
    WorkflowType? WorkflowType = null,
    EquationType? EquationType = null,
    bool? RequiresSystemSuitability = null,
    string? MethodAbbreviation = null,
    decimal? SstMaxRsdPercent = null,
    decimal? SstMinResolution = null,
    decimal? SstMaxTailingFactor = null,
    decimal? SstMinTheoreticalPlates = null,
    CalibrationEntryMode? CalibrationEntryMode = null,
    decimal? CalMinCorrelation = null,
    CorrelationType? CalCorrelationType = null,
    int? CalMinStandards = null,
    decimal? CalCheckRecoveryLowPercent = null,
    decimal? CalCheckRecoveryHighPercent = null,
    decimal? CalBlankMax = null,
    decimal? CalIsRecoveryLowPercent = null,
    decimal? CalIsRecoveryHighPercent = null,
    bool? CalRequireBlank = null,
    bool? CalRequireIcv = null,
    bool? CalRequireCcv = null,
    bool? CalRequireInternalStandard = null,
    ReportedConcentrationBasis? ReportedConcentrationBasis = null,
    int? CalMaxRunAgeHours = null,
    int? ReplicateCount = null,
    MeasurementEvaluationBasis? EvaluationBasis = null,
    string? ConditionFields = null,
    bool? UsesTare = null,
    decimal? DissolutionS1Offset = null,
    decimal? DissolutionS2MinOffset = null,
    decimal? DissolutionS3MinOffset = null,
    decimal? DissolutionS3MaxBelowS2Min = null,
    int? DisintegrationStage1Units = null,
    int? DisintegrationStage2Units = null,
    int? DisintegrationMaxStage1Failures = null,
    int? DisintegrationMinPassTotal = null,
    int? WvUnitCount = null,
    decimal? WvTabletBand1MaxMg = null,
    decimal? WvTabletBand1Percent = null,
    decimal? WvTabletBand2MaxMg = null,
    decimal? WvTabletBand2Percent = null,
    decimal? WvTabletBand3Percent = null,
    int? WvTabletMaxOutside = null,
    decimal? WvCapsuleInnerPercent = null,
    decimal? WvCapsuleOuterPercent = null,
    int? WvCapsuleS1MaxOutside = null,
    int? WvCapsuleS1MaxForRetest = null,
    int? WvCapsuleS2ExtraUnits = null,
    int? WvCapsuleS2MaxOutside = null,
    int? HplcPreparations = null,
    int? HplcInjectionsPerPreparation = null,
    decimal? HplcMaxPreparationRsdPercent = null);
public record UpdateWorkflowTypeRequest(WorkflowType WorkflowType);

public record StepMediaRequest(int MaterialId, bool IsRequired, int DisplayOrder, int? MediaIncubationConditionId);
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
    private readonly MediaProductService _mediaProductService;
    private readonly MediaIncubationConditionService _mediaIncubationConditionService;
    private readonly IUserSectionScopeService _scope;
    private readonly ChromatographyColumnService _columnService;
    private readonly SpecificationService _specificationService;

    public MasterDataController(
        MicroLimsDbContext db,
        EquipmentConfigurationService configService,
        MediaProductService mediaProductService,
        MediaIncubationConditionService mediaIncubationConditionService,
        IUserSectionScopeService scope,
        ChromatographyColumnService columnService,
        SpecificationService? specificationService = null)
    {
        _db = db;
        _configService = configService;
        _mediaProductService = mediaProductService;
        _mediaIncubationConditionService = mediaIncubationConditionService;
        _scope = scope;
        _columnService = columnService;
        _specificationService = specificationService ?? new SpecificationService(db);
    }

    private int CurrentUserId => int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;

    // ---- Water Sampling Points ----
    [HttpGet("water-sampling-points")]
    public async Task<IActionResult> GetWaterSamplingPoints() =>
        Ok(ApiResponse<object>.Ok(await _db.WaterSamplingPoints.AsNoTracking().ToListAsync()));

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
        Ok(ApiResponse<object>.Ok(await _db.SamplingConfigurations.AsNoTracking().Where(c => c.WaterSamplingPointId == pointId).ToListAsync()));

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
        var departments = await _db.Departments
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
        var rooms = await _db.Rooms
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
        var machines = await _db.Machines
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
        Ok(ApiResponse<object>.Ok(await _specificationService.GetForItemAsync(itemId)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("specifications")]
    public async Task<IActionResult> CreateSpecification(CreateSpecificationRequest request)
    {
        var limitType = request.LimitType ?? LimitType.CountTiered;
        var paramName = request.ParameterName;
        if (string.IsNullOrWhiteSpace(paramName))
        {
            var testDef = await _db.TestDefinitions.FirstOrDefaultAsync(t => t.Code == request.TestCode);
            paramName = !string.IsNullOrWhiteSpace(testDef?.DisplayName) ? testDef.DisplayName : request.TestCode;
        }

        var spec = new Specification
        {
            ItemId = request.ItemId,
            TestCode = request.TestCode,
            ParameterName = paramName,
            DisplayOrder = request.DisplayOrder ?? 0,
            LimitType = limitType,
            ReferenceStandard = request.ReferenceStandard,
            LowerLimit = request.LowerLimit,
            UpperLimit = request.UpperLimit,
            LowerInclusive = request.LowerInclusive ?? true,
            UpperInclusive = request.UpperInclusive ?? true,
            Target = request.Target,
            Tolerance = request.Tolerance,
            ToleranceMode = request.ToleranceMode,
            ExpectedResultText = request.ExpectedResultText,
            ExpectedState = request.ExpectedState,
            SampleQuantity = request.SampleQuantity,
            SampleQuantityUnit = request.SampleQuantityUnit,
            AlertLimit = request.AlertLimit ?? string.Empty,
            ActionLimit = request.ActionLimit ?? string.Empty,
            SpecLimit = request.SpecLimit ?? string.Empty,
            Unit = request.Unit ?? string.Empty,
            DilutionFactor = request.DilutionFactor,
            TestAnalyteId = request.TestAnalyteId,
            ResultBasis = request.ResultBasis,
            SampleMatrix = request.SampleMatrix,
            LabelClaim = request.LabelClaim,
            LabelClaimUnit = request.LabelClaimUnit,
            ConversionFactor = request.ConversionFactor ?? 1.0m,
            DosageForm = request.DosageForm,
            Stages = request.Stages?.Select(s => new SpecificationStage
            {
                StageNumber = s.StageNumber,
                StageLabel = s.StageLabel,
                AcceptanceCriteriaText = s.AcceptanceCriteriaText
            }).ToList() ?? new List<SpecificationStage>()
        };

        SpecificationService.ApplyCanonicalSpecLimit(spec);
        await _specificationService.ValidateAsync(spec);

        _db.Specifications.Add(spec);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(spec));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("specifications/{id}")]
    public async Task<IActionResult> UpdateSpecification(int id, UpdateSpecificationRequest request)
    {
        var spec = await _db.Specifications.Include(s => s.Stages).FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new InvalidOperationException($"Specification {id} not found.");

        spec.TestCode = request.TestCode;
        if (request.ParameterName != null)
            spec.ParameterName = request.ParameterName;
        if (request.DisplayOrder.HasValue)
            spec.DisplayOrder = request.DisplayOrder.Value;
        if (request.LimitType.HasValue)
            spec.LimitType = request.LimitType.Value;
        spec.ReferenceStandard = request.ReferenceStandard;
        spec.LowerLimit = request.LowerLimit;
        spec.UpperLimit = request.UpperLimit;
        if (request.LowerInclusive.HasValue)
            spec.LowerInclusive = request.LowerInclusive.Value;
        if (request.UpperInclusive.HasValue)
            spec.UpperInclusive = request.UpperInclusive.Value;
        spec.Target = request.Target;
        spec.Tolerance = request.Tolerance;
        spec.ToleranceMode = request.ToleranceMode;
        spec.ExpectedResultText = request.ExpectedResultText;
        spec.ExpectedState = request.ExpectedState;
        spec.SampleQuantity = request.SampleQuantity;
        spec.SampleQuantityUnit = request.SampleQuantityUnit;
        if (request.AlertLimit != null)
            spec.AlertLimit = request.AlertLimit;
        if (request.ActionLimit != null)
            spec.ActionLimit = request.ActionLimit;
        if (request.SpecLimit != null)
            spec.SpecLimit = request.SpecLimit;
        spec.Unit = request.Unit ?? string.Empty;
        spec.DilutionFactor = request.DilutionFactor;
        spec.TestAnalyteId = request.TestAnalyteId;
        spec.ResultBasis = request.ResultBasis;
        spec.SampleMatrix = request.SampleMatrix;
        spec.LabelClaim = request.LabelClaim;
        spec.LabelClaimUnit = request.LabelClaimUnit;
        spec.DosageForm = request.DosageForm;
        if (request.ConversionFactor.HasValue)
            spec.ConversionFactor = request.ConversionFactor.Value;

        // Replace-all on stages
        _db.SpecificationStages.RemoveRange(spec.Stages);
        spec.Stages = request.Stages?.Select(s => new SpecificationStage
        {
            SpecificationId = spec.Id,
            StageNumber = s.StageNumber,
            StageLabel = s.StageLabel,
            AcceptanceCriteriaText = s.AcceptanceCriteriaText
        }).ToList() ?? new List<SpecificationStage>();

        SpecificationService.ApplyCanonicalSpecLimit(spec);
        await _specificationService.ValidateAsync(spec);

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
    public async Task<IActionResult> GetCausesOfTesting() => Ok(ApiResponse<object>.Ok(await _db.CausesOfTesting.AsNoTracking().Where(c => c.IsActive).ToListAsync()));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("causes-of-testing")]
    public async Task<IActionResult> CreateCauseOfTesting([FromBody] string name)
    {
        if (await _db.CausesOfTesting.AnyAsync(c => c.Name.ToLower() == name.ToLower()))
            throw new InvalidOperationException($"Cause of Testing \"{name}\" already exists.");

        var entity = new CauseOfTesting { Name = name };
        _db.CausesOfTesting.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("causes-of-testing/{id}")]
    public async Task<IActionResult> UpdateCauseOfTesting(int id, [FromBody] string name)
    {
        var entity = await _db.CausesOfTesting.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new InvalidOperationException($"Cause of Testing {id} not found.");

        if (await _db.CausesOfTesting.AnyAsync(c => c.Id != id && c.Name.ToLower() == name.ToLower()))
            throw new InvalidOperationException($"Cause of Testing \"{name}\" already exists.");

        entity.Name = name;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    // Blocked (not a raw FK error) if any Sample still references this
    // cause - real FK (Sample.CauseOfTestingId is Restrict), same
    // "guard with a clear message" pattern as DeleteOrganism.
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("causes-of-testing/{id}")]
    public async Task<IActionResult> DeleteCauseOfTesting(int id)
    {
        var entity = await _db.CausesOfTesting.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new InvalidOperationException($"Cause of Testing {id} not found.");

        var sampleCount = await _db.Samples.CountAsync(s => s.CauseOfTestingId == id);
        if (sampleCount > 0)
            throw new InvalidOperationException($"Cannot delete '{entity.Name}' - it is referenced by {sampleCount} sample(s).");

        _db.CausesOfTesting.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    // ---- Samplers (suggestion list for the free-text "Sampled By" field on
    // Sample - not an FK, so no delete guard needed: removing a name here
    // never touches historical Sample rows, which store the string directly) ----
    [HttpGet("samplers")]
    public async Task<IActionResult> GetSamplers() => Ok(ApiResponse<object>.Ok(await _db.Samplers.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync()));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("samplers")]
    public async Task<IActionResult> CreateSampler([FromBody] string name)
    {
        if (await _db.Samplers.AnyAsync(s => s.Name.ToLower() == name.ToLower()))
            throw new InvalidOperationException($"Sampler \"{name}\" already exists.");

        var entity = new Sampler { Name = name };
        _db.Samplers.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("samplers/{id}")]
    public async Task<IActionResult> UpdateSampler(int id, [FromBody] string name)
    {
        var entity = await _db.Samplers.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new InvalidOperationException($"Sampler {id} not found.");

        if (await _db.Samplers.AnyAsync(s => s.Id != id && s.Name.ToLower() == name.ToLower()))
            throw new InvalidOperationException($"Sampler \"{name}\" already exists.");

        entity.Name = name;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("samplers/{id}")]
    public async Task<IActionResult> DeleteSampler(int id)
    {
        var entity = await _db.Samplers.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new InvalidOperationException($"Sampler {id} not found.");

        _db.Samplers.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    // ---- Production Stages (fixed-choice list for the Finished Product
    // receiving form's Production Stage field - not an FK, same rationale
    // as Samplers above) ----
    [HttpGet("production-stages")]
    public async Task<IActionResult> GetProductionStages() => Ok(ApiResponse<object>.Ok(await _db.ProductionStages.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync()));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("production-stages")]
    public async Task<IActionResult> CreateProductionStage(CreateProductionStageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Name is required.");

        if (await _db.ProductionStages.AnyAsync(s => s.Name.ToLower() == request.Name.ToLower()))
            throw new InvalidOperationException($"Production Stage \"{request.Name}\" already exists.");

        var entity = new ProductionStage { Name = request.Name, Role = request.Role };
        _db.ProductionStages.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("production-stages/{id}")]
    public async Task<IActionResult> UpdateProductionStage(int id, UpdateProductionStageRequest request)
    {
        var entity = await _db.ProductionStages.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new InvalidOperationException($"Production Stage {id} not found.");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Name is required.");

        if (await _db.ProductionStages.AnyAsync(s => s.Id != id && s.Name.ToLower() == request.Name.ToLower()))
            throw new InvalidOperationException($"Production Stage \"{request.Name}\" already exists.");

        entity.Name = request.Name;
        entity.Role = request.Role;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("production-stages/{id}")]
    public async Task<IActionResult> DeleteProductionStage(int id)
    {
        var entity = await _db.ProductionStages.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new InvalidOperationException($"Production Stage {id} not found.");

        _db.ProductionStages.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    // ---- Diluent Types ----
    [HttpGet("diluent-types")]
    public async Task<IActionResult> GetDiluentTypes() => Ok(ApiResponse<object>.Ok(await _db.DiluentTypes.AsNoTracking().ToListAsync()));

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
    public async Task<IActionResult> GetNeutralizers() => Ok(ApiResponse<object>.Ok(await _db.Neutralizers.AsNoTracking().Where(n => n.IsActive).ToListAsync()));

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
        var scope = await _scope.GetAccessibleSectionIdsAsync(CurrentUserId);
        var query = _db.Equipment.AsNoTracking().Include(e => e.Section).AsQueryable();
        if (scope != null) query = query.Where(e => scope.Contains(e.SectionId));
        if (type.HasValue) query = query.Where(e => e.Type == type.Value);
        return Ok(ApiResponse<object>.Ok(await query.ToListAsync()));
    }

    [HttpGet("equipment/{id:int}")]
    public async Task<IActionResult> GetEquipmentById(int id)
    {
        await _scope.EnsureEquipmentAccessAsync(CurrentUserId, id);
        var eq = await _db.Equipment.AsNoTracking()
            .Include(e => e.Section)
            .Include(e => e.CompatibleColumns)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (eq == null)
            throw new InvalidOperationException($"Equipment {id} not found.");
        return Ok(ApiResponse<object>.Ok(eq));
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
        await _scope.EnsureEquipmentAccessAsync(CurrentUserId, id);
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
    public async Task<IActionResult> GetSetPointHistory(int id)
    {
        await _scope.EnsureEquipmentAccessAsync(CurrentUserId, id);
        return Ok(ApiResponse<object>.Ok(await _configService.GetIncubatorSetPointHistoryAsync(id)));
    }

    [HttpGet("equipment/{id:int}/autoclave-programs")]
    public async Task<IActionResult> GetAutoclavePrograms(int id, [FromQuery] bool? activeOnly)
    {
        await _scope.EnsureEquipmentAccessAsync(CurrentUserId, id);
        return Ok(ApiResponse<object>.Ok(await _configService.GetAutoclaveProgramsAsync(id, activeOnly)));
    }

    [HttpGet("equipment/autoclave-programs/all")]
    public async Task<IActionResult> GetAllAutoclavePrograms([FromQuery] bool? activeOnly) =>
        Ok(ApiResponse<object>.Ok(await _configService.GetAutoclaveProgramsAsync(null, activeOnly)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("equipment/{id:int}/autoclave-programs")]
    public async Task<IActionResult> SaveAutoclaveProgram(int id, [FromBody] SaveAutoclaveProgramRequest request)
    {
        await _scope.EnsureEquipmentAccessAsync(CurrentUserId, id);
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
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Name is required.");
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new InvalidOperationException("Code is required.");
        if (await _db.Equipment.AnyAsync(e => e.Code == request.Code.Trim()))
            throw new InvalidOperationException($"Equipment code \"{request.Code}\" already exists.");

        if (!string.IsNullOrWhiteSpace(request.Vendor) && request.Vendor.Length > 100)
            throw new InvalidOperationException("Vendor cannot exceed 100 characters.");

        if (request.Type == EquipmentType.Hplc)
        {
            if (!request.CdsSoftware.HasValue)
                throw new InvalidOperationException("CDS Software is required for HPLC equipment.");
        }
        else if (request.Type == EquipmentType.IcpOes)
        {
            if (!request.CdsSoftware.HasValue)
                throw new InvalidOperationException("CDS Software is required for ICP-OES equipment.");
        }
        else
        {
            if (request.CdsSoftware.HasValue)
                throw new InvalidOperationException("CDS Software is only allowed for HPLC and ICP-OES equipment.");
        }

        var sectionId = await _scope.ResolveSectionForCreateAsync(CurrentUserId, request.SectionId);

        var entity = new Equipment
        {
            Name = request.Name.Trim(),
            Code = request.Code.Trim(),
            Type = request.Type,
            Location = request.Location,
            SetPointTemperature = request.SetPointTemperature,
            CalibrationDueDate = request.CalibrationDueDate,
            Vendor = request.Vendor?.Trim(),
            CdsSoftware = request.CdsSoftware,
            ConnectionSettings = request.ConnectionSettings,
            SectionId = sectionId
        };
        _db.Equipment.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("equipment/{id:int}")]
    public async Task<IActionResult> UpdateEquipment(int id, UpdateEquipmentRequest request)
    {
        await _scope.EnsureEquipmentAccessAsync(CurrentUserId, id);

        var entity = await _db.Equipment.FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new InvalidOperationException($"Equipment {id} not found.");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Name is required.");
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new InvalidOperationException("Code is required.");
        if (await _db.Equipment.AnyAsync(e => e.Code == request.Code.Trim() && e.Id != id))
            throw new InvalidOperationException($"Equipment code \"{request.Code}\" already exists.");

        if (!string.IsNullOrWhiteSpace(request.Vendor) && request.Vendor.Length > 100)
            throw new InvalidOperationException("Vendor cannot exceed 100 characters.");

        if (request.Type == EquipmentType.Hplc)
        {
            if (!request.CdsSoftware.HasValue)
                throw new InvalidOperationException("CDS Software is required for HPLC equipment.");
        }
        else if (request.Type == EquipmentType.IcpOes)
        {
            if (!request.CdsSoftware.HasValue)
                throw new InvalidOperationException("CDS Software is required for ICP-OES equipment.");
        }
        else
        {
            if (request.CdsSoftware.HasValue)
                throw new InvalidOperationException("CDS Software is only allowed for HPLC and ICP-OES equipment.");
        }

        if (request.SectionId.HasValue && request.SectionId.Value != entity.SectionId)
        {
            entity.SectionId = await _scope.ResolveSectionForCreateAsync(CurrentUserId, request.SectionId);
        }

        entity.Name = request.Name.Trim();
        entity.Code = request.Code.Trim();
        entity.Type = request.Type;
        entity.Location = request.Location;
        entity.SetPointTemperature = request.SetPointTemperature;
        entity.CalibrationDueDate = request.CalibrationDueDate;
        entity.Vendor = request.Vendor?.Trim();
        entity.CdsSoftware = request.CdsSoftware;
        entity.ConnectionSettings = request.ConnectionSettings;

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    // ---- Column Master (REQ-FP-011) ----
    [HttpGet("columns")]
    public async Task<IActionResult> GetColumns([FromQuery] bool? activeOnly) =>
        Ok(ApiResponse<object>.Ok(await _columnService.GetAllAsync(CurrentUserId, activeOnly)));

    [HttpGet("columns/{id:int}")]
    public async Task<IActionResult> GetColumnById(int id) =>
        Ok(ApiResponse<object>.Ok(await _columnService.GetByIdAsync(id, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.EquipmentManage)]
    [HttpPost("columns")]
    public async Task<IActionResult> CreateColumn([FromBody] CreateChromatographyColumnRequest request) =>
        Ok(ApiResponse<object>.Ok(await _columnService.CreateAsync(request, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.EquipmentManage)]
    [HttpPut("columns/{id:int}")]
    public async Task<IActionResult> UpdateColumn(int id, [FromBody] UpdateChromatographyColumnRequest request) =>
        Ok(ApiResponse<object>.Ok(await _columnService.UpdateAsync(id, request, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.EquipmentManage)]
    [HttpPut("columns/{id:int}/deactivate")]
    public async Task<IActionResult> DeactivateColumn(int id) =>
        Ok(ApiResponse<object>.Ok(await _columnService.DeactivateAsync(id, CurrentUserId)));

    // ---- Room Test Configurations (EM) ----
    [HttpGet("room-test-configurations")]
    public async Task<IActionResult> GetRoomTestConfigurations([FromQuery] int roomId) =>
        Ok(ApiResponse<object>.Ok(await _db.RoomTestConfigurations.AsNoTracking().Where(c => c.RoomId == roomId).ToListAsync()));

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
        Ok(ApiResponse<object>.Ok(await _db.MachinePartConfigurations.AsNoTracking().Where(c => c.MachinePartId == machinePartId).ToListAsync()));

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

    // ---- Media Products (Media Product Master) ----
    [HttpGet("media-products")]
    public async Task<IActionResult> GetMediaProducts() =>
        Ok(ApiResponse<object>.Ok(await _mediaProductService.GetAllAsync()));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("media-products")]
    public async Task<IActionResult> CreateMediaProduct(CreateMediaProductRequest request) =>
        Ok(ApiResponse<object>.Ok(await _mediaProductService.CreateAsync(request.Name, request.Code)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("media-products/{id}")]
    public async Task<IActionResult> UpdateMediaProduct(int id, UpdateMediaProductRequest request) =>
        Ok(ApiResponse<object>.Ok(await _mediaProductService.RenameAsync(id, request.Name)));

    [Authorize(Roles = RoleConstants.SectionHead)]
    [HttpPut("media-products/{id}/code")]
    public async Task<IActionResult> ChangeMediaProductCode(int id, ChangeMediaProductCodeRequest request) =>
        Ok(ApiResponse<object>.Ok(await _mediaProductService.ChangeCodeAsync(
            id, request.Code, request.Reason, request.Password, CurrentUserId, HttpContext.Connection.RemoteIpAddress?.ToString())));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("media-products/{id}")]
    public async Task<IActionResult> DeleteMediaProduct(int id)
    {
        await _mediaProductService.DeleteAsync(id);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    // ---- Media Incubation Conditions ----
    [HttpGet("media-incubation-conditions")]
    public async Task<IActionResult> GetMediaIncubationConditions([FromQuery] int? mediaProductId) =>
        Ok(ApiResponse<object>.Ok(await _mediaIncubationConditionService.GetAllAsync(mediaProductId)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("media-incubation-conditions")]
    public async Task<IActionResult> CreateMediaIncubationCondition(CreateMediaIncubationConditionRequest request) =>
        Ok(ApiResponse<object>.Ok(await _mediaIncubationConditionService.CreateAsync(
            request.MediaProductId, request.IncubationMinHours, request.IncubationMaxHours, request.TemperatureMin, request.TemperatureMax)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("media-incubation-conditions/{id}")]
    public async Task<IActionResult> UpdateMediaIncubationCondition(int id, UpdateMediaIncubationConditionRequest request) =>
        Ok(ApiResponse<object>.Ok(await _mediaIncubationConditionService.UpdateAsync(
            id, request.IncubationMinHours, request.IncubationMaxHours, request.TemperatureMin, request.TemperatureMax)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("media-incubation-conditions/{id}")]
    public async Task<IActionResult> DeleteMediaIncubationCondition(int id)
    {
        await _mediaIncubationConditionService.DeleteAsync(id);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    // ---- Media Configurations (Media Configuration Migration) ----
    [HttpGet("media-configurations")]
    public async Task<IActionResult> GetMediaConfigurations()
    {
        var configs = await _db.MediaConfigurations
            .Include(m => m.IncubationCondition)
            .OrderBy(m => m.Name)
            .Select(m => new
            {
                m.Id,
                m.Name,
                m.MediaProductId,
                MediaProductCode = m.MediaProduct != null ? m.MediaProduct.Code : null,
                m.EvaluationType,
                m.MediaIncubationConditionId,
                IncubationMinHours = m.IncubationCondition != null ? m.IncubationCondition.IncubationMinHours : 0,
                IncubationMaxHours = m.IncubationCondition != null ? m.IncubationCondition.IncubationMaxHours : 0,
                TemperatureMin = m.IncubationCondition != null ? m.IncubationCondition.TemperatureMin : 0m,
                TemperatureMax = m.IncubationCondition != null ? m.IncubationCondition.TemperatureMax : 0m,
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
        var product = await _db.MediaProducts.FirstOrDefaultAsync(p => p.Id == request.MediaProductId)
            ?? throw new InvalidOperationException($"Media product with ID {request.MediaProductId} not found.");

        var condition = await _db.MediaIncubationConditions.FirstOrDefaultAsync(c => c.Id == request.MediaIncubationConditionId)
            ?? throw new InvalidOperationException($"Incubation condition {request.MediaIncubationConditionId} not found.");
        if (condition.MediaProductId != product.Id)
        {
            throw new InvalidOperationException("The chosen incubation condition belongs to a different media product.");
        }

        var exists = await _db.MediaConfigurations.AnyAsync(m => m.MediaProductId == request.MediaProductId);
        if (exists)
        {
            throw new InvalidOperationException($"'{product.Name}' already has an evaluation configuration. Edit it instead.");
        }

        if (!Enum.IsDefined(typeof(EvaluationType), request.EvaluationType))
            throw new InvalidOperationException("Valid evaluation type is required.");

        if (request.EvaluationType == EvaluationType.GrowthPromotion)
        {
            if (request.RecoveryPercentMin.HasValue && request.RecoveryPercentMax.HasValue &&
                request.RecoveryPercentMin > request.RecoveryPercentMax)
            {
                throw new InvalidOperationException("Recovery percent min cannot exceed recovery percent max.");
            }
        }

        var entity = new MediaConfiguration
        {
            MediaProductId = product.Id,
            Name = product.Name,
            EvaluationType = request.EvaluationType,
            MediaIncubationConditionId = condition.Id,
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
            entity.MediaProductId,
            MediaProductCode = product.Code,
            entity.EvaluationType,
            entity.MediaIncubationConditionId,
            condition.IncubationMinHours,
            condition.IncubationMaxHours,
            condition.TemperatureMin,
            condition.TemperatureMax,
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

        if (request.MediaProductId != entity.MediaProductId)
        {
            throw new InvalidOperationException("A configuration can't be moved to another media product.");
        }

        var product = await _db.MediaProducts.FirstOrDefaultAsync(p => p.Id == entity.MediaProductId)
            ?? throw new InvalidOperationException($"Media product with ID {entity.MediaProductId} not found.");

        var condition = await _db.MediaIncubationConditions.FirstOrDefaultAsync(c => c.Id == request.MediaIncubationConditionId)
            ?? throw new InvalidOperationException($"Incubation condition {request.MediaIncubationConditionId} not found.");
        if (condition.MediaProductId != entity.MediaProductId)
        {
            throw new InvalidOperationException("The chosen incubation condition belongs to a different media product.");
        }

        if (!Enum.IsDefined(typeof(EvaluationType), request.EvaluationType))
            throw new InvalidOperationException("Valid evaluation type is required.");

        if (request.EvaluationType == EvaluationType.GrowthPromotion)
        {
            if (request.RecoveryPercentMin.HasValue && request.RecoveryPercentMax.HasValue &&
                request.RecoveryPercentMin > request.RecoveryPercentMax)
            {
                throw new InvalidOperationException("Recovery percent min cannot exceed recovery percent max.");
            }
        }

        entity.Name = product.Name;
        entity.EvaluationType = request.EvaluationType;
        entity.MediaIncubationConditionId = condition.Id;
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
            entity.MediaProductId,
            MediaProductCode = product.Code,
            entity.EvaluationType,
            entity.MediaIncubationConditionId,
            condition.IncubationMinHours,
            condition.IncubationMaxHours,
            condition.TemperatureMin,
            condition.TemperatureMax,
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

        _db.MediaConfigurations.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    // ---- Organisms (canonical master list backing every OrganismId FK -
    // MediaChallengeSpec, MediaEvaluationChallenge, Cryovial, Material) ----
    [HttpGet("organisms")]
    public async Task<IActionResult> GetOrganisms() =>
        Ok(ApiResponse<object>.Ok(await _db.Organisms.AsNoTracking().OrderBy(o => o.ScientificName).ToListAsync()));

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

    // Equation Types (REQ-FP-030/042)
    [HttpGet("equation-types")]
    public IActionResult GetEquationTypes()
    {
        var types = new[]
        {
            new EquationTypeDto(
                Code: nameof(EquationType.None),
                Name: "None",
                FormulaText: string.Empty,
                RequiredInputs: Array.Empty<string>()),
            new EquationTypeDto(
                Code: nameof(EquationType.HplcAssay),
                Name: "HPLC Assay",
                FormulaText: "% Assay = (SampleArea / StandardMeanArea) * (StandardWeightMg / SampleWeightMg) * (StandardPurityPercent / 100) * (SampleDilution / StandardDilution) * 100",
                RequiredInputs: new[]
                {
                    "SampleArea",
                    "StandardMeanArea",
                    "SampleWeightMg",
                    "StandardWeightMg",
                    "StandardPurityPercent",
                    "SampleDilution",
                    "StandardDilution"
                }),
            new EquationTypeDto(
                Code: nameof(EquationType.HplcMultiAnalyte),
                Name: "HPLC Multi-Analyte",
                FormulaText: "amount = (Au / As) * Cs * Dsample * UnitAmount / SampleAmount; result = amount * ConversionFactor",
                RequiredInputs: new[] { "Au", "As", "Cs", "Dsample", "UnitAmount", "SampleAmount" }),
            new EquationTypeDto(
                Code: nameof(EquationType.SystemSuitability),
                Name: "System Suitability",
                FormulaText: "RSD <= MaxRSD, Resolution >= MinResolution, Tailing <= MaxTailing, Plates >= MinPlates",
                RequiredInputs: new[]
                {
                    "RsdPercent",
                    "Resolution",
                    "TailingFactor",
                    "TheoreticalPlates"
                }),
            new EquationTypeDto(
                Code: nameof(EquationType.CalibrationCurve),
                Name: "Calibration Curve",
                FormulaText: "Linear regression y = mx + b, correlation, blank and check recovery criteria",
                RequiredInputs: new[] { "ReportedPpm" }),
            new EquationTypeDto(
                Code: nameof(EquationType.Measurement),
                Name: "Numeric Measurement",
                FormulaText: "Mean, Min, Max, SD, RSD over replicate readings; evaluated against specification",
                RequiredInputs: new[] { "Readings" }),
            new EquationTypeDto(
                Code: nameof(EquationType.GravimetricLoss),
                Name: "Gravimetric % Loss",
                FormulaText: "% Loss = (W1 - W2) / W1 * 100",
                RequiredInputs: new[] { "W1", "W2" }),
            new EquationTypeDto(
                Code: nameof(EquationType.GravimetricResidue),
                Name: "Gravimetric % Residue",
                FormulaText: "% Residue = W2 / W1 * 100",
                RequiredInputs: new[] { "W1", "W2" }),
            new EquationTypeDto(
                Code: nameof(EquationType.Qualitative),
                Name: "Qualitative / Identification",
                FormulaText: "Conforms / Does Not Conform evaluated against expected text",
                RequiredInputs: new[] { "Conforms" })
        };
        return Ok(ApiResponse<object>.Ok(types));
    }

    // ---- Test Master ----
    // The canonical Code/DisplayName list backing every TestCode picker
    // in the app (Items, Water Sampling Points, Room Test Configurations,
    // Machine Part Configurations). See TestDefinition.cs for why this
    // exists.
    [HttpGet("test-definitions")]
    public async Task<IActionResult> GetTestDefinitions() =>
        Ok(ApiResponse<object>.Ok(await _db.TestDefinitions.AsNoTracking().Include(t => t.Section).OrderBy(t => t.Code).ToListAsync()));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("test-definitions")]
    public async Task<IActionResult> CreateTestDefinition(CreateTestDefinitionRequest request)
    {
        if (await _db.TestDefinitions.AnyAsync(t => t.Code == request.Code))
            throw new InvalidOperationException($"Test code \"{request.Code}\" already exists in the Test Master.");

        var userId = CurrentUserId;
        var sectionId = await _scope.ResolveSectionForCreateAsync(userId, request.SectionId);

        string? methodAbbr = string.IsNullOrWhiteSpace(request.MethodAbbreviation)
            ? null
            : request.MethodAbbreviation.Trim().ToUpperInvariant();

        if ((request.WorkflowType == WorkflowType.Disintegration || request.EquationType == EquationType.Disintegration) && request.RequiresSystemSuitability)
            throw new InvalidOperationException("Disintegration tests must not require system suitability.");

        if ((request.WorkflowType == WorkflowType.WeightVariation || request.EquationType == EquationType.WeightVariation) && request.RequiresSystemSuitability)
            throw new InvalidOperationException("Weight variation tests must not require system suitability.");

        if (request.RequiresSystemSuitability)
        {
            if (string.IsNullOrEmpty(methodAbbr))
                throw new InvalidOperationException("Method abbreviation is required when system suitability is enabled.");

            if (!System.Text.RegularExpressions.Regex.IsMatch(methodAbbr, "^[A-Z0-9-]{1,20}$"))
                throw new InvalidOperationException("Method abbreviation must be 1-20 uppercase alphanumeric characters or hyphens.");

            if (request.EquationType != EquationType.HplcMultiAnalyte && request.WorkflowType != WorkflowType.HplcMultiAnalyte &&
                !request.SstMaxRsdPercent.HasValue && !request.SstMinResolution.HasValue &&
                !request.SstMaxTailingFactor.HasValue && !request.SstMinTheoreticalPlates.HasValue)
            {
                throw new InvalidOperationException("At least one system suitability criterion is required when system suitability is enabled.");
            }
        }
        else if (request.EquationType == EquationType.CalibrationCurve)
        {
            if (request.WorkflowType != WorkflowType.ElementalAssay)
                throw new InvalidOperationException("Workflow type must be ElementalAssay when equation type is CalibrationCurve.");

            if (string.IsNullOrEmpty(methodAbbr))
                throw new InvalidOperationException("Method abbreviation is required when equation type is CalibrationCurve.");

            if (!System.Text.RegularExpressions.Regex.IsMatch(methodAbbr, "^[A-Z0-9-]{1,20}$"))
                throw new InvalidOperationException("Method abbreviation must be 1-20 uppercase alphanumeric characters or hyphens.");

            if (!request.CalMinCorrelation.HasValue || request.CalMinCorrelation.Value <= 0m || request.CalMinCorrelation.Value > 1m)
                throw new InvalidOperationException("Minimum correlation must be in (0, 1] when equation type is CalibrationCurve.");

            if (!request.CalCorrelationType.HasValue)
                throw new InvalidOperationException("Correlation type is required when equation type is CalibrationCurve.");

            if (!request.CalMinStandards.HasValue || request.CalMinStandards.Value < 1)
                throw new InvalidOperationException("Minimum standards must be at least 1 when equation type is CalibrationCurve.");

            if (!request.CalCheckRecoveryLowPercent.HasValue || !request.CalCheckRecoveryHighPercent.HasValue)
                throw new InvalidOperationException("Both check recovery window bounds (low and high) are required when equation type is CalibrationCurve.");

            if (request.CalCheckRecoveryLowPercent.Value > request.CalCheckRecoveryHighPercent.Value)
                throw new InvalidOperationException("Check recovery low percent must be less than or equal to high percent.");

            if (request.CalRequireInternalStandard == true)
            {
                if (!request.CalIsRecoveryLowPercent.HasValue || !request.CalIsRecoveryHighPercent.HasValue)
                    throw new InvalidOperationException("Both internal standard recovery bounds (low and high) are required when internal standards are required.");

                if (request.CalIsRecoveryLowPercent.Value > request.CalIsRecoveryHighPercent.Value)
                    throw new InvalidOperationException("Internal standard recovery low percent must be less than or equal to high percent.");
            }
            else if (request.CalIsRecoveryLowPercent.HasValue && request.CalIsRecoveryHighPercent.HasValue &&
                request.CalIsRecoveryLowPercent.Value > request.CalIsRecoveryHighPercent.Value)
            {
                throw new InvalidOperationException("Internal standard recovery low percent must be less than or equal to high percent.");
            }

            if (!request.ReportedConcentrationBasis.HasValue)
                throw new InvalidOperationException("Reported concentration basis is required when equation type is CalibrationCurve.");
            if (request.ReportedConcentrationBasis != ReportedConcentrationBasis.SamplePpm)
                throw new InvalidOperationException("Only 'ppm in the sample' is supported: Syngistix applies weight, volume and dilution itself.");

            var maxAge = request.CalMaxRunAgeHours ?? 24;
            if (maxAge < 1)
                throw new InvalidOperationException("Maximum run age must be at least 1 hour when equation type is CalibrationCurve.");
        }
        else if (methodAbbr != null)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(methodAbbr, "^[A-Z0-9-]{1,20}$"))
                throw new InvalidOperationException("Method abbreviation must be 1-20 uppercase alphanumeric characters or hyphens.");
        }

        if (request.EquationType == EquationType.Measurement)
        {
            if (request.WorkflowType != WorkflowType.Measurement)
                throw new InvalidOperationException("Workflow type must be Measurement when equation type is Measurement.");

            if (!request.ReplicateCount.HasValue || request.ReplicateCount.Value < 1 || request.ReplicateCount.Value > 30)
                throw new InvalidOperationException("Replicate count must be between 1 and 30 when equation type is Measurement.");

            if (!request.EvaluationBasis.HasValue)
                throw new InvalidOperationException("Evaluation basis is required when equation type is Measurement.");
        }
        else if (request.WorkflowType == WorkflowType.Measurement)
        {
            if (request.EquationType != EquationType.Measurement)
                throw new InvalidOperationException("Equation type must be Measurement when workflow type is Measurement.");
        }

        if (request.ConditionFields != null && request.ConditionFields.Length > 500)
            throw new InvalidOperationException("Condition fields cannot exceed 500 characters.");

        if (request.EquationType is EquationType.GravimetricLoss or EquationType.GravimetricResidue)
        {
            if (request.WorkflowType != WorkflowType.Gravimetric)
                throw new InvalidOperationException($"Workflow type must be Gravimetric when equation type is {request.EquationType}.");

            if (!request.ReplicateCount.HasValue || request.ReplicateCount.Value < 1 || request.ReplicateCount.Value > 30)
                throw new InvalidOperationException($"Replicate count must be between 1 and 30 when equation type is {request.EquationType}.");
        }
        else if (request.WorkflowType == WorkflowType.Gravimetric)
        {
            if (request.EquationType is not (EquationType.GravimetricLoss or EquationType.GravimetricResidue))
                throw new InvalidOperationException("Equation type must be GravimetricLoss or GravimetricResidue when workflow type is Gravimetric.");

            if (!request.ReplicateCount.HasValue || request.ReplicateCount.Value < 1 || request.ReplicateCount.Value > 30)
                throw new InvalidOperationException($"Replicate count must be between 1 and 30 when equation type is {request.EquationType}.");
        }

        if (request.EquationType == EquationType.Qualitative)
        {
            if (request.WorkflowType != WorkflowType.Qualitative)
                throw new InvalidOperationException("Workflow type must be Qualitative when equation type is Qualitative.");
        }
        else if (request.WorkflowType == WorkflowType.Qualitative)
        {
            if (request.EquationType != EquationType.Qualitative)
                throw new InvalidOperationException("Equation type must be Qualitative when workflow type is Qualitative.");
        }

        if (request.EquationType == EquationType.Dissolution)
        {
            if (request.WorkflowType != WorkflowType.Dissolution)
                throw new InvalidOperationException("Workflow type must be Dissolution when equation type is Dissolution.");
        }
        else if (request.WorkflowType == WorkflowType.Dissolution)
        {
            if (request.EquationType != EquationType.Dissolution)
                throw new InvalidOperationException("Equation type must be Dissolution when workflow type is Dissolution.");
        }

        if (request.WorkflowType == WorkflowType.Dissolution)
        {
            if (!request.RequiresSystemSuitability)
                throw new InvalidOperationException("Dissolution tests must require system suitability: the standard comes from the linked suitability run.");

            decimal s1 = request.DissolutionS1Offset ?? 5m;
            decimal s2 = request.DissolutionS2MinOffset ?? 15m;
            decimal s3 = request.DissolutionS3MinOffset ?? 25m;
            decimal maxBelow = request.DissolutionS3MaxBelowS2Min ?? 2m;

            if (s1 < 0 || s2 < 0 || s3 < 0 || maxBelow < 0)
                throw new InvalidOperationException("Dissolution stage offsets must be greater than or equal to zero.");
        }

        if (request.EquationType == EquationType.Disintegration)
        {
            if (request.WorkflowType != WorkflowType.Disintegration)
                throw new InvalidOperationException("Workflow type must be Disintegration when equation type is Disintegration.");
        }
        else if (request.WorkflowType == WorkflowType.Disintegration)
        {
            if (request.EquationType != EquationType.Disintegration)
                throw new InvalidOperationException("Equation type must be Disintegration when workflow type is Disintegration.");
        }

        if (request.WorkflowType == WorkflowType.Disintegration)
        {
            if (request.RequiresSystemSuitability)
                throw new InvalidOperationException("Disintegration tests must not require system suitability.");

            int s1 = request.DisintegrationStage1Units ?? 6;
            int s2 = request.DisintegrationStage2Units ?? 12;
            int maxFail = request.DisintegrationMaxStage1Failures ?? 2;
            int minPass = request.DisintegrationMinPassTotal ?? 16;

            if (s1 < 1 || s2 < 1)
                throw new InvalidOperationException("Disintegration stage units must be greater than or equal to 1.");
            if (maxFail < 0 || maxFail >= s1)
                throw new InvalidOperationException($"Disintegration maximum Stage 1 failures must be between 0 and {s1 - 1}.");
            if (minPass < 1 || minPass > (s1 + s2))
                throw new InvalidOperationException($"Disintegration minimum pass total must be between 1 and {s1 + s2}.");
        }

        if (request.EquationType == EquationType.WeightVariation)
        {
            if (request.WorkflowType != WorkflowType.WeightVariation)
                throw new InvalidOperationException("Workflow type must be WeightVariation when equation type is WeightVariation.");
        }
        else if (request.WorkflowType == WorkflowType.WeightVariation)
        {
            if (request.EquationType != EquationType.WeightVariation)
                throw new InvalidOperationException("Equation type must be WeightVariation when workflow type is WeightVariation.");
        }

        if (request.WorkflowType == WorkflowType.WeightVariation)
        {
            if (request.RequiresSystemSuitability)
                throw new InvalidOperationException("Weight variation tests must not require system suitability.");

            int unitCount = request.WvUnitCount ?? 20;
            decimal band1Mg = request.WvTabletBand1MaxMg ?? 130m;
            decimal band1Pct = request.WvTabletBand1Percent ?? 10m;
            decimal band2Mg = request.WvTabletBand2MaxMg ?? 324m;
            decimal band2Pct = request.WvTabletBand2Percent ?? 7.5m;
            decimal band3Pct = request.WvTabletBand3Percent ?? 5m;
            int tabMaxOutside = request.WvTabletMaxOutside ?? 2;

            decimal capInnerPct = request.WvCapsuleInnerPercent ?? 10m;
            decimal capOuterPct = request.WvCapsuleOuterPercent ?? 25m;
            int capS1MaxOutside = request.WvCapsuleS1MaxOutside ?? 2;
            int capS1MaxRetest = request.WvCapsuleS1MaxForRetest ?? 6;
            int capS2Extra = request.WvCapsuleS2ExtraUnits ?? 40;
            int capS2MaxOutside = request.WvCapsuleS2MaxOutside ?? 6;

            if (unitCount < 1 || capS2Extra < 1)
                throw new InvalidOperationException("Weight variation unit count and extra units must be greater than or equal to 1.");
            if (tabMaxOutside < 0 || capS1MaxOutside < 0 || capS2MaxOutside < 0)
                throw new InvalidOperationException("Weight variation maximum outside counts must be greater than or equal to 0.");
            if (band1Mg <= 0m || band2Mg <= 0m)
                throw new InvalidOperationException("Weight variation tablet band weight limits must be greater than zero.");
            if (band1Pct <= 0m || band2Pct <= 0m || band3Pct <= 0m || capInnerPct <= 0m || capOuterPct <= 0m)
                throw new InvalidOperationException("Weight variation percentages must be greater than zero.");
            if (band1Mg >= band2Mg)
                throw new InvalidOperationException("Weight variation Tablet Band 1 Max Mg must be less than Band 2 Max Mg.");
            if (capInnerPct >= capOuterPct)
                throw new InvalidOperationException("Weight variation capsule inner percentage must be less than outer percentage.");
            if (capS1MaxOutside >= capS1MaxRetest || capS1MaxRetest > unitCount)
                throw new InvalidOperationException("Weight variation capsule Stage 1 max outside must be less than Stage 1 max for retest, which must be less than or equal to unit count.");
            if (capS2MaxOutside >= unitCount + capS2Extra)
                throw new InvalidOperationException($"Weight variation capsule Stage 2 max outside must be less than total units ({unitCount + capS2Extra}).");
        }

        if (request.EquationType == EquationType.HplcMultiAnalyte)
        {
            if (request.WorkflowType != WorkflowType.HplcMultiAnalyte)
                throw new InvalidOperationException("Workflow type must be HplcMultiAnalyte when equation type is HplcMultiAnalyte.");
        }
        else if (request.WorkflowType == WorkflowType.HplcMultiAnalyte)
        {
            if (request.EquationType != EquationType.HplcMultiAnalyte)
                throw new InvalidOperationException("Equation type must be HplcMultiAnalyte when workflow type is HplcMultiAnalyte.");
        }

        if (request.WorkflowType == WorkflowType.HplcMultiAnalyte)
        {
            if (!request.RequiresSystemSuitability)
                throw new InvalidOperationException("HPLC multi-analyte tests must require system suitability.");

            var preps = request.HplcPreparations ?? 2;
            if (preps < 1 || preps > 10)
                throw new InvalidOperationException("HPLC preparations must be between 1 and 10.");

            var injections = request.HplcInjectionsPerPreparation ?? 2;
            if (injections < 1 || injections > 10)
                throw new InvalidOperationException("HPLC injections per preparation must be between 1 and 10.");

            if (request.HplcMaxPreparationRsdPercent.HasValue && request.HplcMaxPreparationRsdPercent.Value <= 0m)
                throw new InvalidOperationException("HPLC maximum preparation RSD percent must be greater than zero.");
        }

        var entity = new TestDefinition
        {
            Code = request.Code,
            DisplayName = request.DisplayName,
            SectionId = sectionId,
            WorkflowType = request.WorkflowType,
            EquationType = request.EquationType,
            RequiresSystemSuitability = request.RequiresSystemSuitability,
            MethodAbbreviation = methodAbbr,
            SstMaxRsdPercent = request.SstMaxRsdPercent,
            SstMinResolution = request.SstMinResolution,
            SstMaxTailingFactor = request.SstMaxTailingFactor,
            SstMinTheoreticalPlates = request.SstMinTheoreticalPlates,
            CalibrationEntryMode = request.CalibrationEntryMode,
            CalMinCorrelation = request.CalMinCorrelation,
            CalCorrelationType = request.CalCorrelationType,
            CalMinStandards = request.CalMinStandards,
            CalCheckRecoveryLowPercent = request.CalCheckRecoveryLowPercent,
            CalCheckRecoveryHighPercent = request.CalCheckRecoveryHighPercent,
            CalBlankMax = request.CalBlankMax,
            CalIsRecoveryLowPercent = request.CalIsRecoveryLowPercent,
            CalIsRecoveryHighPercent = request.CalIsRecoveryHighPercent,
            CalRequireBlank = request.CalRequireBlank,
            CalRequireIcv = request.CalRequireIcv,
            CalRequireCcv = request.CalRequireCcv,
            CalRequireInternalStandard = request.CalRequireInternalStandard,
            ReportedConcentrationBasis = request.ReportedConcentrationBasis,
            CalMaxRunAgeHours = request.CalMaxRunAgeHours ?? 24,
            ReplicateCount = request.ReplicateCount,
            EvaluationBasis = request.EvaluationBasis,
            ConditionFields = request.ConditionFields,
            UsesTare = request.WorkflowType == WorkflowType.Gravimetric ? (request.UsesTare ?? false) : request.UsesTare,
            DissolutionS1Offset = request.WorkflowType == WorkflowType.Dissolution ? (request.DissolutionS1Offset ?? 5m) : request.DissolutionS1Offset,
            DissolutionS2MinOffset = request.WorkflowType == WorkflowType.Dissolution ? (request.DissolutionS2MinOffset ?? 15m) : request.DissolutionS2MinOffset,
            DissolutionS3MinOffset = request.WorkflowType == WorkflowType.Dissolution ? (request.DissolutionS3MinOffset ?? 25m) : request.DissolutionS3MinOffset,
            DissolutionS3MaxBelowS2Min = request.WorkflowType == WorkflowType.Dissolution ? (request.DissolutionS3MaxBelowS2Min ?? 2m) : request.DissolutionS3MaxBelowS2Min,
            DisintegrationStage1Units = request.WorkflowType == WorkflowType.Disintegration ? (request.DisintegrationStage1Units ?? 6) : request.DisintegrationStage1Units,
            DisintegrationStage2Units = request.WorkflowType == WorkflowType.Disintegration ? (request.DisintegrationStage2Units ?? 12) : request.DisintegrationStage2Units,
            DisintegrationMaxStage1Failures = request.WorkflowType == WorkflowType.Disintegration ? (request.DisintegrationMaxStage1Failures ?? 2) : request.DisintegrationMaxStage1Failures,
            DisintegrationMinPassTotal = request.WorkflowType == WorkflowType.Disintegration ? (request.DisintegrationMinPassTotal ?? 16) : request.DisintegrationMinPassTotal,
            WvUnitCount = request.WorkflowType == WorkflowType.WeightVariation ? (request.WvUnitCount ?? 20) : request.WvUnitCount,
            WvTabletBand1MaxMg = request.WorkflowType == WorkflowType.WeightVariation ? (request.WvTabletBand1MaxMg ?? 130m) : request.WvTabletBand1MaxMg,
            WvTabletBand1Percent = request.WorkflowType == WorkflowType.WeightVariation ? (request.WvTabletBand1Percent ?? 10m) : request.WvTabletBand1Percent,
            WvTabletBand2MaxMg = request.WorkflowType == WorkflowType.WeightVariation ? (request.WvTabletBand2MaxMg ?? 324m) : request.WvTabletBand2MaxMg,
            WvTabletBand2Percent = request.WorkflowType == WorkflowType.WeightVariation ? (request.WvTabletBand2Percent ?? 7.5m) : request.WvTabletBand2Percent,
            WvTabletBand3Percent = request.WorkflowType == WorkflowType.WeightVariation ? (request.WvTabletBand3Percent ?? 5m) : request.WvTabletBand3Percent,
            WvTabletMaxOutside = request.WorkflowType == WorkflowType.WeightVariation ? (request.WvTabletMaxOutside ?? 2) : request.WvTabletMaxOutside,
            WvCapsuleInnerPercent = request.WorkflowType == WorkflowType.WeightVariation ? (request.WvCapsuleInnerPercent ?? 10m) : request.WvCapsuleInnerPercent,
            WvCapsuleOuterPercent = request.WorkflowType == WorkflowType.WeightVariation ? (request.WvCapsuleOuterPercent ?? 25m) : request.WvCapsuleOuterPercent,
            WvCapsuleS1MaxOutside = request.WorkflowType == WorkflowType.WeightVariation ? (request.WvCapsuleS1MaxOutside ?? 2) : request.WvCapsuleS1MaxOutside,
            WvCapsuleS1MaxForRetest = request.WorkflowType == WorkflowType.WeightVariation ? (request.WvCapsuleS1MaxForRetest ?? 6) : request.WvCapsuleS1MaxForRetest,
            WvCapsuleS2ExtraUnits = request.WorkflowType == WorkflowType.WeightVariation ? (request.WvCapsuleS2ExtraUnits ?? 40) : request.WvCapsuleS2ExtraUnits,
            WvCapsuleS2MaxOutside = request.WorkflowType == WorkflowType.WeightVariation ? (request.WvCapsuleS2MaxOutside ?? 6) : request.WvCapsuleS2MaxOutside,
            HplcPreparations = request.WorkflowType == WorkflowType.HplcMultiAnalyte ? (request.HplcPreparations ?? 2) : 2,
            HplcInjectionsPerPreparation = request.WorkflowType == WorkflowType.HplcMultiAnalyte ? (request.HplcInjectionsPerPreparation ?? 2) : 2,
            HplcMaxPreparationRsdPercent = request.WorkflowType == WorkflowType.HplcMultiAnalyte ? request.HplcMaxPreparationRsdPercent : null
        };
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

        // Only a member of the test's section may change it, and only into a
        // section they belong to.
        var scope = await _scope.GetAccessibleSectionIdsAsync(CurrentUserId);
        if (scope is not null && !scope.Contains(entity.SectionId))
            throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");
        if (request.SectionId.HasValue && request.SectionId.Value != entity.SectionId)
            entity.SectionId = await _scope.ResolveSectionForCreateAsync(CurrentUserId, request.SectionId);

        var effectiveRequiresSst = request.RequiresSystemSuitability ?? entity.RequiresSystemSuitability;
        var effectiveEquationType = request.EquationType ?? entity.EquationType;
        var effectiveWorkflowType = request.WorkflowType ?? entity.WorkflowType;
        var effectiveMethodAbbr = request.MethodAbbreviation != null
            ? (string.IsNullOrWhiteSpace(request.MethodAbbreviation) ? null : request.MethodAbbreviation.Trim().ToUpperInvariant())
            : entity.MethodAbbreviation;
        var effectiveRsd = request.SstMaxRsdPercent ?? entity.SstMaxRsdPercent;
        var effectiveRes = request.SstMinResolution ?? entity.SstMinResolution;
        var effectiveTailing = request.SstMaxTailingFactor ?? entity.SstMaxTailingFactor;
        var effectivePlates = request.SstMinTheoreticalPlates ?? entity.SstMinTheoreticalPlates;

        var effectiveCalMinCorr = request.CalMinCorrelation ?? entity.CalMinCorrelation;
        var effectiveCalCorrType = request.CalCorrelationType ?? entity.CalCorrelationType;
        var effectiveCalMinStds = request.CalMinStandards ?? entity.CalMinStandards;
        var effectiveCalRecLow = request.CalCheckRecoveryLowPercent ?? entity.CalCheckRecoveryLowPercent;
        var effectiveCalRecHigh = request.CalCheckRecoveryHighPercent ?? entity.CalCheckRecoveryHighPercent;
        var effectiveCalIsLow = request.CalIsRecoveryLowPercent ?? entity.CalIsRecoveryLowPercent;
        var effectiveCalIsHigh = request.CalIsRecoveryHighPercent ?? entity.CalIsRecoveryHighPercent;
        var effectiveCalRequireIs = request.CalRequireInternalStandard ?? entity.CalRequireInternalStandard;
        var effectiveBasis = request.ReportedConcentrationBasis ?? entity.ReportedConcentrationBasis;
        var effectiveMaxAge = request.CalMaxRunAgeHours ?? entity.CalMaxRunAgeHours ?? 24;
        var effectiveReplicateCount = request.ReplicateCount ?? entity.ReplicateCount;
        var effectiveEvaluationBasis = request.EvaluationBasis ?? entity.EvaluationBasis;
        var effectiveConditionFields = request.ConditionFields ?? entity.ConditionFields;
        var effectiveUsesTare = request.UsesTare ?? entity.UsesTare;

        if (effectiveConditionFields != null && effectiveConditionFields.Length > 500)
            throw new InvalidOperationException("Condition fields cannot exceed 500 characters.");

        if ((effectiveWorkflowType == WorkflowType.Disintegration || effectiveEquationType == EquationType.Disintegration) && effectiveRequiresSst)
            throw new InvalidOperationException("Disintegration tests must not require system suitability.");

        if ((effectiveWorkflowType == WorkflowType.WeightVariation || effectiveEquationType == EquationType.WeightVariation) && effectiveRequiresSst)
            throw new InvalidOperationException("Weight variation tests must not require system suitability.");

        if (effectiveRequiresSst)
        {
            if (string.IsNullOrEmpty(effectiveMethodAbbr))
                throw new InvalidOperationException("Method abbreviation is required when system suitability is enabled.");

            if (!System.Text.RegularExpressions.Regex.IsMatch(effectiveMethodAbbr, "^[A-Z0-9-]{1,20}$"))
                throw new InvalidOperationException("Method abbreviation must be 1-20 uppercase alphanumeric characters or hyphens.");

            if (effectiveEquationType != EquationType.HplcMultiAnalyte && effectiveWorkflowType != WorkflowType.HplcMultiAnalyte &&
                !effectiveRsd.HasValue && !effectiveRes.HasValue && !effectiveTailing.HasValue && !effectivePlates.HasValue)
            {
                throw new InvalidOperationException("At least one system suitability criterion is required when system suitability is enabled.");
            }
        }
        else if (effectiveEquationType == EquationType.CalibrationCurve)
        {
            if (effectiveWorkflowType != WorkflowType.ElementalAssay)
                throw new InvalidOperationException("Workflow type must be ElementalAssay when equation type is CalibrationCurve.");

            if (string.IsNullOrEmpty(effectiveMethodAbbr))
                throw new InvalidOperationException("Method abbreviation is required when equation type is CalibrationCurve.");

            if (!System.Text.RegularExpressions.Regex.IsMatch(effectiveMethodAbbr, "^[A-Z0-9-]{1,20}$"))
                throw new InvalidOperationException("Method abbreviation must be 1-20 uppercase alphanumeric characters or hyphens.");

            if (!effectiveCalMinCorr.HasValue || effectiveCalMinCorr.Value <= 0m || effectiveCalMinCorr.Value > 1m)
                throw new InvalidOperationException("Minimum correlation must be in (0, 1] when equation type is CalibrationCurve.");

            if (!effectiveCalCorrType.HasValue)
                throw new InvalidOperationException("Correlation type is required when equation type is CalibrationCurve.");

            if (!effectiveCalMinStds.HasValue || effectiveCalMinStds.Value < 1)
                throw new InvalidOperationException("Minimum standards must be at least 1 when equation type is CalibrationCurve.");

            if (!effectiveCalRecLow.HasValue || !effectiveCalRecHigh.HasValue)
                throw new InvalidOperationException("Both check recovery window bounds (low and high) are required when equation type is CalibrationCurve.");

            if (effectiveCalRecLow.Value > effectiveCalRecHigh.Value)
                throw new InvalidOperationException("Check recovery low percent must be less than or equal to high percent.");

            if (effectiveCalRequireIs == true)
            {
                if (!effectiveCalIsLow.HasValue || !effectiveCalIsHigh.HasValue)
                    throw new InvalidOperationException("Both internal standard recovery bounds (low and high) are required when internal standards are required.");

                if (effectiveCalIsLow.Value > effectiveCalIsHigh.Value)
                    throw new InvalidOperationException("Internal standard recovery low percent must be less than or equal to high percent.");
            }
            else if (effectiveCalIsLow.HasValue && effectiveCalIsHigh.HasValue && effectiveCalIsLow.Value > effectiveCalIsHigh.Value)
            {
                throw new InvalidOperationException("Internal standard recovery low percent must be less than or equal to high percent.");
            }

            if (!effectiveBasis.HasValue)
                throw new InvalidOperationException("Reported concentration basis is required when equation type is CalibrationCurve.");
            if (effectiveBasis != ReportedConcentrationBasis.SamplePpm)
                throw new InvalidOperationException("Only 'ppm in the sample' is supported: Syngistix applies weight, volume and dilution itself.");

            if (effectiveMaxAge < 1)
                throw new InvalidOperationException("Maximum run age must be at least 1 hour when equation type is CalibrationCurve.");
        }
        else if (effectiveMethodAbbr != null)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(effectiveMethodAbbr, "^[A-Z0-9-]{1,20}$"))
                throw new InvalidOperationException("Method abbreviation must be 1-20 uppercase alphanumeric characters or hyphens.");
        }

        if (effectiveEquationType == EquationType.Measurement)
        {
            if (effectiveWorkflowType != WorkflowType.Measurement)
                throw new InvalidOperationException("Workflow type must be Measurement when equation type is Measurement.");

            if (!effectiveReplicateCount.HasValue || effectiveReplicateCount.Value < 1 || effectiveReplicateCount.Value > 30)
                throw new InvalidOperationException("Replicate count must be between 1 and 30 when equation type is Measurement.");

            if (!effectiveEvaluationBasis.HasValue)
                throw new InvalidOperationException("Evaluation basis is required when equation type is Measurement.");
        }
        else if (effectiveWorkflowType == WorkflowType.Measurement)
        {
            if (effectiveEquationType != EquationType.Measurement)
                throw new InvalidOperationException("Equation type must be Measurement when workflow type is Measurement.");
        }

        if (effectiveEquationType is EquationType.GravimetricLoss or EquationType.GravimetricResidue)
        {
            if (effectiveWorkflowType != WorkflowType.Gravimetric)
                throw new InvalidOperationException($"Workflow type must be Gravimetric when equation type is {effectiveEquationType}.");

            if (!effectiveReplicateCount.HasValue || effectiveReplicateCount.Value < 1 || effectiveReplicateCount.Value > 30)
                throw new InvalidOperationException($"Replicate count must be between 1 and 30 when equation type is {effectiveEquationType}.");
        }
        else if (effectiveWorkflowType == WorkflowType.Gravimetric)
        {
            if (effectiveEquationType is not (EquationType.GravimetricLoss or EquationType.GravimetricResidue))
                throw new InvalidOperationException("Equation type must be GravimetricLoss or GravimetricResidue when workflow type is Gravimetric.");

            if (!effectiveReplicateCount.HasValue || effectiveReplicateCount.Value < 1 || effectiveReplicateCount.Value > 30)
                throw new InvalidOperationException($"Replicate count must be between 1 and 30 when equation type is {effectiveEquationType}.");
        }

        if (effectiveEquationType == EquationType.Qualitative)
        {
            if (effectiveWorkflowType != WorkflowType.Qualitative)
                throw new InvalidOperationException("Workflow type must be Qualitative when equation type is Qualitative.");
        }
        else if (effectiveWorkflowType == WorkflowType.Qualitative)
        {
            if (effectiveEquationType != EquationType.Qualitative)
                throw new InvalidOperationException("Equation type must be Qualitative when workflow type is Qualitative.");
        }

        if (effectiveEquationType == EquationType.Dissolution)
        {
            if (effectiveWorkflowType != WorkflowType.Dissolution)
                throw new InvalidOperationException("Workflow type must be Dissolution when equation type is Dissolution.");
        }
        else if (effectiveWorkflowType == WorkflowType.Dissolution)
        {
            if (effectiveEquationType != EquationType.Dissolution)
                throw new InvalidOperationException("Equation type must be Dissolution when workflow type is Dissolution.");
        }

        if (effectiveWorkflowType == WorkflowType.Dissolution)
        {
            if (!effectiveRequiresSst)
                throw new InvalidOperationException("Dissolution tests must require system suitability: the standard comes from the linked suitability run.");

            var effectiveS1 = request.DissolutionS1Offset ?? entity.DissolutionS1Offset;
            var effectiveS2 = request.DissolutionS2MinOffset ?? entity.DissolutionS2MinOffset;
            var effectiveS3 = request.DissolutionS3MinOffset ?? entity.DissolutionS3MinOffset;
            var effectiveMaxBelow = request.DissolutionS3MaxBelowS2Min ?? entity.DissolutionS3MaxBelowS2Min;

            if (!effectiveS1.HasValue || !effectiveS2.HasValue || !effectiveS3.HasValue || !effectiveMaxBelow.HasValue)
                throw new InvalidOperationException("Dissolution stage offsets are required for Dissolution tests.");

            if (effectiveS1.Value < 0 || effectiveS2.Value < 0 || effectiveS3.Value < 0 || effectiveMaxBelow.Value < 0)
                throw new InvalidOperationException("Dissolution stage offsets must be greater than or equal to zero.");
        }

        if (effectiveEquationType == EquationType.Disintegration)
        {
            if (effectiveWorkflowType != WorkflowType.Disintegration)
                throw new InvalidOperationException("Workflow type must be Disintegration when equation type is Disintegration.");
        }
        else if (effectiveWorkflowType == WorkflowType.Disintegration)
        {
            if (effectiveEquationType != EquationType.Disintegration)
                throw new InvalidOperationException("Equation type must be Disintegration when workflow type is Disintegration.");
        }

        if (effectiveWorkflowType == WorkflowType.Disintegration)
        {
            if (effectiveRequiresSst)
                throw new InvalidOperationException("Disintegration tests must not require system suitability.");

            var effectiveS1 = request.DisintegrationStage1Units ?? entity.DisintegrationStage1Units ?? 6;
            var effectiveS2 = request.DisintegrationStage2Units ?? entity.DisintegrationStage2Units ?? 12;
            var effectiveMaxF1 = request.DisintegrationMaxStage1Failures ?? entity.DisintegrationMaxStage1Failures ?? 2;
            var effectiveMinPass = request.DisintegrationMinPassTotal ?? entity.DisintegrationMinPassTotal ?? 16;

            if (effectiveS1 < 1 || effectiveS2 < 1)
                throw new InvalidOperationException("Disintegration stage units must be greater than or equal to 1.");
            if (effectiveMaxF1 < 0 || effectiveMaxF1 >= effectiveS1)
                throw new InvalidOperationException($"Disintegration maximum Stage 1 failures must be between 0 and {effectiveS1 - 1}.");
            if (effectiveMinPass < 1 || effectiveMinPass > effectiveS1 + effectiveS2)
                throw new InvalidOperationException($"Disintegration minimum pass total must be between 1 and {effectiveS1 + effectiveS2}.");
        }

        if (effectiveEquationType == EquationType.WeightVariation)
        {
            if (effectiveWorkflowType != WorkflowType.WeightVariation)
                throw new InvalidOperationException("Workflow type must be WeightVariation when equation type is WeightVariation.");
        }
        else if (effectiveWorkflowType == WorkflowType.WeightVariation)
        {
            if (effectiveEquationType != EquationType.WeightVariation)
                throw new InvalidOperationException("Equation type must be WeightVariation when workflow type is WeightVariation.");
        }

        if (effectiveWorkflowType == WorkflowType.WeightVariation)
        {
            if (effectiveRequiresSst)
                throw new InvalidOperationException("Weight variation tests must not require system suitability.");

            var effectiveUnitCount = request.WvUnitCount ?? entity.WvUnitCount ?? 20;
            var effectiveBand1Mg = request.WvTabletBand1MaxMg ?? entity.WvTabletBand1MaxMg ?? 130m;
            var effectiveBand1Pct = request.WvTabletBand1Percent ?? entity.WvTabletBand1Percent ?? 10m;
            var effectiveBand2Mg = request.WvTabletBand2MaxMg ?? entity.WvTabletBand2MaxMg ?? 324m;
            var effectiveBand2Pct = request.WvTabletBand2Percent ?? entity.WvTabletBand2Percent ?? 7.5m;
            var effectiveBand3Pct = request.WvTabletBand3Percent ?? entity.WvTabletBand3Percent ?? 5m;
            var effectiveTabMaxOutside = request.WvTabletMaxOutside ?? entity.WvTabletMaxOutside ?? 2;

            var effectiveCapInnerPct = request.WvCapsuleInnerPercent ?? entity.WvCapsuleInnerPercent ?? 10m;
            var effectiveCapOuterPct = request.WvCapsuleOuterPercent ?? entity.WvCapsuleOuterPercent ?? 25m;
            var effectiveCapS1MaxOutside = request.WvCapsuleS1MaxOutside ?? entity.WvCapsuleS1MaxOutside ?? 2;
            var effectiveCapS1MaxRetest = request.WvCapsuleS1MaxForRetest ?? entity.WvCapsuleS1MaxForRetest ?? 6;
            var effectiveCapS2Extra = request.WvCapsuleS2ExtraUnits ?? entity.WvCapsuleS2ExtraUnits ?? 40;
            var effectiveCapS2MaxOutside = request.WvCapsuleS2MaxOutside ?? entity.WvCapsuleS2MaxOutside ?? 6;

            if (effectiveUnitCount < 1 || effectiveCapS2Extra < 1)
                throw new InvalidOperationException("Weight variation unit count and extra units must be greater than or equal to 1.");
            if (effectiveTabMaxOutside < 0 || effectiveCapS1MaxOutside < 0 || effectiveCapS2MaxOutside < 0)
                throw new InvalidOperationException("Weight variation maximum outside counts must be greater than or equal to 0.");
            if (effectiveBand1Mg <= 0m || effectiveBand2Mg <= 0m)
                throw new InvalidOperationException("Weight variation tablet band weight limits must be greater than zero.");
            if (effectiveBand1Pct <= 0m || effectiveBand2Pct <= 0m || effectiveBand3Pct <= 0m || effectiveCapInnerPct <= 0m || effectiveCapOuterPct <= 0m)
                throw new InvalidOperationException("Weight variation percentages must be greater than zero.");
            if (effectiveBand1Mg >= effectiveBand2Mg)
                throw new InvalidOperationException("Weight variation Tablet Band 1 Max Mg must be less than Band 2 Max Mg.");
            if (effectiveCapInnerPct >= effectiveCapOuterPct)
                throw new InvalidOperationException("Weight variation capsule inner percentage must be less than outer percentage.");
            if (effectiveCapS1MaxOutside >= effectiveCapS1MaxRetest || effectiveCapS1MaxRetest > effectiveUnitCount)
                throw new InvalidOperationException("Weight variation capsule Stage 1 max outside must be less than Stage 1 max for retest, which must be less than or equal to unit count.");
            if (effectiveCapS2MaxOutside >= effectiveUnitCount + effectiveCapS2Extra)
                throw new InvalidOperationException($"Weight variation capsule Stage 2 max outside must be less than total units ({effectiveUnitCount + effectiveCapS2Extra}).");
        }

        if (effectiveEquationType == EquationType.HplcMultiAnalyte)
        {
            if (effectiveWorkflowType != WorkflowType.HplcMultiAnalyte)
                throw new InvalidOperationException("Workflow type must be HplcMultiAnalyte when equation type is HplcMultiAnalyte.");
        }
        else if (effectiveWorkflowType == WorkflowType.HplcMultiAnalyte)
        {
            if (effectiveEquationType != EquationType.HplcMultiAnalyte)
                throw new InvalidOperationException("Equation type must be HplcMultiAnalyte when workflow type is HplcMultiAnalyte.");
        }

        if (effectiveWorkflowType == WorkflowType.HplcMultiAnalyte)
        {
            if (!effectiveRequiresSst)
                throw new InvalidOperationException("HPLC multi-analyte tests must require system suitability.");

            var effectivePreps = request.HplcPreparations ?? entity.HplcPreparations;
            if (effectivePreps < 1 || effectivePreps > 10)
                throw new InvalidOperationException("HPLC preparations must be between 1 and 10.");

            var effectiveInjections = request.HplcInjectionsPerPreparation ?? entity.HplcInjectionsPerPreparation;
            if (effectiveInjections < 1 || effectiveInjections > 10)
                throw new InvalidOperationException("HPLC injections per preparation must be between 1 and 10.");

            var effectiveRsdPercent = request.HplcMaxPreparationRsdPercent.HasValue
                ? request.HplcMaxPreparationRsdPercent
                : entity.HplcMaxPreparationRsdPercent;
            if (effectiveRsdPercent.HasValue && effectiveRsdPercent.Value <= 0m)
                throw new InvalidOperationException("HPLC maximum preparation RSD percent must be greater than zero.");
        }

        entity.Code = request.Code;
        entity.DisplayName = request.DisplayName;
        if (request.WorkflowType.HasValue) entity.WorkflowType = request.WorkflowType.Value;
        if (request.EquationType.HasValue) entity.EquationType = request.EquationType.Value;
        if (request.RequiresSystemSuitability.HasValue) entity.RequiresSystemSuitability = request.RequiresSystemSuitability.Value;
        if (request.MethodAbbreviation != null) entity.MethodAbbreviation = effectiveMethodAbbr;
        if (request.SstMaxRsdPercent.HasValue) entity.SstMaxRsdPercent = request.SstMaxRsdPercent;
        if (request.SstMinResolution.HasValue) entity.SstMinResolution = request.SstMinResolution;
        if (request.SstMaxTailingFactor.HasValue) entity.SstMaxTailingFactor = request.SstMaxTailingFactor;
        if (request.SstMinTheoreticalPlates.HasValue) entity.SstMinTheoreticalPlates = request.SstMinTheoreticalPlates;
        if (request.CalibrationEntryMode.HasValue) entity.CalibrationEntryMode = request.CalibrationEntryMode.Value;
        if (request.CalMinCorrelation.HasValue) entity.CalMinCorrelation = request.CalMinCorrelation;
        if (request.CalCorrelationType.HasValue) entity.CalCorrelationType = request.CalCorrelationType;
        if (request.CalMinStandards.HasValue) entity.CalMinStandards = request.CalMinStandards;
        if (request.CalCheckRecoveryLowPercent.HasValue) entity.CalCheckRecoveryLowPercent = request.CalCheckRecoveryLowPercent;
        if (request.CalCheckRecoveryHighPercent.HasValue) entity.CalCheckRecoveryHighPercent = request.CalCheckRecoveryHighPercent;
        if (request.CalBlankMax.HasValue) entity.CalBlankMax = request.CalBlankMax;
        if (request.CalIsRecoveryLowPercent.HasValue) entity.CalIsRecoveryLowPercent = request.CalIsRecoveryLowPercent;
        if (request.CalIsRecoveryHighPercent.HasValue) entity.CalIsRecoveryHighPercent = request.CalIsRecoveryHighPercent;
        if (request.CalRequireBlank.HasValue) entity.CalRequireBlank = request.CalRequireBlank;
        if (request.CalRequireIcv.HasValue) entity.CalRequireIcv = request.CalRequireIcv;
        if (request.CalRequireCcv.HasValue) entity.CalRequireCcv = request.CalRequireCcv;
        if (request.CalRequireInternalStandard.HasValue) entity.CalRequireInternalStandard = request.CalRequireInternalStandard;
        if (request.ReportedConcentrationBasis.HasValue) entity.ReportedConcentrationBasis = request.ReportedConcentrationBasis;
        if (request.CalMaxRunAgeHours.HasValue) entity.CalMaxRunAgeHours = request.CalMaxRunAgeHours;
        if (request.ReplicateCount.HasValue) entity.ReplicateCount = request.ReplicateCount.Value;
        if (request.EvaluationBasis.HasValue) entity.EvaluationBasis = request.EvaluationBasis.Value;
        if (request.ConditionFields != null) entity.ConditionFields = request.ConditionFields;
        if (request.UsesTare.HasValue) entity.UsesTare = request.UsesTare.Value;
        else if (effectiveWorkflowType == WorkflowType.Gravimetric && !entity.UsesTare.HasValue) entity.UsesTare = false;
        if (request.DissolutionS1Offset.HasValue) entity.DissolutionS1Offset = request.DissolutionS1Offset.Value;
        else if (effectiveWorkflowType == WorkflowType.Dissolution && !entity.DissolutionS1Offset.HasValue) entity.DissolutionS1Offset = 5m;
        if (request.DissolutionS2MinOffset.HasValue) entity.DissolutionS2MinOffset = request.DissolutionS2MinOffset.Value;
        else if (effectiveWorkflowType == WorkflowType.Dissolution && !entity.DissolutionS2MinOffset.HasValue) entity.DissolutionS2MinOffset = 15m;
        if (request.DissolutionS3MinOffset.HasValue) entity.DissolutionS3MinOffset = request.DissolutionS3MinOffset.Value;
        else if (effectiveWorkflowType == WorkflowType.Dissolution && !entity.DissolutionS3MinOffset.HasValue) entity.DissolutionS3MinOffset = 25m;
        if (request.DissolutionS3MaxBelowS2Min.HasValue) entity.DissolutionS3MaxBelowS2Min = request.DissolutionS3MaxBelowS2Min.Value;
        else if (effectiveWorkflowType == WorkflowType.Dissolution && !entity.DissolutionS3MaxBelowS2Min.HasValue) entity.DissolutionS3MaxBelowS2Min = 2m;
        if (request.DisintegrationStage1Units.HasValue) entity.DisintegrationStage1Units = request.DisintegrationStage1Units.Value;
        else if (effectiveWorkflowType == WorkflowType.Disintegration && !entity.DisintegrationStage1Units.HasValue) entity.DisintegrationStage1Units = 6;
        if (request.DisintegrationStage2Units.HasValue) entity.DisintegrationStage2Units = request.DisintegrationStage2Units.Value;
        else if (effectiveWorkflowType == WorkflowType.Disintegration && !entity.DisintegrationStage2Units.HasValue) entity.DisintegrationStage2Units = 12;
        if (request.DisintegrationMaxStage1Failures.HasValue) entity.DisintegrationMaxStage1Failures = request.DisintegrationMaxStage1Failures.Value;
        else if (effectiveWorkflowType == WorkflowType.Disintegration && !entity.DisintegrationMaxStage1Failures.HasValue) entity.DisintegrationMaxStage1Failures = 2;
        if (request.DisintegrationMinPassTotal.HasValue) entity.DisintegrationMinPassTotal = request.DisintegrationMinPassTotal.Value;
        else if (effectiveWorkflowType == WorkflowType.Disintegration && !entity.DisintegrationMinPassTotal.HasValue) entity.DisintegrationMinPassTotal = 16;
        if (request.WvUnitCount.HasValue) entity.WvUnitCount = request.WvUnitCount.Value;
        else if (effectiveWorkflowType == WorkflowType.WeightVariation && !entity.WvUnitCount.HasValue) entity.WvUnitCount = 20;
        if (request.WvTabletBand1MaxMg.HasValue) entity.WvTabletBand1MaxMg = request.WvTabletBand1MaxMg.Value;
        else if (effectiveWorkflowType == WorkflowType.WeightVariation && !entity.WvTabletBand1MaxMg.HasValue) entity.WvTabletBand1MaxMg = 130m;
        if (request.WvTabletBand1Percent.HasValue) entity.WvTabletBand1Percent = request.WvTabletBand1Percent.Value;
        else if (effectiveWorkflowType == WorkflowType.WeightVariation && !entity.WvTabletBand1Percent.HasValue) entity.WvTabletBand1Percent = 10m;
        if (request.WvTabletBand2MaxMg.HasValue) entity.WvTabletBand2MaxMg = request.WvTabletBand2MaxMg.Value;
        else if (effectiveWorkflowType == WorkflowType.WeightVariation && !entity.WvTabletBand2MaxMg.HasValue) entity.WvTabletBand2MaxMg = 324m;
        if (request.WvTabletBand2Percent.HasValue) entity.WvTabletBand2Percent = request.WvTabletBand2Percent.Value;
        else if (effectiveWorkflowType == WorkflowType.WeightVariation && !entity.WvTabletBand2Percent.HasValue) entity.WvTabletBand2Percent = 7.5m;
        if (request.WvTabletBand3Percent.HasValue) entity.WvTabletBand3Percent = request.WvTabletBand3Percent.Value;
        else if (effectiveWorkflowType == WorkflowType.WeightVariation && !entity.WvTabletBand3Percent.HasValue) entity.WvTabletBand3Percent = 5m;
        if (request.WvTabletMaxOutside.HasValue) entity.WvTabletMaxOutside = request.WvTabletMaxOutside.Value;
        else if (effectiveWorkflowType == WorkflowType.WeightVariation && !entity.WvTabletMaxOutside.HasValue) entity.WvTabletMaxOutside = 2;
        if (request.WvCapsuleInnerPercent.HasValue) entity.WvCapsuleInnerPercent = request.WvCapsuleInnerPercent.Value;
        else if (effectiveWorkflowType == WorkflowType.WeightVariation && !entity.WvCapsuleInnerPercent.HasValue) entity.WvCapsuleInnerPercent = 10m;
        if (request.WvCapsuleOuterPercent.HasValue) entity.WvCapsuleOuterPercent = request.WvCapsuleOuterPercent.Value;
        else if (effectiveWorkflowType == WorkflowType.WeightVariation && !entity.WvCapsuleOuterPercent.HasValue) entity.WvCapsuleOuterPercent = 25m;
        if (request.WvCapsuleS1MaxOutside.HasValue) entity.WvCapsuleS1MaxOutside = request.WvCapsuleS1MaxOutside.Value;
        else if (effectiveWorkflowType == WorkflowType.WeightVariation && !entity.WvCapsuleS1MaxOutside.HasValue) entity.WvCapsuleS1MaxOutside = 2;
        if (request.WvCapsuleS1MaxForRetest.HasValue) entity.WvCapsuleS1MaxForRetest = request.WvCapsuleS1MaxForRetest.Value;
        else if (effectiveWorkflowType == WorkflowType.WeightVariation && !entity.WvCapsuleS1MaxForRetest.HasValue) entity.WvCapsuleS1MaxForRetest = 6;
        if (request.WvCapsuleS2ExtraUnits.HasValue) entity.WvCapsuleS2ExtraUnits = request.WvCapsuleS2ExtraUnits.Value;
        else if (effectiveWorkflowType == WorkflowType.WeightVariation && !entity.WvCapsuleS2ExtraUnits.HasValue) entity.WvCapsuleS2ExtraUnits = 40;
        if (request.WvCapsuleS2MaxOutside.HasValue) entity.WvCapsuleS2MaxOutside = request.WvCapsuleS2MaxOutside.Value;
        else if (effectiveWorkflowType == WorkflowType.WeightVariation && !entity.WvCapsuleS2MaxOutside.HasValue) entity.WvCapsuleS2MaxOutside = 6;
        if (request.HplcPreparations.HasValue) entity.HplcPreparations = request.HplcPreparations.Value;
        else if (effectiveWorkflowType == WorkflowType.HplcMultiAnalyte && entity.HplcPreparations == 0) entity.HplcPreparations = 2;
        if (request.HplcInjectionsPerPreparation.HasValue) entity.HplcInjectionsPerPreparation = request.HplcInjectionsPerPreparation.Value;
        else if (effectiveWorkflowType == WorkflowType.HplcMultiAnalyte && entity.HplcInjectionsPerPreparation == 0) entity.HplcInjectionsPerPreparation = 2;
        if (request.HplcMaxPreparationRsdPercent.HasValue) entity.HplcMaxPreparationRsdPercent = request.HplcMaxPreparationRsdPercent.Value;


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
                    m.MediaIncubationConditionId
                }),
                s.RequiresIncubationTransfer,
                incubationStages = s.IncubationStages.OrderBy(x => x.StageNumber).Select(x => new
                {
                    x.StageNumber, x.TempMin, x.TempMax, x.IncubationMinHours, x.IncubationMaxHours
                })
            })
            .ToListAsync()));

    // TempMin/Max and IncubationMinHours/MaxHours are copied from the chosen
    // MediaIncubationCondition at save time - single source of truth,
    // conditions are locked once used.
    private async Task<List<TestWorkflowStepMedia>> BuildStepMediaAsync(List<StepMediaRequest> requests)
    {
        var materialIds = requests.Select(m => m.MaterialId).Distinct().ToList();
        var materials = await _db.Materials.Where(m => materialIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id);

        var conditionIds = requests.Where(m => m.MediaIncubationConditionId.HasValue)
            .Select(m => m.MediaIncubationConditionId!.Value)
            .Distinct()
            .ToList();
        var conditions = await _db.MediaIncubationConditions.Where(c => conditionIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id);

        var result = new List<TestWorkflowStepMedia>();
        foreach (var m in requests)
        {
            if (!materials.TryGetValue(m.MaterialId, out var material))
                throw new InvalidOperationException($"Material {m.MaterialId} not found.");

            if (!m.MediaIncubationConditionId.HasValue)
                throw new InvalidOperationException($"Choose an incubation condition for medium '{material.MaterialName}'.");

            if (material.MediaProductId is null)
                throw new InvalidOperationException($"'{material.MaterialName}' isn't linked to a media product. Link it in Inventory > Materials Stock first.");

            if (!conditions.TryGetValue(m.MediaIncubationConditionId.Value, out var condition))
                throw new InvalidOperationException($"Incubation condition {m.MediaIncubationConditionId.Value} not found.");

            if (condition.MediaProductId != material.MediaProductId.Value)
                throw new InvalidOperationException($"The incubation condition chosen for '{material.MaterialName}' belongs to a different media product.");

            var entity = new TestWorkflowStepMedia
            {
                MaterialId = m.MaterialId,
                IsRequired = m.IsRequired,
                DisplayOrder = m.DisplayOrder,
                MediaIncubationConditionId = condition.Id,
                TempMin = condition.TemperatureMin,
                TempMax = condition.TemperatureMax,
                IncubationMinHours = condition.IncubationMinHours,
                IncubationMaxHours = condition.IncubationMaxHours
            };
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
        var test = await _db.TestDefinitions.FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new InvalidOperationException($"Test {id} not found.");
        if (test.WorkflowType == WorkflowType.HplcAssay)
            throw new InvalidOperationException("HPLC assay tests have no workflow steps.");
        if (AnalysisWorkflows.UsesTestAnalysis(test.WorkflowType))
            throw new InvalidOperationException($"{test.WorkflowType} tests have no workflow steps.");

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

    // ---- Test Analytes (ICP-OES / Calibration Curve) ----
    [HttpGet("test-definitions/{id:int}/analytes")]
    public async Task<IActionResult> GetTestAnalytes(int id)
    {
        var test = await _db.TestDefinitions.FindAsync(id)
            ?? throw new InvalidOperationException($"Test {id} not found.");

        var analytes = await _db.TestAnalytes
            .Where(a => a.TestDefinitionId == id)
            .OrderBy(a => a.DisplayOrder)
            .ThenBy(a => a.Id)
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(analytes.Select(TestAnalyteDto.From)));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("test-definitions/{id:int}/analytes")]
    public async Task<IActionResult> CreateTestAnalyte(int id, CreateTestAnalyteRequest request)
    {
        var test = await _db.TestDefinitions.FindAsync(id)
            ?? throw new InvalidOperationException($"Test {id} not found.");

        var scope = await _scope.GetAccessibleSectionIdsAsync(CurrentUserId);
        if (scope is not null && !scope.Contains(test.SectionId))
            throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");

        var isHplcMulti = test.WorkflowType == WorkflowType.HplcMultiAnalyte || test.EquationType == EquationType.HplcMultiAnalyte;

        if (string.IsNullOrWhiteSpace(request.Element))
            throw new InvalidOperationException("Element is required.");

        var element = request.Element.Trim();
        if (element.Length > 20)
            throw new InvalidOperationException(isHplcMulti ? "Analyte name cannot exceed 20 characters." : "Element symbol cannot exceed 20 characters.");

        if (request.WavelengthNm <= 0)
            throw new InvalidOperationException("Wavelength must be greater than 0.");

        if (isHplcMulti)
        {
            if (request.View.HasValue)
                throw new InvalidOperationException("View is not allowed for HPLC multi-analyte tests.");

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
            if (!request.View.HasValue)
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
            View = isHplcMulti ? null : request.View,
            LoqMgPerL = request.LoqMgPerL,
            DisplayOrder = request.DisplayOrder,
            SstMaxRsdPercent = isHplcMulti ? request.SstMaxRsdPercent : null,
            SstMinResolution = isHplcMulti ? request.SstMinResolution : null,
            SstMaxTailingFactor = isHplcMulti ? request.SstMaxTailingFactor : null,
            SstMinTheoreticalPlates = isHplcMulti ? request.SstMinTheoreticalPlates : null,
            IsActive = true
        };

        _db.TestAnalytes.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(TestAnalyteDto.From(entity)));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("test-definitions/{id:int}/analytes/{analyteId:int}")]
    public async Task<IActionResult> UpdateTestAnalyte(int id, int analyteId, UpdateTestAnalyteRequest request)
    {
        var test = await _db.TestDefinitions.FindAsync(id)
            ?? throw new InvalidOperationException($"Test {id} not found.");

        var scope = await _scope.GetAccessibleSectionIdsAsync(CurrentUserId);
        if (scope is not null && !scope.Contains(test.SectionId))
            throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");

        var analyte = await _db.TestAnalytes.FirstOrDefaultAsync(a => a.Id == analyteId && a.TestDefinitionId == id)
            ?? throw new InvalidOperationException($"Analyte {analyteId} not found for test {id}.");

        var isHplcMulti = test.WorkflowType == WorkflowType.HplcMultiAnalyte || test.EquationType == EquationType.HplcMultiAnalyte;

        var effectiveElement = request.Element != null ? request.Element.Trim() : analyte.Element;
        var effectiveWavelength = request.WavelengthNm ?? analyte.WavelengthNm;

        if (string.IsNullOrWhiteSpace(effectiveElement))
            throw new InvalidOperationException("Element is required.");
        if (effectiveElement.Length > 20)
            throw new InvalidOperationException(isHplcMulti ? "Analyte name cannot exceed 20 characters." : "Element symbol cannot exceed 20 characters.");
        if (effectiveWavelength <= 0)
            throw new InvalidOperationException("Wavelength must be greater than 0.");

        if (isHplcMulti)
        {
            if (request.View.HasValue)
                throw new InvalidOperationException("View is not allowed for HPLC multi-analyte tests.");

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
        return Ok(ApiResponse<object>.Ok(TestAnalyteDto.From(analyte)));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("test-definitions/{id:int}/analytes/{analyteId:int}")]
    public async Task<IActionResult> DeleteTestAnalyte(int id, int analyteId)
    {
        var test = await _db.TestDefinitions.FindAsync(id)
            ?? throw new InvalidOperationException($"Test {id} not found.");

        var scope = await _scope.GetAccessibleSectionIdsAsync(CurrentUserId);
        if (scope is not null && !scope.Contains(test.SectionId))
            throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");

        var analyte = await _db.TestAnalytes.FirstOrDefaultAsync(a => a.Id == analyteId && a.TestDefinitionId == id)
            ?? throw new InvalidOperationException($"Analyte {analyteId} not found for test {id}.");

        var inUseCalibration = await _db.CalibrationRunAnalytes.AnyAsync(r => r.TestAnalyteId == analyteId);
        var inUseSuitability = await _db.SystemSuitabilityRunAnalytes.AnyAsync(r => r.TestAnalyteId == analyteId);
        if (inUseCalibration || inUseSuitability)
        {
            analyte.IsActive = false;
            await _db.SaveChangesAsync();
            var runType = inUseCalibration && inUseSuitability ? "calibration and suitability runs"
                : inUseCalibration ? "calibration runs"
                : "suitability runs";
            return Ok(ApiResponse<object>.Ok(new
            {
                message = $"Analyte {analyte.Element} ({analyte.WavelengthNm} nm) is referenced by {runType} and has been deactivated instead of deleted.",
                deactivated = true
            }));
        }

        _db.TestAnalytes.Remove(analyte);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new
        {
            message = $"Analyte {analyte.Element} ({analyte.WavelengthNm} nm) deleted successfully.",
            deleted = true
        }));
    }

    // ---- Test Definition Stage Replicates (FP Standard-Comparison Assay -
    // per-ProductionStageRole standard/sample replicate counts) ----
    [HttpGet("test-definitions/{id:int}/stage-replicates")]
    public async Task<IActionResult> GetTestDefinitionStageReplicates(int id)
    {
        var test = await _db.TestDefinitions.FindAsync(id)
            ?? throw new InvalidOperationException($"Test {id} not found.");

        var replicates = await _db.TestDefinitionStageReplicates
            .Where(r => r.TestDefinitionId == id)
            .OrderBy(r => r.Role)
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(replicates.Select(TestDefinitionStageReplicateDto.From)));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("test-definitions/{id:int}/stage-replicates")]
    public async Task<IActionResult> CreateTestDefinitionStageReplicate(int id, CreateTestDefinitionStageReplicateRequest request)
    {
        var test = await _db.TestDefinitions.FindAsync(id)
            ?? throw new InvalidOperationException($"Test {id} not found.");

        var scope = await _scope.GetAccessibleSectionIdsAsync(CurrentUserId);
        if (scope is not null && !scope.Contains(test.SectionId))
            throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");

        if (request.StandardReplicates < 1)
            throw new InvalidOperationException("Standard replicates must be at least 1.");
        if (request.SampleReplicates < 1)
            throw new InvalidOperationException("Sample replicates must be at least 1.");

        if (await _db.TestDefinitionStageReplicates.AnyAsync(r => r.TestDefinitionId == id && r.Role == request.Role))
            throw new InvalidOperationException($"Stage replicate configuration for role {request.Role} already exists for this test definition.");

        var entity = new TestDefinitionStageReplicate
        {
            TestDefinitionId = id,
            Role = request.Role,
            StandardReplicates = request.StandardReplicates,
            SampleReplicates = request.SampleReplicates
        };

        _db.TestDefinitionStageReplicates.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(TestDefinitionStageReplicateDto.From(entity)));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("test-definitions/{id:int}/stage-replicates/{replicateId:int}")]
    public async Task<IActionResult> UpdateTestDefinitionStageReplicate(int id, int replicateId, UpdateTestDefinitionStageReplicateRequest request)
    {
        var test = await _db.TestDefinitions.FindAsync(id)
            ?? throw new InvalidOperationException($"Test {id} not found.");

        var scope = await _scope.GetAccessibleSectionIdsAsync(CurrentUserId);
        if (scope is not null && !scope.Contains(test.SectionId))
            throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");

        var replicate = await _db.TestDefinitionStageReplicates.FirstOrDefaultAsync(r => r.Id == replicateId && r.TestDefinitionId == id)
            ?? throw new InvalidOperationException($"Stage replicate {replicateId} not found for test {id}.");

        if (request.StandardReplicates.HasValue)
        {
            if (request.StandardReplicates.Value < 1)
                throw new InvalidOperationException("Standard replicates must be at least 1.");
            replicate.StandardReplicates = request.StandardReplicates.Value;
        }
        if (request.SampleReplicates.HasValue)
        {
            if (request.SampleReplicates.Value < 1)
                throw new InvalidOperationException("Sample replicates must be at least 1.");
            replicate.SampleReplicates = request.SampleReplicates.Value;
        }

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(TestDefinitionStageReplicateDto.From(replicate)));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("test-definitions/{id:int}/stage-replicates/{replicateId:int}")]
    public async Task<IActionResult> DeleteTestDefinitionStageReplicate(int id, int replicateId)
    {
        var test = await _db.TestDefinitions.FindAsync(id)
            ?? throw new InvalidOperationException($"Test {id} not found.");

        var scope = await _scope.GetAccessibleSectionIdsAsync(CurrentUserId);
        if (scope is not null && !scope.Contains(test.SectionId))
            throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");

        var replicate = await _db.TestDefinitionStageReplicates.FirstOrDefaultAsync(r => r.Id == replicateId && r.TestDefinitionId == id)
            ?? throw new InvalidOperationException($"Stage replicate {replicateId} not found for test {id}.");

        _db.TestDefinitionStageReplicates.Remove(replicate);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new
        {
            message = "Stage replicate configuration deleted.",
            deleted = true
        }));
    }
}
