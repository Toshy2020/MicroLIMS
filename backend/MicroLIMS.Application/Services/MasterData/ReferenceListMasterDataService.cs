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
            throw new InvalidOperationException($"Cause of Testing \"{name}\" already exists.");

        var entity = new CauseOfTesting { Name = name };
        _db.CausesOfTesting.Add(entity);
        await _db.SaveChangesAsync();
        return CauseOfTestingResponse.From(entity);
    }

    public async Task<CauseOfTestingResponse> UpdateCauseOfTestingAsync(int id, string name)
    {
        var entity = await _db.CausesOfTesting.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException($"Cause of Testing {id} not found.");
        RecordVersion.EnsureCurrent(_db, entity);

        if (await _db.CausesOfTesting.AnyAsync(c => c.Id != id && c.Name.ToLower() == name.ToLower()))
            throw new InvalidOperationException($"Cause of Testing \"{name}\" already exists.");

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
            ?? throw new NotFoundException($"Cause of Testing {id} not found.");

        var sampleCount = await _db.Samples.CountAsync(s => s.CauseOfTestingId == id);
        if (sampleCount > 0)
            throw new InvalidOperationException($"Cannot delete '{entity.Name}' - it is referenced by {sampleCount} sample(s).");

        _db.CausesOfTesting.Remove(entity);
        await _db.SaveChangesAsync();
        return new { };
    }

    public async Task<List<SamplerResponse>> GetSamplersAsync() =>
        (await _db.Samplers.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync()).Select(SamplerResponse.From).ToList();

    public async Task<SamplerResponse> CreateSamplerAsync(string name)
    {
        if (await _db.Samplers.AnyAsync(s => s.Name.ToLower() == name.ToLower()))
            throw new InvalidOperationException($"Sampler \"{name}\" already exists.");

        var entity = new Sampler { Name = name };
        _db.Samplers.Add(entity);
        await _db.SaveChangesAsync();
        return SamplerResponse.From(entity);
    }

    public async Task<SamplerResponse> UpdateSamplerAsync(int id, string name)
    {
        var entity = await _db.Samplers.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new NotFoundException($"Sampler {id} not found.");
        RecordVersion.EnsureCurrent(_db, entity);

        if (await _db.Samplers.AnyAsync(s => s.Id != id && s.Name.ToLower() == name.ToLower()))
            throw new InvalidOperationException($"Sampler \"{name}\" already exists.");

        entity.Name = name;
        await _db.SaveChangesAsync();
        return SamplerResponse.From(entity);
    }

    public async Task<object> DeleteSamplerAsync(int id)
    {
        var entity = await _db.Samplers.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new NotFoundException($"Sampler {id} not found.");

        _db.Samplers.Remove(entity);
        await _db.SaveChangesAsync();
        return new { };
    }

    public async Task<List<ProductionStageResponse>> GetProductionStagesAsync() =>
        (await _db.ProductionStages.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync()).Select(ProductionStageResponse.From).ToList();

    public async Task<ProductionStageResponse> CreateProductionStageAsync(CreateProductionStageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Name is required.");

        if (await _db.ProductionStages.AnyAsync(s => s.Name.ToLower() == request.Name.ToLower()))
            throw new InvalidOperationException($"Production Stage \"{request.Name}\" already exists.");

        var entity = new ProductionStage { Name = request.Name, Role = request.Role };
        _db.ProductionStages.Add(entity);
        await _db.SaveChangesAsync();
        return ProductionStageResponse.From(entity);
    }

    public async Task<ProductionStageResponse> UpdateProductionStageAsync(int id, UpdateProductionStageRequest request)
    {
        var entity = await _db.ProductionStages.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new NotFoundException($"Production Stage {id} not found.");
        RecordVersion.EnsureCurrent(_db, entity);

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Name is required.");

        if (await _db.ProductionStages.AnyAsync(s => s.Id != id && s.Name.ToLower() == request.Name.ToLower()))
            throw new InvalidOperationException($"Production Stage \"{request.Name}\" already exists.");

        entity.Name = request.Name;
        entity.Role = request.Role;
        await _db.SaveChangesAsync();
        return ProductionStageResponse.From(entity);
    }

    public async Task<object> DeleteProductionStageAsync(int id)
    {
        var entity = await _db.ProductionStages.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new NotFoundException($"Production Stage {id} not found.");

        _db.ProductionStages.Remove(entity);
        await _db.SaveChangesAsync();
        return new { };
    }

    public async Task<List<DiluentTypeResponse>> GetDiluentTypesAsync() =>
        (await _db.DiluentTypes.AsNoTracking().Include(d => d.MediaProduct).ToListAsync()).Select(DiluentTypeResponse.From).ToList();

    // A batch-tracked diluent (e.g. TSB) is a Culture Media product: the analyst
    // later picks any released lot of it. Other diluents carry no product.
    public async Task<DiluentTypeResponse> CreateDiluentTypeAsync(CreateDiluentTypeRequest request)
    {
        MediaProduct? product = null;
        if (request.RequiresBatchTracking)
        {
            if (request.MediaProductId is not int productId)
                throw new InvalidOperationException("Choose the Culture Media product for a diluent that requires batch tracking.");
            product = await _db.MediaProducts.FirstOrDefaultAsync(p => p.Id == productId)
                ?? throw new InvalidOperationException($"Media product {productId} not found.");
            if (!product.IsActive)
                throw new InvalidOperationException($"Media product '{product.Name}' is inactive and can't be chosen for a diluent type.");
        }
        else if (request.MediaProductId is not null)
            throw new InvalidOperationException("A media product can only be set on a diluent that requires batch tracking.");

        var entity = new DiluentType { Name = request.Name, RequiresBatchTracking = request.RequiresBatchTracking, MediaProductId = product?.Id };
        _db.DiluentTypes.Add(entity);
        await _db.SaveChangesAsync();
        entity.MediaProduct = product;
        return DiluentTypeResponse.From(entity);
    }

    // The lots a batch-tracked diluent may be backed by: any GPT-released, active,
    // non-expired Media lot of the diluent's product, whichever batch it came from.
    public async Task<List<Media>> GetSelectableDiluentLotsAsync(int diluentTypeId)
    {
        var diluent = await _db.DiluentTypes.AsNoTracking().FirstOrDefaultAsync(d => d.Id == diluentTypeId)
            ?? throw new NotFoundException($"Diluent type {diluentTypeId} not found.");
        if (!diluent.RequiresBatchTracking || diluent.MediaProductId is not int productId)
            return new List<Media>();

        var now = _time.GetUtcNow().UtcDateTime;
        return await _db.Media.AsNoTracking()
            .Where(m => m.Material!.MediaProductId == productId && m.IsReleasedForUse && m.Status == MediaStatus.Active && m.ExpiryDate > now)
            .OrderBy(m => m.ExpiryDate)
            .ToListAsync();
    }

    public async Task<Media> RequireSelectableDiluentLotAsync(int diluentTypeId, int mediaLotId) =>
        (await GetSelectableDiluentLotsAsync(diluentTypeId)).FirstOrDefault(m => m.Id == mediaLotId)
            ?? throw new InvalidOperationException("The selected lot is not a released, active, non-expired lot of this diluent's media product.");

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
