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

// Master data: after cleaning. Behind AfterCleaningMasterDataController
// (api/masterdata/...); the controller only maps HTTP to these calls.
public class AfterCleaningMasterDataService
{
    private readonly IMicroLimsDbContext _db;

    public AfterCleaningMasterDataService(IMicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<object> GetMachinesAsync()
    {
        // Shaped to avoid the Machine.Parts <-> MachinePart.Machine
        // navigation cycle EF's relationship fixup creates when both
        // sides are tracked in the same query.
        var machines = await _db.Machines
            .Select(m => new { m.Id, m.Version, m.Name, Parts = m.Parts.Select(p => new { p.Id, p.Version, p.Name, p.MachineId }) })
            .ToListAsync();
        return machines;
    }

    public async Task<MachineResponse> CreateMachineAsync(CreateMachineRequest request)
    {
        var machine = new Machine { Name = request.Name };
        _db.Machines.Add(machine);
        await _db.SaveChangesAsync();
        return MachineResponse.From(machine);
    }

    public async Task<MachineResponse> UpdateMachineAsync(int id, UpdateMachineRequest request)
    {
        var machine = await _db.Machines.FirstOrDefaultAsync(m => m.Id == id)
            ?? throw new NotFoundException($"Machine {id} not found.");
        RecordVersion.EnsureCurrent(_db, machine);
        machine.Name = request.Name;
        await _db.SaveChangesAsync();
        return MachineResponse.From(machine);
    }

    // Blocked if this machine still has parts, or any Sample has ever
    // referenced it directly (Sample.MachineId) - same "guard with a
    // clear message" pattern as ItemService.DeleteAsync.
    public async Task<object> DeleteMachineAsync(int id)
    {
        var machine = await _db.Machines.FirstOrDefaultAsync(m => m.Id == id)
            ?? throw new NotFoundException($"Machine {id} not found.");

        var partCount = await _db.MachineParts.CountAsync(p => p.MachineId == id);
        if (partCount > 0)
            throw new InvalidOperationException($"Cannot delete '{machine.Name}' - it still has {partCount} part(s). Delete those first.");

        var sampleCount = await _db.Samples.CountAsync(s => s.MachineId == id);
        if (sampleCount > 0)
            throw new InvalidOperationException($"Cannot delete '{machine.Name}' - it has been used to receive {sampleCount} sample(s).");

        _db.Machines.Remove(machine);
        await _db.SaveChangesAsync();
        return new { };
    }

    public async Task<MachinePartResponse> CreateMachinePartAsync(CreateMachinePartRequest request)
    {
        var part = new MachinePart { Name = request.Name, MachineId = request.MachineId };
        _db.MachineParts.Add(part);
        await _db.SaveChangesAsync();
        return MachinePartResponse.From(part);
    }

    public async Task<MachinePartResponse> UpdateMachinePartAsync(int id, UpdateMachinePartRequest request)
    {
        var part = await _db.MachineParts.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NotFoundException($"Machine part {id} not found.");
        RecordVersion.EnsureCurrent(_db, part);
        part.Name = request.Name;
        part.MachineId = request.MachineId;
        await _db.SaveChangesAsync();
        return MachinePartResponse.From(part);
    }

    // Blocked if this part still has test configurations - configurations
    // have no downstream dependents of their own (TestOrder.TestCode is a
    // copied string, not an FK), so removing those first is enough.
    public async Task<object> DeleteMachinePartAsync(int id)
    {
        var part = await _db.MachineParts.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NotFoundException($"Machine part {id} not found.");

        var configCount = await _db.MachinePartConfigurations.CountAsync(c => c.MachinePartId == id);
        if (configCount > 0)
            throw new InvalidOperationException($"Cannot delete '{part.Name}' - it still has {configCount} test configuration(s). Delete those first.");

        _db.MachineParts.Remove(part);
        await _db.SaveChangesAsync();
        return new { };
    }

    public async Task<List<MachinePartConfigurationResponse>> GetMachinePartConfigurationsAsync(int machinePartId) =>
        (await _db.MachinePartConfigurations.AsNoTracking().Where(c => c.MachinePartId == machinePartId).ToListAsync()).Select(MachinePartConfigurationResponse.From).ToList();

    // A part may carry several tests (e.g. a swab count plus several
    // pathogens), but the same test type + test code only once - a
    // duplicate would put the part in the same TestOrder twice.
    private async Task EnsureNotDuplicateAsync(int machinePartId, string testType, string testCode, int? exceptId)
    {
        var duplicate = await _db.MachinePartConfigurations.AnyAsync(c =>
            c.MachinePartId == machinePartId && c.TestType == testType && c.TestCode == testCode && c.Id != exceptId);
        if (duplicate)
            throw new InvalidOperationException($"{testCode} ({testType}) is already configured for this part.");
    }

    public async Task<MachinePartConfigurationResponse> CreateMachinePartConfigurationAsync(CreateMachinePartConfigRequest request)
    {
        await EnsureNotDuplicateAsync(request.MachinePartId, request.TestType, request.TestCode, null);
        var entity = new MachinePartConfiguration
        {
            MachinePartId = request.MachinePartId, TestType = request.TestType, TestCode = request.TestCode,
            AlertLimit = request.AlertLimit, ActionLimit = request.ActionLimit, SpecLimit = request.SpecLimit,
            IsPathogenTest = request.IsPathogenTest,
            Unit = request.Unit ?? string.Empty
        };
        _db.MachinePartConfigurations.Add(entity);
        await _db.SaveChangesAsync();
        return MachinePartConfigurationResponse.From(entity);
    }

    public async Task<MachinePartConfigurationResponse> UpdateMachinePartConfigurationAsync(int id, UpdateMachinePartConfigRequest request)
    {
        var entity = await _db.MachinePartConfigurations.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException($"Machine part configuration {id} not found.");
        RecordVersion.EnsureCurrent(_db, entity);
        await EnsureNotDuplicateAsync(entity.MachinePartId, request.TestType, request.TestCode, entity.Id);
        entity.TestType = request.TestType;
        entity.TestCode = request.TestCode;
        entity.AlertLimit = request.AlertLimit;
        entity.ActionLimit = request.ActionLimit;
        entity.SpecLimit = request.SpecLimit;
        entity.IsPathogenTest = request.IsPathogenTest;
        entity.Unit = request.Unit ?? string.Empty;
        await _db.SaveChangesAsync();
        return MachinePartConfigurationResponse.From(entity);
    }

    // TestOrder.TestCode is a copied string, but SampleLocation keeps a
    // restricted FK to this row - refuse with a clear message rather than
    // a raw FK error once samples have used it.
    public async Task<object> DeleteMachinePartConfigurationAsync(int id)
    {
        var entity = await _db.MachinePartConfigurations.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException($"Machine part configuration {id} not found.");
        var usedBy = await _db.SampleLocations.CountAsync(l => l.MachinePartConfigurationId == id);
        if (usedBy > 0)
            throw new InvalidOperationException(
                $"{entity.TestCode} has already been tested on this part ({usedBy} sample location(s)), so it cannot be removed.");
        _db.MachinePartConfigurations.Remove(entity);
        await _db.SaveChangesAsync();
        return new { };
    }
}
