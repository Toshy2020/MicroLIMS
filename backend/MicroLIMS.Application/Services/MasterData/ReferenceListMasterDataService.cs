using System.Globalization;
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
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Application.Services.MasterData;

// Master data: reference list. Behind ReferenceListMasterDataController
// (api/masterdata/...); the controller only maps HTTP to these calls.
public class ReferenceListMasterDataService
{
    private readonly IMicroLimsDbContext _db;
    private readonly TimeProvider _time;

    public ReferenceListMasterDataService(IMicroLimsDbContext db, TimeProvider? timeProvider = null)
    {
        _db = db;
        _time = timeProvider ?? TimeProvider.System;
    }

    public async Task<List<CauseOfTestingResponse>> GetCausesOfTestingAsync() =>
        (await _db.CausesOfTesting.AsNoTracking().Where(c => c.IsActive).ToListAsync()).Select(CauseOfTestingResponse.From).ToList();

    public async Task<CauseOfTestingResponse> CreateCauseOfTestingAsync(string name)
    {
        if (await _db.CausesOfTesting.AnyAsync(c => c.Name.ToLower() == name.ToLower()))
            throw new BusinessRuleException(
                MessageCodes.ReferenceListCauseOfTestingExists,
                $"Cause of Testing \"{name}\" already exists.",
                new Dictionary<string, string> { ["name"] = name });

        var entity = new CauseOfTesting { Name = name };
        _db.CausesOfTesting.Add(entity);
        await _db.SaveChangesAsync();
        return CauseOfTestingResponse.From(entity);
    }

    public async Task<CauseOfTestingResponse> UpdateCauseOfTestingAsync(int id, string name)
    {
        var entity = await _db.CausesOfTesting.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException(
                MessageCodes.ReferenceListCauseOfTestingNotFound,
                $"Cause of Testing {id} not found.",
                new Dictionary<string, string> { ["id"] = id.ToString(CultureInfo.InvariantCulture) });
        RecordVersion.EnsureCurrent(_db, entity);

        if (await _db.CausesOfTesting.AnyAsync(c => c.Id != id && c.Name.ToLower() == name.ToLower()))
            throw new BusinessRuleException(
                MessageCodes.ReferenceListCauseOfTestingExists,
                $"Cause of Testing \"{name}\" already exists.",
                new Dictionary<string, string> { ["name"] = name });

        entity.Name = name;
        await _db.SaveChangesAsync();
        return CauseOfTestingResponse.From(entity);
    }

    // Blocked (not a raw FK error) if any Sample still references this
    // cause - real FK (Sample.CauseOfTestingId is Restrict), same
    // "guard with a clear message" pattern as DeleteOrganism.
    public async Task<object> DeleteCauseOfTestingAsync(int id)
    {
        var entity = await _db.CausesOfTesting.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException(
                MessageCodes.ReferenceListCauseOfTestingNotFound,
                $"Cause of Testing {id} not found.",
                new Dictionary<string, string> { ["id"] = id.ToString(CultureInfo.InvariantCulture) });

        var sampleCount = await _db.Samples.CountAsync(s => s.CauseOfTestingId == id);
        if (sampleCount > 0)
            throw new BusinessRuleException(
                MessageCodes.ReferenceListCauseOfTestingInUse,
                $"Cannot delete '{entity.Name}' - it is referenced by {sampleCount} sample(s).",
                new Dictionary<string, string>
                {
                    ["name"] = entity.Name,
                    ["sampleCount"] = sampleCount.ToString(CultureInfo.InvariantCulture)
                });

        _db.CausesOfTesting.Remove(entity);
        await _db.SaveChangesAsync();
        return new { };
    }

    public async Task<List<SamplerResponse>> GetSamplersAsync() =>
        (await _db.Samplers.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync()).Select(SamplerResponse.From).ToList();

    public async Task<SamplerResponse> CreateSamplerAsync(string name)
    {
        if (await _db.Samplers.AnyAsync(s => s.Name.ToLower() == name.ToLower()))
            throw new BusinessRuleException(
                MessageCodes.ReferenceListSamplerExists,
                $"Sampler \"{name}\" already exists.",
                new Dictionary<string, string> { ["name"] = name });

        var entity = new Sampler { Name = name };
        _db.Samplers.Add(entity);
        await _db.SaveChangesAsync();
        return SamplerResponse.From(entity);
    }

    public async Task<SamplerResponse> UpdateSamplerAsync(int id, string name)
    {
        var entity = await _db.Samplers.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new NotFoundException(
                MessageCodes.ReferenceListSamplerNotFound,
                $"Sampler {id} not found.",
                new Dictionary<string, string> { ["id"] = id.ToString(CultureInfo.InvariantCulture) });
        RecordVersion.EnsureCurrent(_db, entity);

        if (await _db.Samplers.AnyAsync(s => s.Id != id && s.Name.ToLower() == name.ToLower()))
            throw new BusinessRuleException(
                MessageCodes.ReferenceListSamplerExists,
                $"Sampler \"{name}\" already exists.",
                new Dictionary<string, string> { ["name"] = name });

        entity.Name = name;
        await _db.SaveChangesAsync();
        return SamplerResponse.From(entity);
    }

    public async Task<object> DeleteSamplerAsync(int id)
    {
        var entity = await _db.Samplers.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new NotFoundException(
                MessageCodes.ReferenceListSamplerNotFound,
                $"Sampler {id} not found.",
                new Dictionary<string, string> { ["id"] = id.ToString(CultureInfo.InvariantCulture) });

        _db.Samplers.Remove(entity);
        await _db.SaveChangesAsync();
        return new { };
    }

    public async Task<List<ProductionStageResponse>> GetProductionStagesAsync() =>
        (await _db.ProductionStages.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync()).Select(ProductionStageResponse.From).ToList();

    public async Task<ProductionStageResponse> CreateProductionStageAsync(CreateProductionStageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BusinessRuleException(
                MessageCodes.ReferenceListNameRequired,
                "Name is required.");

        if (await _db.ProductionStages.AnyAsync(s => s.Name.ToLower() == request.Name.ToLower()))
            throw new BusinessRuleException(
                MessageCodes.ReferenceListProductionStageExists,
                $"Production Stage \"{request.Name}\" already exists.",
                new Dictionary<string, string> { ["name"] = request.Name });

        var entity = new ProductionStage { Name = request.Name, Role = request.Role };
        _db.ProductionStages.Add(entity);
        await _db.SaveChangesAsync();
        return ProductionStageResponse.From(entity);
    }

    public async Task<ProductionStageResponse> UpdateProductionStageAsync(int id, UpdateProductionStageRequest request)
    {
        var entity = await _db.ProductionStages.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new NotFoundException(
                MessageCodes.ReferenceListProductionStageNotFound,
                $"Production Stage {id} not found.",
                new Dictionary<string, string> { ["id"] = id.ToString(CultureInfo.InvariantCulture) });
        RecordVersion.EnsureCurrent(_db, entity);

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BusinessRuleException(
                MessageCodes.ReferenceListNameRequired,
                "Name is required.");

        if (await _db.ProductionStages.AnyAsync(s => s.Id != id && s.Name.ToLower() == request.Name.ToLower()))
            throw new BusinessRuleException(
                MessageCodes.ReferenceListProductionStageExists,
                $"Production Stage \"{request.Name}\" already exists.",
                new Dictionary<string, string> { ["name"] = request.Name });

        entity.Name = request.Name;
        entity.Role = request.Role;
        await _db.SaveChangesAsync();
        return ProductionStageResponse.From(entity);
    }

    public async Task<object> DeleteProductionStageAsync(int id)
    {
        var entity = await _db.ProductionStages.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new NotFoundException(
                MessageCodes.ReferenceListProductionStageNotFound,
                $"Production Stage {id} not found.",
                new Dictionary<string, string> { ["id"] = id.ToString(CultureInfo.InvariantCulture) });

        _db.ProductionStages.Remove(entity);
        await _db.SaveChangesAsync();
        return new { };
    }

    public async Task<List<NeutralizerResponse>> GetNeutralizersAsync() =>
        (await _db.Neutralizers.AsNoTracking().Where(n => n.IsActive).ToListAsync()).Select(NeutralizerResponse.From).ToList();

    public async Task<NeutralizerResponse> CreateNeutralizerAsync(string name)
    {
        var entity = new Neutralizer { Name = name };
        _db.Neutralizers.Add(entity);
        await _db.SaveChangesAsync();
        return NeutralizerResponse.From(entity);
    }
}
