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

// Master data: reference list. Behind ReferenceListMasterDataController
// (api/masterdata/...); the controller only maps HTTP to these calls.
public class ReferenceListMasterDataService
{
    private readonly IMicroLimsDbContext _db;

    public ReferenceListMasterDataService(IMicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<object> GetCausesOfTestingAsync() => await _db.CausesOfTesting.AsNoTracking().Where(c => c.IsActive).ToListAsync();

    public async Task<object> CreateCauseOfTestingAsync(string name)
    {
        if (await _db.CausesOfTesting.AnyAsync(c => c.Name.ToLower() == name.ToLower()))
            throw new InvalidOperationException($"Cause of Testing \"{name}\" already exists.");

        var entity = new CauseOfTesting { Name = name };
        _db.CausesOfTesting.Add(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    public async Task<object> UpdateCauseOfTestingAsync(int id, string name)
    {
        var entity = await _db.CausesOfTesting.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException($"Cause of Testing {id} not found.");

        if (await _db.CausesOfTesting.AnyAsync(c => c.Id != id && c.Name.ToLower() == name.ToLower()))
            throw new InvalidOperationException($"Cause of Testing \"{name}\" already exists.");

        entity.Name = name;
        await _db.SaveChangesAsync();
        return entity;
    }

    // Blocked (not a raw FK error) if any Sample still references this
    // cause - real FK (Sample.CauseOfTestingId is Restrict), same
    // "guard with a clear message" pattern as DeleteOrganism.
    public async Task<object> DeleteCauseOfTestingAsync(int id)
    {
        var entity = await _db.CausesOfTesting.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException($"Cause of Testing {id} not found.");

        var sampleCount = await _db.Samples.CountAsync(s => s.CauseOfTestingId == id);
        if (sampleCount > 0)
            throw new InvalidOperationException($"Cannot delete '{entity.Name}' - it is referenced by {sampleCount} sample(s).");

        _db.CausesOfTesting.Remove(entity);
        await _db.SaveChangesAsync();
        return new { };
    }

    public async Task<object> GetSamplersAsync() => await _db.Samplers.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();

    public async Task<object> CreateSamplerAsync(string name)
    {
        if (await _db.Samplers.AnyAsync(s => s.Name.ToLower() == name.ToLower()))
            throw new InvalidOperationException($"Sampler \"{name}\" already exists.");

        var entity = new Sampler { Name = name };
        _db.Samplers.Add(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    public async Task<object> UpdateSamplerAsync(int id, string name)
    {
        var entity = await _db.Samplers.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new NotFoundException($"Sampler {id} not found.");

        if (await _db.Samplers.AnyAsync(s => s.Id != id && s.Name.ToLower() == name.ToLower()))
            throw new InvalidOperationException($"Sampler \"{name}\" already exists.");

        entity.Name = name;
        await _db.SaveChangesAsync();
        return entity;
    }

    public async Task<object> DeleteSamplerAsync(int id)
    {
        var entity = await _db.Samplers.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new NotFoundException($"Sampler {id} not found.");

        _db.Samplers.Remove(entity);
        await _db.SaveChangesAsync();
        return new { };
    }

    public async Task<object> GetProductionStagesAsync() => await _db.ProductionStages.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();

    public async Task<object> CreateProductionStageAsync(CreateProductionStageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Name is required.");

        if (await _db.ProductionStages.AnyAsync(s => s.Name.ToLower() == request.Name.ToLower()))
            throw new InvalidOperationException($"Production Stage \"{request.Name}\" already exists.");

        var entity = new ProductionStage { Name = request.Name, Role = request.Role };
        _db.ProductionStages.Add(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    public async Task<object> UpdateProductionStageAsync(int id, UpdateProductionStageRequest request)
    {
        var entity = await _db.ProductionStages.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new NotFoundException($"Production Stage {id} not found.");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Name is required.");

        if (await _db.ProductionStages.AnyAsync(s => s.Id != id && s.Name.ToLower() == request.Name.ToLower()))
            throw new InvalidOperationException($"Production Stage \"{request.Name}\" already exists.");

        entity.Name = request.Name;
        entity.Role = request.Role;
        await _db.SaveChangesAsync();
        return entity;
    }

    public async Task<object> DeleteProductionStageAsync(int id)
    {
        var entity = await _db.ProductionStages.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new NotFoundException($"Production Stage {id} not found.");

        _db.ProductionStages.Remove(entity);
        await _db.SaveChangesAsync();
        return new { };
    }

    public async Task<object> GetDiluentTypesAsync() => await _db.DiluentTypes.AsNoTracking().ToListAsync();

    public async Task<object> CreateDiluentTypeAsync(CreateDiluentTypeRequest request)
    {
        var entity = new DiluentType { Name = request.Name, RequiresBatchTracking = request.RequiresBatchTracking, MaterialId = request.MaterialId };
        _db.DiluentTypes.Add(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    public async Task<object> GetNeutralizersAsync() => await _db.Neutralizers.AsNoTracking().Where(n => n.IsActive).ToListAsync();

    public async Task<object> CreateNeutralizerAsync(string name)
    {
        var entity = new Neutralizer { Name = name };
        _db.Neutralizers.Add(entity);
        await _db.SaveChangesAsync();
        return entity;
    }
}
