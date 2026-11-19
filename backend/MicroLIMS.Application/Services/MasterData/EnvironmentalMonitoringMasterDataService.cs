using System.Globalization;
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
using MicroLIMS.Shared.Messages;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Services.MasterData;

// Master data: environmental monitoring. Behind EnvironmentalMonitoringMasterDataController
// (api/masterdata/...); the controller only maps HTTP to these calls.
public class EnvironmentalMonitoringMasterDataService
{
    private readonly IMicroLimsDbContext _db;
    private readonly MasterDataBatchService _batch;

    public EnvironmentalMonitoringMasterDataService(IMicroLimsDbContext db, MasterDataBatchService? batchService = null)
    {
        _db = db;
        _batch = batchService ?? new MasterDataBatchService(db);
    }

    public async Task<object> GetDepartmentsAsync()
    {
        // Shaped to avoid the Department.Rooms <-> Room.Department
        // navigation cycle EF's relationship fixup creates when both
        // sides are tracked in the same query - same pattern as GetMachines.
        var departments = await _db.Departments
            .Select(d => new { d.Id, d.Version, d.Name, d.Class, d.TestingFrequency, d.IsActive, d.FrequencyType, d.FrequencyInterval, d.SamplingWeekday, d.FrequencyAnchorMonth, d.FrequencyWeekOfMonth, Rooms = d.Rooms.Select(r => new { r.Id, r.Version, r.Name, r.DepartmentId, r.GradeClassification, r.IsActive, r.Kind, r.ParentRoomId, r.FrequencyType, r.FrequencyInterval, r.SamplingWeekday, r.FrequencyAnchorMonth, r.FrequencyWeekOfMonth }) })
            .ToListAsync();
        return departments;
    }

    public async Task<EmDepartmentResponse> CreateDepartmentAsync(CreateDepartmentRequest request)
    {
        var dept = new Department { Name = request.Name, Class = request.Class, TestingFrequency = request.TestingFrequency };
        ApplyFrequency(dept, request.Frequency);
        _db.Departments.Add(dept);
        await _db.SaveChangesAsync();
        return EmDepartmentResponse.From(dept);
    }

    public async Task<EmDepartmentResponse> UpdateDepartmentAsync(int id, UpdateDepartmentRequest request)
    {
        var dept = await _db.Departments.FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new NotFoundException(
                MessageCodes.MonitoringDepartmentNotFound,
                $"Department {id} not found.",
                new Dictionary<string, string> { ["departmentId"] = id.ToString(CultureInfo.InvariantCulture) });
        RecordVersion.EnsureCurrent(_db, dept);
        if (request.IsActive.HasValue) dept.IsActive = request.IsActive.Value;
        dept.Name = request.Name;
        dept.Class = request.Class;
        dept.TestingFrequency = request.TestingFrequency;
        ApplyFrequency(dept, request.Frequency);
        await _db.SaveChangesAsync();
        return EmDepartmentResponse.From(dept);
    }

    // Blocked (not a raw FK error) if this department still has Rooms -
    // same "guard with a clear message" pattern as ItemService.DeleteAsync.
    public async Task<object> DeleteDepartmentAsync(int id)
    {
        var dept = await _db.Departments.FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new NotFoundException(
                MessageCodes.MonitoringDepartmentNotFound,
                $"Department {id} not found.",
                new Dictionary<string, string> { ["departmentId"] = id.ToString(CultureInfo.InvariantCulture) });

        var roomCount = await _db.Rooms.CountAsync(r => r.DepartmentId == id);
        if (roomCount > 0)
            throw new BusinessRuleException(
                MessageCodes.MonitoringRoomDeleteHasRooms,
                $"Cannot delete '{dept.Name}' - it still has {roomCount} room(s). Delete those rooms first.",
                new Dictionary<string, string> { ["name"] = dept.Name, ["roomCount"] = roomCount.ToString(CultureInfo.InvariantCulture) });

        _db.Departments.Remove(dept);
        await _db.SaveChangesAsync();
        return new { };
    }

    private static void ApplyFrequency(IHasFrequency target, FrequencyRuleDto? rule)
    {
        if (rule is not null && FrequencyRules.Validate(rule) is { } problem)
            throw new BusinessRuleException(
                MessageCodes.MonitoringFrequencyValidationFailed,
                problem,
                new Dictionary<string, string> { ["message"] = problem });
        FrequencyRules.Apply(target, rule);
    }

    // A Room has no parent; an LAF must sit in a Room of the same department.
    private async Task ApplyKindAsync(Room room, RoomKind kind, int? parentRoomId)
    {
        if (kind == RoomKind.Room)
        {
            room.Kind = RoomKind.Room;
            room.ParentRoomId = null;
            return;
        }

        if (parentRoomId is null)
            throw new BusinessRuleException(
                MessageCodes.MonitoringLafBelongsToRoom,
                "An LAF must belong to a room.");
        if (parentRoomId == room.Id)
            throw new BusinessRuleException(
                MessageCodes.MonitoringLafCannotBeOwnParent,
                "An LAF cannot be its own parent.");
        if (room.Id != 0 && await _db.Rooms.AnyAsync(r => r.ParentRoomId == room.Id))
            throw new BusinessRuleException(
                MessageCodes.MonitoringRoomCannotMakeLaf,
                $"Cannot make '{room.Name}' an LAF - it already contains LAFs.",
                new Dictionary<string, string> { ["name"] = room.Name });

        var parent = await _db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == parentRoomId)
            ?? throw new NotFoundException(
                MessageCodes.MonitoringRoomNotFound,
                $"Room {parentRoomId} not found.",
                new Dictionary<string, string> { ["id"] = parentRoomId.Value.ToString(CultureInfo.InvariantCulture) });
        if (parent.Kind != RoomKind.Room)
            throw new BusinessRuleException(
                MessageCodes.MonitoringLafMustSitInRoom,
                "An LAF must sit in a room, not in another LAF.");
        if (parent.DepartmentId != room.DepartmentId)
            throw new BusinessRuleException(
                MessageCodes.MonitoringLafMustBeInSameDepartment,
                "An LAF must be in the same department as its room.");

        room.Kind = RoomKind.LAF;
        room.ParentRoomId = parentRoomId;
    }

    public async Task<object> GetRoomsAsync()
    {
        // Shaped to avoid the Department.Rooms <-> Room.Department
        // navigation cycle EF's relationship fixup creates when both
        // sides are tracked in the same query (raw entities would crash
        // JSON serialization here).
        var rooms = await _db.Rooms
            .Select(r => new { r.Id, r.Version, r.Name, r.DepartmentId, r.GradeClassification, r.IsActive, r.Kind, r.ParentRoomId, r.FrequencyType, r.FrequencyInterval, r.SamplingWeekday, r.FrequencyAnchorMonth, r.FrequencyWeekOfMonth, Department = r.Department == null ? null : new { r.Department.Id, r.Department.Name } })
            .ToListAsync();
        return rooms;
    }

    public async Task<RoomResponse> CreateRoomAsync(CreateRoomRequest request)
    {
        await EnsureRoomNameUniqueInDepartmentAsync(request.DepartmentId, request.Name);
        var room = new Room { Name = request.Name, DepartmentId = request.DepartmentId, GradeClassification = request.GradeClassification };
        await ApplyKindAsync(room, request.Kind, request.ParentRoomId);
        ApplyFrequency(room, request.Frequency);
        _db.Rooms.Add(room);
        await _db.SaveChangesAsync();
        return RoomResponse.From(room);
    }

    public Task<IReadOnlyList<RoomResponse>> CreateRoomsBatchAsync(
        IReadOnlyList<CreateRoomRequest>? requests,
        CancellationToken ct = default) =>
        _batch.ExecuteAsync(
            requests,
            ValidateCreateRoomRowAsync,
            CreateRoomAsync,
            validateBatchAsync: ValidateRoomsBatchUniquenessAsync,
            ct: ct);

    private async Task<IReadOnlyList<FieldError>> ValidateCreateRoomRowAsync(
        CreateRoomRequest request,
        int row,
        CancellationToken ct)
    {
        var errors = new List<FieldError>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add(new FieldError("name", "Room name is required.", row));
        }
        else if (request.Name.Trim().Length > 150)
        {
            errors.Add(new FieldError("name", "Room name must be at most 150 characters.", row));
        }

        if (request.GradeClassification is null)
        {
            errors.Add(new FieldError("gradeClassification", "Grade classification is required.", row));
        }

        if (request.DepartmentId <= 0 || !await _db.Departments.AsNoTracking().AnyAsync(d => d.Id == request.DepartmentId, ct))
        {
            errors.Add(new FieldError("departmentId", $"Department {request.DepartmentId} not found.", row));
        }

        if (!Enum.IsDefined(request.Kind))
        {
            errors.Add(new FieldError("kind", $"Unknown room kind '{request.Kind}'.", row));
        }
        else if (request.Kind == RoomKind.LAF)
        {
            if (request.ParentRoomId is null)
            {
                errors.Add(new FieldError("parentRoomId", "An LAF must belong to a room.", row));
            }
            else
            {
                var parent = await _db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == request.ParentRoomId.Value, ct);
                if (parent is null)
                {
                    errors.Add(new FieldError("parentRoomId", $"Room {request.ParentRoomId.Value} not found.", row));
                }
                else if (parent.Kind != RoomKind.Room)
                {
                    errors.Add(new FieldError("parentRoomId", "An LAF must sit in a room, not in another LAF.", row));
                }
                else if (parent.DepartmentId != request.DepartmentId)
                {
                    errors.Add(new FieldError("parentRoomId", "An LAF must be in the same department as its room.", row));
                }
            }
        }

        if (request.Frequency is not null && FrequencyRules.Validate(request.Frequency) is { } problem)
        {
            errors.Add(new FieldError("frequency", problem, row));
        }

        return errors;
    }

    private async Task<IReadOnlyList<FieldError>> ValidateRoomsBatchUniquenessAsync(
        IReadOnlyList<CreateRoomRequest> rows,
        CancellationToken ct)
    {
        var errors = new List<FieldError>();
        var byKey = new Dictionary<(int DepartmentId, string Name), List<int>>(RoomKeyComparer.Instance);

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row is null || string.IsNullOrWhiteSpace(row.Name)) continue;

            var key = (row.DepartmentId, row.Name.Trim());
            if (!byKey.TryGetValue(key, out var indices))
            {
                indices = new List<int>();
                byKey[key] = indices;
            }
            indices.Add(i);
        }

        foreach (var ((_, name), indices) in byKey)
        {
            if (indices.Count <= 1) continue;
            foreach (var rowIndex in indices)
            {
                errors.Add(new FieldError("name", $"Duplicate room '{name}' in batch.", rowIndex));
            }
        }

        if (byKey.Count > 0)
        {
            var existingSet = await FindExistingRoomKeysAsync(byKey.Keys, ct: ct);

            foreach (var (key, indices) in byKey)
            {
                if (!existingSet.Contains(key)) continue;
                foreach (var rowIndex in indices)
                {
                    errors.Add(new FieldError("name", DuplicateRoomMessage(key.Name), rowIndex));
                }
            }
        }

        return errors;
    }

    private static string DuplicateRoomMessage(string trimmedName) =>
        $"Room '{trimmedName}' already exists.";

    private async Task EnsureRoomNameUniqueInDepartmentAsync(
        int departmentId,
        string? name,
        int? excludeRoomId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        var trimmedName = name.Trim();
        var existing = await FindExistingRoomKeysAsync([(departmentId, trimmedName)], excludeRoomId, ct);
        if (existing.Contains((departmentId, trimmedName)))
            throw new BusinessRuleException(
                MessageCodes.MonitoringRoomAlreadyExists,
                $"Room '{trimmedName}' already exists.",
                new Dictionary<string, string> { ["name"] = trimmedName });
    }

    private async Task<HashSet<(int DepartmentId, string Name)>> FindExistingRoomKeysAsync(
        IEnumerable<(int DepartmentId, string Name)> keys,
        int? excludeRoomId = null,
        CancellationToken ct = default)
    {
        var keyList = keys
            .Where(k => !string.IsNullOrWhiteSpace(k.Name))
            .Select(k => (k.DepartmentId, Name: k.Name.Trim()))
            .Distinct(RoomKeyComparer.Instance)
            .ToList();

        if (keyList.Count == 0)
            return new HashSet<(int DepartmentId, string Name)>(RoomKeyComparer.Instance);

        var deptIds = keyList.Select(k => k.DepartmentId).Distinct().ToList();
        var loweredNames = keyList.Select(k => k.Name.ToLowerInvariant()).Distinct().ToList();

        var query = _db.Rooms
            .AsNoTracking()
            .Where(r => deptIds.Contains(r.DepartmentId) && loweredNames.Contains(r.Name.Trim().ToLower()));

        if (excludeRoomId.HasValue)
            query = query.Where(r => r.Id != excludeRoomId.Value);

        var existingRows = await query
            .Select(r => new { r.DepartmentId, r.Name })
            .ToListAsync(ct);

        return new HashSet<(int DepartmentId, string Name)>(
            existingRows.Select(r => (r.DepartmentId, r.Name.Trim())),
            RoomKeyComparer.Instance);
    }

    public async Task<RoomResponse> UpdateRoomAsync(int id, UpdateRoomRequest request)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new NotFoundException(
                MessageCodes.MonitoringRoomNotFound,
                $"Room {id} not found.",
                new Dictionary<string, string> { ["id"] = id.ToString(CultureInfo.InvariantCulture) });
        RecordVersion.EnsureCurrent(_db, room);

        var nameOrDeptChanged = room.DepartmentId != request.DepartmentId
            || !string.Equals(room.Name.Trim(), request.Name.Trim(), StringComparison.OrdinalIgnoreCase);
        if (nameOrDeptChanged)
            await EnsureRoomNameUniqueInDepartmentAsync(request.DepartmentId, request.Name, excludeRoomId: id);

        if (request.IsActive.HasValue) room.IsActive = request.IsActive.Value;
        room.Name = request.Name;
        room.DepartmentId = request.DepartmentId;
        room.GradeClassification = request.GradeClassification;
        await ApplyKindAsync(room, request.Kind ?? room.Kind, request.Kind is null && request.ParentRoomId is null ? room.ParentRoomId : request.ParentRoomId);
        ApplyFrequency(room, request.Frequency);
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
            ?? throw new NotFoundException(
                MessageCodes.MonitoringRoomNotFound,
                $"Room {id} not found.",
                new Dictionary<string, string> { ["id"] = id.ToString(CultureInfo.InvariantCulture) });

        var configCount = await _db.RoomTestConfigurations.CountAsync(c => c.RoomId == id);
        if (configCount > 0)
            throw new BusinessRuleException(
                MessageCodes.MonitoringRoomDeleteHasConfigs,
                $"Cannot delete '{room.Name}' - it still has {configCount} test configuration(s). Delete those first.",
                new Dictionary<string, string> { ["name"] = room.Name, ["configCount"] = configCount.ToString(CultureInfo.InvariantCulture) });

        var monitoringCount = await _db.RoomMonitorings.CountAsync(m => m.RoomId == id);
        if (monitoringCount > 0)
            throw new BusinessRuleException(
                MessageCodes.MonitoringRoomDeleteHasMonitorings,
                $"Cannot delete '{room.Name}' - it has {monitoringCount} monitoring record(s) in its history.",
                new Dictionary<string, string> { ["name"] = room.Name, ["monitoringCount"] = monitoringCount.ToString(CultureInfo.InvariantCulture) });

        _db.Rooms.Remove(room);
        await _db.SaveChangesAsync();
        return new { };
    }

    public async Task<List<RoomTestConfigurationResponse>> GetRoomTestConfigurationsAsync(int roomId) =>
        (await _db.RoomTestConfigurations.AsNoTracking().Where(c => c.RoomId == roomId).ToListAsync()).Select(RoomTestConfigurationResponse.From).ToList();

    // Unknown types would have no unit at result entry (DeriveBatchLocationUnit
    // throws), and the configured-unit types report the unit entered here.
    private static void ValidateRoomTestConfiguration(string testType, string? unit)
    {
        if (!RoomTestTypes.IsKnown(testType))
            throw new BusinessRuleException(
                MessageCodes.MonitoringRoomTestConfigUnknownTestType,
                $"Unknown test type '{testType}'. Allowed: {string.Join(", ", RoomTestTypes.All)}.",
                new Dictionary<string, string>
                {
                    ["testType"] = testType,
                    ["allowed"] = string.Join(", ", RoomTestTypes.All)
                });
        if (RoomTestTypes.UsesConfiguredUnit(testType) && string.IsNullOrWhiteSpace(unit))
            throw new BusinessRuleException(
                MessageCodes.MonitoringRoomTestConfigUnitRequired,
                $"Unit is required for {testType} test configurations.",
                new Dictionary<string, string> { ["testType"] = testType });
    }

    public async Task<RoomTestConfigurationResponse> CreateRoomTestConfigurationAsync(CreateRoomTestConfigRequest request)
    {
        ValidateRoomTestConfiguration(request.TestType, request.Unit);
        await EnsureRoomTestConfigUniqueAsync(request.RoomId, request.TestType, request.TestCode);
        var entity = new RoomTestConfiguration
        {
            RoomId = request.RoomId, TestType = request.TestType, TestCode = request.TestCode,
            AlertLimit = request.AlertLimit, ActionLimit = request.ActionLimit, SpecLimit = request.SpecLimit,
            Unit = request.Unit?.Trim() ?? string.Empty
        };
        _db.RoomTestConfigurations.Add(entity);
        await _db.SaveChangesAsync();
        return RoomTestConfigurationResponse.From(entity);
    }

    public Task<IReadOnlyList<RoomTestConfigurationResponse>> CreateRoomTestConfigurationsBatchAsync(
        IReadOnlyList<CreateRoomTestConfigRequest>? requests,
        CancellationToken ct = default) =>
        _batch.ExecuteAsync(
            requests,
            ValidateCreateRoomTestConfigRowAsync,
            CreateRoomTestConfigurationAsync,
            validateBatchAsync: ValidateRoomTestConfigsBatchUniquenessAsync,
            ct: ct);

    private async Task<IReadOnlyList<FieldError>> ValidateCreateRoomTestConfigRowAsync(
        CreateRoomTestConfigRequest request,
        int row,
        CancellationToken ct)
    {
        var errors = new List<FieldError>();

        if (request.RoomId <= 0 || !await _db.Rooms.AsNoTracking().AnyAsync(r => r.Id == request.RoomId, ct))
        {
            errors.Add(new FieldError("roomId", $"Room {request.RoomId} not found.", row));
        }

        if (string.IsNullOrWhiteSpace(request.TestType))
        {
            errors.Add(new FieldError("testType", "Test type is required.", row));
        }
        else if (!RoomTestTypes.IsKnown(request.TestType))
        {
            errors.Add(new FieldError(
                "testType",
                $"Unknown test type '{request.TestType}'. Allowed: {string.Join(", ", RoomTestTypes.All)}.",
                row));
        }

        if (string.IsNullOrWhiteSpace(request.TestCode))
        {
            errors.Add(new FieldError("testCode", "Test code is required.", row));
        }

        if (RoomTestTypes.UsesConfiguredUnit(request.TestType) && string.IsNullOrWhiteSpace(request.Unit))
        {
            errors.Add(new FieldError("unit", $"Unit is required for {request.TestType} test configurations.", row));
        }

        if (request.AlertLimit is null)
        {
            errors.Add(new FieldError("alertLimit", "Alert limit is required.", row));
        }

        if (request.ActionLimit is null)
        {
            errors.Add(new FieldError("actionLimit", "Action limit is required.", row));
        }

        if (request.SpecLimit is null)
        {
            errors.Add(new FieldError("specLimit", "Spec limit is required.", row));
        }

        return errors;
    }

    private async Task<IReadOnlyList<FieldError>> ValidateRoomTestConfigsBatchUniquenessAsync(
        IReadOnlyList<CreateRoomTestConfigRequest> rows,
        CancellationToken ct)
    {
        var errors = new List<FieldError>();
        var byKey = new Dictionary<(int RoomId, string TestType, string TestCode), List<int>>(RoomTestConfigKeyComparer.Instance);

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row is null || string.IsNullOrWhiteSpace(row.TestType) || string.IsNullOrWhiteSpace(row.TestCode)) continue;

            var key = (row.RoomId, row.TestType.Trim(), row.TestCode.Trim());
            if (!byKey.TryGetValue(key, out var indices))
            {
                indices = new List<int>();
                byKey[key] = indices;
            }
            indices.Add(i);
        }

        foreach (var ((roomId, testType, testCode), indices) in byKey)
        {
            if (indices.Count <= 1) continue;
            foreach (var rowIndex in indices)
            {
                errors.Add(new FieldError(
                    "testCode",
                    $"Duplicate test configuration '{testCode}' ({testType}) for room {roomId} in batch.",
                    rowIndex));
            }
        }

        if (byKey.Count > 0)
        {
            var existingSet = await FindExistingActiveRoomTestConfigKeysAsync(byKey.Keys, ct: ct);

            foreach (var (key, indices) in byKey)
            {
                if (!existingSet.Contains(key)) continue;
                foreach (var rowIndex in indices)
                {
                    errors.Add(new FieldError(
                        "testCode",
                        DuplicateRoomTestConfigMessage(key.RoomId, key.TestType, key.TestCode),
                        rowIndex));
                }
            }
        }

        return errors;
    }

    private static string DuplicateRoomTestConfigMessage(int roomId, string trimmedTestType, string trimmedTestCode) =>
        $"Test configuration '{trimmedTestCode}' ({trimmedTestType}) already exists for room {roomId}.";

    private async Task EnsureRoomTestConfigUniqueAsync(
        int roomId,
        string? testType,
        string? testCode,
        int? excludeConfigId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(testType) || string.IsNullOrWhiteSpace(testCode))
            return;

        var trimmedType = testType.Trim();
        var trimmedCode = testCode.Trim();
        var existing = await FindExistingActiveRoomTestConfigKeysAsync([(roomId, trimmedType, trimmedCode)], excludeConfigId, ct);
        if (existing.Contains((roomId, trimmedType, trimmedCode)))
            throw new BusinessRuleException(
                MessageCodes.MonitoringRoomTestConfigAlreadyExists,
                $"Test configuration '{trimmedCode}' ({trimmedType}) already exists for room {roomId}.",
                new Dictionary<string, string> { ["testCode"] = trimmedCode, ["testType"] = trimmedType, ["roomId"] = roomId.ToString(CultureInfo.InvariantCulture) });
    }

    private async Task<HashSet<(int RoomId, string TestType, string TestCode)>> FindExistingActiveRoomTestConfigKeysAsync(
        IEnumerable<(int RoomId, string TestType, string TestCode)> keys,
        int? excludeConfigId = null,
        CancellationToken ct = default)
    {
        var keyList = keys
            .Where(k => !string.IsNullOrWhiteSpace(k.TestType) && !string.IsNullOrWhiteSpace(k.TestCode))
            .Select(k => (k.RoomId, TestType: k.TestType.Trim(), TestCode: k.TestCode.Trim()))
            .Distinct(RoomTestConfigKeyComparer.Instance)
            .ToList();

        if (keyList.Count == 0)
            return new HashSet<(int RoomId, string TestType, string TestCode)>(RoomTestConfigKeyComparer.Instance);

        var roomIds = keyList.Select(k => k.RoomId).Distinct().ToList();
        var loweredTypes = keyList.Select(k => k.TestType.ToLowerInvariant()).Distinct().ToList();
        var loweredCodes = keyList.Select(k => k.TestCode.ToLowerInvariant()).Distinct().ToList();

        var query = _db.RoomTestConfigurations
            .AsNoTracking()
            // An inactive row is history: replacing it with a new active row is allowed.
            .Where(c => c.IsActive
                && roomIds.Contains(c.RoomId)
                && loweredTypes.Contains(c.TestType.Trim().ToLower())
                && loweredCodes.Contains(c.TestCode.Trim().ToLower()));

        if (excludeConfigId.HasValue)
            query = query.Where(c => c.Id != excludeConfigId.Value);

        var existingRows = await query
            .Select(c => new { c.RoomId, c.TestType, c.TestCode })
            .ToListAsync(ct);

        return new HashSet<(int RoomId, string TestType, string TestCode)>(
            existingRows.Select(c => (c.RoomId, c.TestType.Trim(), c.TestCode.Trim())),
            RoomTestConfigKeyComparer.Instance);
    }

    private sealed class RoomKeyComparer : IEqualityComparer<(int DepartmentId, string Name)>
    {
        public static readonly RoomKeyComparer Instance = new();

        public bool Equals((int DepartmentId, string Name) x, (int DepartmentId, string Name) y) =>
            x.DepartmentId == y.DepartmentId && string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((int DepartmentId, string Name) obj) =>
            HashCode.Combine(obj.DepartmentId, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name));
    }

    private sealed class RoomTestConfigKeyComparer : IEqualityComparer<(int RoomId, string TestType, string TestCode)>
    {
        public static readonly RoomTestConfigKeyComparer Instance = new();

        public bool Equals((int RoomId, string TestType, string TestCode) x, (int RoomId, string TestType, string TestCode) y) =>
            x.RoomId == y.RoomId
            && string.Equals(x.TestType, y.TestType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.TestCode, y.TestCode, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((int RoomId, string TestType, string TestCode) obj) =>
            HashCode.Combine(
                obj.RoomId,
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.TestType),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.TestCode));
    }

    public async Task<RoomTestConfigurationResponse> UpdateRoomTestConfigurationAsync(int id, UpdateRoomTestConfigRequest request)
    {
        var entity = await _db.RoomTestConfigurations.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException(
                MessageCodes.MonitoringRoomTestConfigurationNotFound,
                $"Room test configuration {id} not found.",
                new Dictionary<string, string> { ["id"] = id.ToString(CultureInfo.InvariantCulture) });
        RecordVersion.EnsureCurrent(_db, entity);
        ValidateRoomTestConfiguration(request.TestType, request.Unit);

        var wasActive = entity.IsActive;
        var willBeActive = request.IsActive ?? entity.IsActive;
        var typeOrCodeChanged =
            !string.Equals(entity.TestType.Trim(), request.TestType.Trim(), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(entity.TestCode.Trim(), request.TestCode.Trim(), StringComparison.OrdinalIgnoreCase);

        if (willBeActive && (!wasActive || typeOrCodeChanged))
            await EnsureRoomTestConfigUniqueAsync(entity.RoomId, request.TestType, request.TestCode, excludeConfigId: id);

        if (request.IsActive.HasValue) entity.IsActive = request.IsActive.Value;
        entity.TestType = request.TestType;
        entity.TestCode = request.TestCode;
        entity.AlertLimit = request.AlertLimit;
        entity.ActionLimit = request.ActionLimit;
        entity.SpecLimit = request.SpecLimit;
        entity.Unit = request.Unit?.Trim() ?? string.Empty;
        await _db.SaveChangesAsync();
        return RoomTestConfigurationResponse.From(entity);
    }

    // Blocked once any sample location has been recorded against this
    // configuration, so results keep their link to the limits they used.
    public async Task<object> DeleteRoomTestConfigurationAsync(int id)
    {
        var entity = await _db.RoomTestConfigurations.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException(
                MessageCodes.MonitoringRoomTestConfigurationNotFound,
                $"Room test configuration {id} not found.",
                new Dictionary<string, string> { ["id"] = id.ToString(CultureInfo.InvariantCulture) });

        var locationCount = await _db.SampleLocations.CountAsync(l => l.RoomTestConfigurationId == id);
        if (locationCount > 0)
            throw new BusinessRuleException(
                MessageCodes.MonitoringRoomTestConfigDeleteReferenced,
                $"Cannot delete this room test configuration - it is referenced by {locationCount} sample location(s).",
                new Dictionary<string, string> { ["locationCount"] = locationCount.ToString(CultureInfo.InvariantCulture) });

        _db.RoomTestConfigurations.Remove(entity);
        await _db.SaveChangesAsync();
        return new { };
    }
}
