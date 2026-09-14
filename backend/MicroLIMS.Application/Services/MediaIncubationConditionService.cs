using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record MediaIncubationConditionDto(
    int Id,
    int MediaProductId,
    int IncubationMinHours,
    int IncubationMaxHours,
    decimal TemperatureMin,
    decimal TemperatureMax,
    int ConfigurationCount,
    int StepMediaCount);

public class MediaIncubationConditionService
{
    private readonly MicroLimsDbContext _db;

    public MediaIncubationConditionService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<List<MediaIncubationConditionDto>> GetAllAsync(int? mediaProductId)
    {
        var query = _db.MediaIncubationConditions.AsQueryable();
        if (mediaProductId.HasValue)
        {
            query = query.Where(c => c.MediaProductId == mediaProductId.Value);
        }

        return await query
            .OrderBy(c => c.MediaProductId)
            .ThenBy(c => c.TemperatureMin)
            .ThenBy(c => c.IncubationMinHours)
            .Select(c => new MediaIncubationConditionDto(
                c.Id,
                c.MediaProductId,
                c.IncubationMinHours,
                c.IncubationMaxHours,
                c.TemperatureMin,
                c.TemperatureMax,
                _db.MediaConfigurations.Count(cfg => cfg.MediaIncubationConditionId == c.Id),
                _db.TestWorkflowStepMedias.Count(sm => sm.MediaIncubationConditionId == c.Id)))
            .ToListAsync();
    }

    public async Task<MediaIncubationCondition> CreateAsync(
        int mediaProductId, int minH, int maxH, decimal tMin, decimal tMax)
    {
        var product = await _db.MediaProducts.FirstOrDefaultAsync(p => p.Id == mediaProductId)
            ?? throw new InvalidOperationException($"Media product with ID {mediaProductId} not found.");

        ValidateRange(minH, maxH, tMin, tMax);

        var exists = await _db.MediaIncubationConditions.AnyAsync(c =>
            c.MediaProductId == mediaProductId &&
            c.IncubationMinHours == minH &&
            c.IncubationMaxHours == maxH &&
            c.TemperatureMin == tMin &&
            c.TemperatureMax == tMax);

        if (exists)
        {
            throw new InvalidOperationException(
                $"'{product.Name}' already has an incubation condition of {minH}–{maxH} h at {tMin}–{tMax} °C.");
        }

        var condition = new MediaIncubationCondition
        {
            MediaProductId = mediaProductId,
            IncubationMinHours = minH,
            IncubationMaxHours = maxH,
            TemperatureMin = tMin,
            TemperatureMax = tMax
        };

        _db.MediaIncubationConditions.Add(condition);
        await _db.SaveChangesAsync();
        return condition;
    }

    public async Task<MediaIncubationCondition> UpdateAsync(
        int id, int minH, int maxH, decimal tMin, decimal tMax)
    {
        var condition = await _db.MediaIncubationConditions
            .Include(c => c.MediaProduct)
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new InvalidOperationException($"Incubation condition with ID {id} not found.");

        await EnsureNotLockedAsync(id);

        ValidateRange(minH, maxH, tMin, tMax);

        var exists = await _db.MediaIncubationConditions.AnyAsync(c =>
            c.Id != id &&
            c.MediaProductId == condition.MediaProductId &&
            c.IncubationMinHours == minH &&
            c.IncubationMaxHours == maxH &&
            c.TemperatureMin == tMin &&
            c.TemperatureMax == tMax);

        if (exists)
        {
            var productName = condition.MediaProduct?.Name
                ?? (await _db.MediaProducts.Where(p => p.Id == condition.MediaProductId).Select(p => p.Name).FirstOrDefaultAsync())
                ?? string.Empty;

            throw new InvalidOperationException(
                $"'{productName}' already has an incubation condition of {minH}–{maxH} h at {tMin}–{tMax} °C.");
        }

        condition.IncubationMinHours = minH;
        condition.IncubationMaxHours = maxH;
        condition.TemperatureMin = tMin;
        condition.TemperatureMax = tMax;

        await _db.SaveChangesAsync();
        return condition;
    }

    public async Task DeleteAsync(int id)
    {
        var condition = await _db.MediaIncubationConditions.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new InvalidOperationException($"Incubation condition with ID {id} not found.");

        await EnsureNotLockedAsync(id);

        _db.MediaIncubationConditions.Remove(condition);
        await _db.SaveChangesAsync();
    }

    private static void ValidateRange(int minH, int maxH, decimal tMin, decimal tMax)
    {
        if (minH <= 0)
            throw new InvalidOperationException("Incubation min hours must be greater than zero.");
        if (maxH < minH)
            throw new InvalidOperationException("Incubation max hours cannot be less than min hours.");
        if (tMin > tMax)
            throw new InvalidOperationException("Temperature min cannot exceed max.");
    }

    private async Task EnsureNotLockedAsync(int id)
    {
        var configCount = await _db.MediaConfigurations.CountAsync(cfg => cfg.MediaIncubationConditionId == id);
        var stepMediaCount = await _db.TestWorkflowStepMedias.CountAsync(sm => sm.MediaIncubationConditionId == id);

        if (configCount > 0 || stepMediaCount > 0)
        {
            throw new InvalidOperationException(
                $"This incubation condition is used by {configCount} evaluation configuration(s) and {stepMediaCount} Test Master step medium/media, so it can't be changed or deleted. Add a new condition instead.");
        }
    }
}
