using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services;

public class SupplierStatedAttributeService
{
    public const string CreatedActionCode = "SupplierStatedAttributeCreated";
    public const string UpdatedActionCode = "SupplierStatedAttributeUpdated";
    public const string DeletedActionCode = "SupplierStatedAttributeDeleted";
    public const string RecordType = "SupplierStatedAttribute";

    private readonly IMicroLimsDbContext _db;
    private readonly IAuditEventService _auditEventService;

    public SupplierStatedAttributeService(IMicroLimsDbContext db, IAuditEventService auditEventService)
    {
        _db = db;
        _auditEventService = auditEventService;
    }

    public async Task<List<SupplierStatedAttributeDto>> ListForItemAsync(int itemId)
    {
        return await _db.SupplierStatedAttributes
            .Where(a => a.ItemId == itemId)
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Name)
            .ThenBy(a => a.Id)
            .Select(a => new SupplierStatedAttributeDto(a.Id, a.ItemId, a.Grade, a.Name, a.StatedValue, a.SortOrder))
            .ToListAsync();
    }

    public async Task<SupplierStatedAttributeDto> CreateAsync(int userId, SupplierStatedAttributeRequest r)
    {
        _db.CurrentUserId = userId;

        var itemExists = await _db.Items.AnyAsync(i => i.Id == r.ItemId);
        if (!itemExists)
            throw new NotFoundException($"Item {r.ItemId} not found.");

        if (string.IsNullOrWhiteSpace(r.Name))
            throw new InvalidOperationException("Name is required.");
        var trimmedName = r.Name.Trim();
        if (trimmedName.Length > 200)
            throw new InvalidOperationException("Name must be 200 characters or fewer.");

        if (string.IsNullOrWhiteSpace(r.StatedValue))
            throw new InvalidOperationException("Stated value is required.");
        var trimmedValue = r.StatedValue.Trim();
        if (trimmedValue.Length > 500)
            throw new InvalidOperationException("Stated value must be 500 characters or fewer.");

        var validatedGrade = await ValidateGradeAsync(r.ItemId, r.Grade);

        var entity = new SupplierStatedAttribute
        {
            ItemId = r.ItemId,
            Grade = validatedGrade,
            Name = trimmedName,
            StatedValue = trimmedValue,
            SortOrder = r.SortOrder
        };

        _db.SupplierStatedAttributes.Add(entity);
        await _db.SaveChangesAsync();

        var changes = new List<AuditFieldChange>
        {
            new("Name", null, trimmedName),
            new("StatedValue", null, trimmedValue),
            new("Grade", null, validatedGrade),
            new("SortOrder", null, r.SortOrder.ToString())
        };

        await _auditEventService.RecordUserEventAsync(
            CreatedActionCode,
            AuditActionCategory.Configuration,
            RecordType,
            changes: changes,
            entityId: entity.Id.ToString());

        return new SupplierStatedAttributeDto(entity.Id, entity.ItemId, entity.Grade, entity.Name, entity.StatedValue, entity.SortOrder);
    }

    public async Task<SupplierStatedAttributeDto> UpdateAsync(int userId, int id, SupplierStatedAttributeRequest r)
    {
        _db.CurrentUserId = userId;

        var entity = await _db.SupplierStatedAttributes.FirstOrDefaultAsync(a => a.Id == id)
            ?? throw new NotFoundException($"Supplier-stated attribute {id} not found.");

        if (string.IsNullOrWhiteSpace(r.Name))
            throw new InvalidOperationException("Name is required.");
        var trimmedName = r.Name.Trim();
        if (trimmedName.Length > 200)
            throw new InvalidOperationException("Name must be 200 characters or fewer.");

        if (string.IsNullOrWhiteSpace(r.StatedValue))
            throw new InvalidOperationException("Stated value is required.");
        var trimmedValue = r.StatedValue.Trim();
        if (trimmedValue.Length > 500)
            throw new InvalidOperationException("Stated value must be 500 characters or fewer.");

        var validatedGrade = await ValidateGradeAsync(entity.ItemId, r.Grade);

        var changes = new List<AuditFieldChange>();
        if (entity.Name != trimmedName)
            changes.Add(new AuditFieldChange("Name", entity.Name, trimmedName));
        if (entity.StatedValue != trimmedValue)
            changes.Add(new AuditFieldChange("StatedValue", entity.StatedValue, trimmedValue));
        if (entity.Grade != validatedGrade)
            changes.Add(new AuditFieldChange("Grade", entity.Grade, validatedGrade));
        if (entity.SortOrder != r.SortOrder)
            changes.Add(new AuditFieldChange("SortOrder", entity.SortOrder.ToString(), r.SortOrder.ToString()));

        entity.Name = trimmedName;
        entity.StatedValue = trimmedValue;
        entity.Grade = validatedGrade;
        entity.SortOrder = r.SortOrder;

        await _db.SaveChangesAsync();

        if (changes.Count > 0)
        {
            await _auditEventService.RecordUserEventAsync(
                UpdatedActionCode,
                AuditActionCategory.Configuration,
                RecordType,
                changes: changes,
                entityId: entity.Id.ToString());
        }

        return new SupplierStatedAttributeDto(entity.Id, entity.ItemId, entity.Grade, entity.Name, entity.StatedValue, entity.SortOrder);
    }

    public async Task DeleteAsync(int userId, int id)
    {
        _db.CurrentUserId = userId;

        var entity = await _db.SupplierStatedAttributes.FirstOrDefaultAsync(a => a.Id == id)
            ?? throw new NotFoundException($"Supplier-stated attribute {id} not found.");

        var changes = new List<AuditFieldChange>
        {
            new("Name", entity.Name, null),
            new("StatedValue", entity.StatedValue, null),
            new("Grade", entity.Grade, null),
            new("SortOrder", entity.SortOrder.ToString(), null)
        };

        _db.SupplierStatedAttributes.Remove(entity);
        await _db.SaveChangesAsync();

        await _auditEventService.RecordUserEventAsync(
            DeletedActionCode,
            AuditActionCategory.Configuration,
            RecordType,
            changes: changes,
            entityId: id.ToString());
    }

    public Task<List<SupplierStatedAttributeDto>> ForSampleAsync(int itemId, string? grade) =>
        ForSampleAsync(_db, itemId, grade);

    public static async Task<List<SupplierStatedAttributeDto>> ForSampleAsync(IMicroLimsDbContext db, int itemId, string? grade)
    {
        var normalizedGrade = SpecificationService.NormalizeGrade(grade);
        var query = db.SupplierStatedAttributes.Where(a => a.ItemId == itemId);

        if (normalizedGrade is null)
        {
            query = query.Where(a => a.Grade == null);
        }
        else
        {
            var lowerGrade = normalizedGrade.ToLower();
            query = query.Where(a => a.Grade == null || (a.Grade != null && a.Grade.ToLower() == lowerGrade));
        }

        return await query
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Name)
            .ThenBy(a => a.Id)
            .Select(a => new SupplierStatedAttributeDto(a.Id, a.ItemId, a.Grade, a.Name, a.StatedValue, a.SortOrder))
            .ToListAsync();
    }

    private async Task<string?> ValidateGradeAsync(int itemId, string? grade, CancellationToken ct = default)
    {
        var normalized = SpecificationService.NormalizeGrade(grade);
        if (normalized is not null)
        {
            var itemGrades = await _db.Specifications
                .Where(s => s.ItemId == itemId && s.Grade != null)
                .Select(s => s.Grade!)
                .Distinct()
                .ToListAsync(ct);

            var match = itemGrades.FirstOrDefault(g => string.Equals(g, normalized, StringComparison.OrdinalIgnoreCase));
            if (match is null)
                throw new InvalidOperationException($"Grade {normalized} has no specification for this item.");

            return match;
        }

        return null;
    }
}
