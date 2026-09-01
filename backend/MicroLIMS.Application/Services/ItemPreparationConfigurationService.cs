using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record ItemPreparationConfigurationDto(
    int Id,
    int ItemId,
    decimal Amount,
    string Unit,
    string Technique,
    decimal? FiltrationVolume,
    decimal? WashingVolume,
    int DiluentTypeId,
    string DiluentTypeName,
    int? DiluentMediaId,
    string? DiluentMediaLotNumber,
    int NeutralizerId,
    string NeutralizerName,
    ApprovalGateStatus ApprovalStatus,
    int CreatedByUserId,
    string? CreatedByName,
    DateTime CreatedAt,
    int? ApprovedByUserId,
    string? ApprovedByName,
    DateTime? ApprovedAt);

// Per-item preparation protocol. Configured once by a Section Head, or
// seeded automatically from the first analyst's manual entry (in which
// case it lands as PendingReview but is usable immediately - testing is
// never blocked waiting for approval).
public class ItemPreparationConfigurationService
{
    private readonly MicroLimsDbContext _db;
    private readonly PreparationParameterValidator _validator;

    public ItemPreparationConfigurationService(MicroLimsDbContext db, PreparationParameterValidator validator)
    {
        _db = db;
        _validator = validator;
    }

    public async Task<ItemPreparationConfigurationDto?> GetByItemIdAsync(int itemId)
    {
        var config = await _db.ItemPreparationConfigurations
            .AsNoTracking()
            .Include(c => c.DiluentType)
            .Include(c => c.DiluentMedia)
            .Include(c => c.Neutralizer)
            .FirstOrDefaultAsync(c => c.ItemId == itemId);

        return config is null ? null : await ToDtoAsync(config);
    }

    public async Task<ItemPreparationConfigurationDto> UpsertAsync(int itemId, PreparationParameters p, int userId)
    {
        if (!await _db.Items.AnyAsync(i => i.Id == itemId))
            throw new InvalidOperationException($"Item {itemId} not found.");

        var diluentType = await _validator.ValidateAsync(p);

        var config = await _db.ItemPreparationConfigurations.FirstOrDefaultAsync(c => c.ItemId == itemId);
        if (config is null)
        {
            config = new ItemPreparationConfiguration { ItemId = itemId, CreatedByUserId = userId };
            _db.ItemPreparationConfigurations.Add(config);
        }
        else
        {
            // Any edit re-opens approval - an already-approved protocol that
            // changed is no longer the protocol the Section Head signed off.
            config.ApprovedByUserId = null;
            config.ApprovedAt = null;
        }

        config.Amount = p.Amount;
        config.Unit = p.Unit;
        config.Technique = p.Technique;
        config.FiltrationVolume = p.FiltrationVolume;
        config.WashingVolume = p.WashingVolume;
        config.DiluentTypeId = p.DiluentTypeId;
        config.DiluentMediaId = diluentType.RequiresBatchTracking ? p.DiluentMediaId : null;
        config.NeutralizerId = p.NeutralizerId;
        config.ApprovalStatus = ApprovalGateStatus.PendingReview;

        await _db.SaveChangesAsync();
        return (await GetByItemIdAsync(itemId))!;
    }

    public async Task<ItemPreparationConfigurationDto> ApproveAsync(int itemId, int userId)
    {
        var config = await _db.ItemPreparationConfigurations.FirstOrDefaultAsync(c => c.ItemId == itemId)
            ?? throw new InvalidOperationException($"Item {itemId} has no preparation configuration to approve.");

        if (config.ApprovalStatus == ApprovalGateStatus.Approved)
            throw new InvalidOperationException("This preparation configuration is already approved.");

        config.ApprovalStatus = ApprovalGateStatus.Approved;
        config.ApprovedByUserId = userId;
        config.ApprovedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return (await GetByItemIdAsync(itemId))!;
    }

    public async Task<int> CountPendingApprovalAsync() =>
        await _db.ItemPreparationConfigurations.CountAsync(c => c.ApprovalStatus == ApprovalGateStatus.PendingReview);

    public async Task<List<ItemPreparationConfigurationDto>> GetPendingApprovalAsync()
    {
        var configs = await _db.ItemPreparationConfigurations
            .AsNoTracking()
            .Include(c => c.DiluentType)
            .Include(c => c.DiluentMedia)
            .Include(c => c.Neutralizer)
            .Where(c => c.ApprovalStatus == ApprovalGateStatus.PendingReview)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        var dtos = new List<ItemPreparationConfigurationDto>();
        foreach (var c in configs)
            dtos.Add(await ToDtoAsync(c));
        return dtos;
    }

    private async Task<ItemPreparationConfigurationDto> ToDtoAsync(ItemPreparationConfiguration c)
    {
        var userIds = new List<int> { c.CreatedByUserId };
        if (c.ApprovedByUserId.HasValue) userIds.Add(c.ApprovedByUserId.Value);

        var names = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        return new ItemPreparationConfigurationDto(
            c.Id,
            c.ItemId,
            c.Amount,
            c.Unit,
            c.Technique,
            c.FiltrationVolume,
            c.WashingVolume,
            c.DiluentTypeId,
            c.DiluentType?.Name ?? string.Empty,
            c.DiluentMediaId,
            c.DiluentMedia?.LotNumber,
            c.NeutralizerId,
            c.Neutralizer?.Name ?? string.Empty,
            c.ApprovalStatus,
            c.CreatedByUserId,
            names.TryGetValue(c.CreatedByUserId, out var createdBy) ? createdBy : null,
            c.CreatedAt,
            c.ApprovedByUserId,
            c.ApprovedByUserId.HasValue && names.TryGetValue(c.ApprovedByUserId.Value, out var approvedBy) ? approvedBy : null,
            c.ApprovedAt);
    }
}
