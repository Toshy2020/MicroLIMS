using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Services.Oos;

public sealed record SaveOosTemplateRequest(int? PhaseOneDueWorkingDays, int? TotalDueWorkingDays,
    bool AlertOpensExcursionReview, IReadOnlyList<OosTemplateItem> Items);

public sealed record OosTemplateItem(string? Key, int Phase, int Order, string Text, bool IsActive);
public sealed record OosTemplateDto(uint Version, int? PhaseOneDueWorkingDays, int? TotalDueWorkingDays,
    bool AlertOpensExcursionReview, IReadOnlyList<OosTemplateItem> Items);
public sealed record SaveOosCategoryRequest(string Name, bool IsActive, string Reason);
public sealed record OosCategoryDto(int Id, string Name, bool IsActive, bool IsNoCauseFound, uint Version);

public sealed class OosTemplateService(IMicroLimsDbContext db, IUserSectionScopeService sectionScope, PermissionService permissions)
{
    public async Task<OosTemplateDto> GetAsync(int sectionId, OosInvestigationKind kind, CancellationToken ct = default)
    {
        await EnsureConfigureAsync(sectionId, db.CurrentUserId ?? 0, ct);
        if (!Enum.IsDefined(kind))
            throw InvalidTemplate();
        return await ReadAsync(sectionId, kind, ct);
    }

    public Task<OosTemplateDto> SaveAsync(int sectionId, OosInvestigationKind kind, SaveOosTemplateRequest request,
        uint expectedVersion, int userId, CancellationToken ct = default) =>
        UnitOfWork.RunAsync(db, async () =>
        {
            await EnsureConfigureAsync(sectionId, userId, ct);
            var items = request.Items;
            if (!Enum.IsDefined(kind) || request.PhaseOneDueWorkingDays <= 0 || request.TotalDueWorkingDays <= 0 ||
                items is null || items.Any(i => i is null || i.Phase is not (1 or 2) || i.Key?.Length > 36 ||
                    string.IsNullOrWhiteSpace(i.Text) || i.Text.Length > 4000) ||
                items.Where(i => !string.IsNullOrWhiteSpace(i.Key)).GroupBy(i => i.Key).Any(g => g.Count() > 1))
                throw InvalidTemplate();

            // Serialize initial creation and subsequent edits for this section.
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""SELECT 1 FROM "DocumentSections" WHERE "Id" = {sectionId} FOR UPDATE""", ct);
            var row = await db.OosInvestigationTemplates.SingleOrDefaultAsync(t => t.SectionId == sectionId && t.Kind == kind, ct);
            if (expectedVersion != (row?.Version ?? 0))
                throw VersionConflict();

            db.CurrentUserId = userId;
            if (row is null)
            {
                row = new OosInvestigationTemplate { SectionId = sectionId, Kind = kind };
                ApplySettings(row, request);
                db.OosInvestigationTemplates.Add(row);
                await db.SaveChangesAsync(ct);
            }
            else
            {
                ApplySettings(row, request);
                // Item-only edits must advance the template concurrency token too.
                db.Entry(row).Property(nameof(OosInvestigationTemplate.AlertOpensExcursionReview)).IsModified = true;
            }

            var existing = await db.OosChecklistItems.Where(i => i.TemplateId == row.Id).ToDictionaryAsync(i => i.Key, ct);
            foreach (var item in items)
            {
                var key = string.IsNullOrWhiteSpace(item.Key) ? Guid.NewGuid().ToString() : item.Key;
                if (!existing.Remove(key, out var stored))
                {
                    if (!Guid.TryParseExact(key, "D", out _))
                        throw InvalidTemplate();
                    stored = new OosChecklistItem { TemplateId = row.Id, Key = key };
                    db.OosChecklistItems.Add(stored);
                }
                stored.Phase = item.Phase;
                stored.Order = item.Order;
                stored.Text = item.Text.Trim();
                stored.IsActive = item.IsActive;
            }
            foreach (var retired in existing.Values)
                retired.IsActive = false;

            await db.SaveChangesAsync(ct);
            return await ReadAsync(sectionId, kind, ct);
        }, ct);

    private static void ApplySettings(OosInvestigationTemplate target, SaveOosTemplateRequest request)
    {
        target.PhaseOneDueWorkingDays = request.PhaseOneDueWorkingDays;
        target.TotalDueWorkingDays = request.TotalDueWorkingDays;
        target.AlertOpensExcursionReview = request.AlertOpensExcursionReview;
    }

    private async Task<OosTemplateDto> ReadAsync(int sectionId, OosInvestigationKind kind, CancellationToken ct = default) =>
        await db.OosInvestigationTemplates.AsNoTracking()
            .Where(t => t.SectionId == sectionId && t.Kind == kind)
            .Select(t => new OosTemplateDto(t.Version, t.PhaseOneDueWorkingDays, t.TotalDueWorkingDays,
                t.AlertOpensExcursionReview, db.OosChecklistItems.Where(i => i.TemplateId == t.Id)
                    .OrderBy(i => i.Phase).ThenBy(i => i.Order).ThenBy(i => i.Id)
                    .Select(i => new OosTemplateItem(i.Key, i.Phase, i.Order, i.Text, i.IsActive)).ToList()))
            .AsSingleQuery().SingleOrDefaultAsync(ct) ?? new(0, null, null, false, []);

    public async Task<PagedResult<OosCategoryDto>> ListAsync(int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        if (!await permissions.UserHasPermissionAsync(db.CurrentUserId ?? 0, PermissionConstants.OosManage, ct))
            throw new CodedAccessDeniedException(MessageCodes.OosCategoryAccessDenied, "You do not have permission to edit OOS cause categories.");

        var (number, size) = Paging.Normalize(page, pageSize, defaultPageSize: 50);
        var query = QueryCategories();
        var count = await query.CountAsync(ct);
        var offset = (long)(number - 1) * size;
        var items = offset >= count ? [] : await query.OrderBy(c => c.Name).ThenBy(c => c.Id)
            .Skip((int)offset).Take(size).ToListAsync(ct);
        return new() { Items = items, Page = number, PageSize = size, TotalCount = count };
    }

    public Task<OosCategoryDto> SaveAsync(int? categoryId, SaveOosCategoryRequest request, int userId, CancellationToken ct = default) =>
        UnitOfWork.RunAsync(db, async () =>
        {
            if (!await permissions.UserHasPermissionAsync(userId, PermissionConstants.OosManage, ct) ||
                !await permissions.UserHasPermissionAsync(userId, PermissionConstants.MasterDataManage, ct))
                throw new CodedAccessDeniedException(MessageCodes.OosCategoryAccessDenied, "You do not have permission to edit OOS cause categories.");

            var name = request.Name?.Trim();
            var reason = request.Reason?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > 200 || string.IsNullOrEmpty(reason) || reason.Length > 4000)
                throw InvalidCategory();

            OosCauseCategory row;
            if (categoryId is int id)
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""SELECT 1 FROM "OosCauseCategories" WHERE "Id" = {id} FOR UPDATE""", ct);
                var current = await QueryCategories().SingleOrDefaultAsync(c => c.Id == id, ct)
                    ?? throw new BusinessRuleException(MessageCodes.OosCategoryNotFound, "The OOS cause category was not found.");
                if (db.ExpectedVersion is uint expected && current.Version != expected)
                    throw VersionConflict();
                row = await db.OosCauseCategories.SingleAsync(c => c.Id == id, ct);
                if (row.IsNoCauseFound && (row.Name != name || !request.IsActive))
                    throw new BusinessRuleException(MessageCodes.OosCategoryFixed, "The No cause found category cannot be renamed or deactivated.");
            }
            else
            {
                row = new OosCauseCategory();
                db.OosCauseCategories.Add(row);
            }

            if (await db.OosCauseCategories.AnyAsync(c => c.Name == name && c.Id != categoryId, ct))
                throw InvalidCategory();

            row.Name = name;
            row.IsActive = request.IsActive;
            db.CurrentUserId = userId;
            var previousReason = db.CurrentReason;
            db.CurrentReason = reason;
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException exception) when (exception.InnerException is System.Data.Common.DbException { SqlState: "23505" })
            {
                throw InvalidCategory();
            }
            finally
            {
                db.CurrentReason = previousReason;
            }
            return await QueryCategories().SingleAsync(c => c.Id == row.Id, ct);
        }, ct);

    private IQueryable<OosCategoryDto> QueryCategories() => db.Database.SqlQuery<OosCategoryDto>(
        $"""SELECT "Id", "Name", "IsActive", "IsNoCauseFound", xmin::text::bigint AS "Version" FROM "OosCauseCategories" """);

    private async Task EnsureConfigureAsync(int sectionId, int userId, CancellationToken ct = default)
    {
        if (!await permissions.UserHasPermissionAsync(userId, PermissionConstants.OosManage, ct))
            throw new CodedAccessDeniedException(MessageCodes.OosSectionAccessDenied, "You do not have access to this laboratory section.");
        try
        {
            await sectionScope.EnsureSectionsAccessAsync(userId, [sectionId], ct, SectionAccess.Configure);
        }
        catch (UnauthorizedAccessException)
        {
            throw new CodedAccessDeniedException(MessageCodes.OosSectionAccessDenied, "You do not have access to this laboratory section.");
        }
        if (!await db.DocumentSections.AsNoTracking().AnyAsync(s => s.Id == sectionId, ct))
            throw new NotFoundException(MessageCodes.OosSectionNotFound, "The laboratory section was not found.");
    }

    private static BusinessRuleException InvalidTemplate() =>
        new(MessageCodes.OosTemplateInvalid, "Enter valid checklist items and working-day settings.");

    private static BusinessRuleException InvalidCategory() =>
        new(MessageCodes.OosCategoryInvalid, "Enter a unique category name and a reason for the change.");

    internal static BusinessRuleException VersionConflict() =>
        new(MessageCodes.OosVersionConflict, "This record changed. Reload it and try again.");
}
