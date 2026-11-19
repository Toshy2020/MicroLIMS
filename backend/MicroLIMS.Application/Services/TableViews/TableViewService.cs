using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.TableViews;

// Saved table views: a user's own, and those shared with a laboratory section.
//
// A view holds settings only (filters, sort, columns, density, page size), never rows, so nothing here
// widens what anyone may read. Visibility follows section membership (a membership without a section
// covers every section of its department, as UserSectionScopeService defines it): the viewer's own
// personal views, and the section views of sections the viewer belongs to. A view the caller cannot see
// does not exist for them: NotFoundException (404), never a refusal that would confirm it is there.
// Every change is audited by MicroLimsDbContext in the same transaction as the change.
public sealed class TableViewService
{
    public const int MaxPersonalViewsPerTable = 20;
    public const int MaxNameLength = 60;
    public const int MaxStateBytes = 8 * 1024;

    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly PermissionService _permissions;
    private readonly TimeProvider _time;

    public TableViewService(IMicroLimsDbContext db, IUserSectionScopeService scope, PermissionService permissions, TimeProvider? timeProvider = null)
    {
        _db = db;
        _scope = scope;
        _permissions = permissions;
        _time = timeProvider ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<TableViewDto>> ListAsync(int userId, string tableKey, CancellationToken ct = default)
    {
        EnsureKnownTable(tableKey);
        var memberSections = await _scope.GetMemberSectionIdsAsync(userId, ct);
        var holdsShare = await _permissions.UserHasPermissionAsync(userId, PermissionConstants.TableViewsShare, ct);
        var defaultViewId = await _db.UserTableDefaults.AsNoTracking()
            .Where(d => d.UserId == userId && d.TableKey == tableKey)
            .Select(d => (int?)d.TableViewId)
            .SingleOrDefaultAsync(ct);

        var rows = await VisibleTo(userId, memberSections)
            .Where(v => v.TableKey == tableKey)
            .OrderBy(v => v.Scope).ThenBy(v => v.Name).ThenBy(v => v.Id)
            .Select(v => new ViewRow(v.Id, v.TableKey, v.Name, v.Scope, v.SectionId,
                v.Section != null ? v.Section.Name : null, v.StateJson, v.OwnerUserId,
                v.Owner != null ? v.Owner.FullName : string.Empty))
            .ToListAsync(ct);

        return rows.Select(r => ToDto(r, userId, holdsShare, memberSections, defaultViewId)).ToList();
    }

    public async Task<TableViewDto> CreateAsync(
        int userId, string tableKey, string name, string stateJson, TableViewScope scope, int? sectionId, CancellationToken ct = default)
    {
        EnsureKnownTable(tableKey);
        var cleanName = CleanName(name);
        ValidateState(stateJson);

        if (scope == TableViewScope.Section)
        {
            if (sectionId is null)
                throw new BusinessRuleException("Choose the section to share the view with.");
            if (!await _permissions.UserHasPermissionAsync(userId, PermissionConstants.TableViewsShare, ct))
                throw new UnauthorizedAccessException("Sharing a view with a section needs the permission to share table views.");
            if (!(await _scope.GetMemberSectionIdsAsync(userId, ct)).Contains(sectionId.Value))
                throw new UnauthorizedAccessException("A view can only be shared with a section you belong to.");
            if (await _db.TableViews.AnyAsync(v => v.Scope == TableViewScope.Section && v.SectionId == sectionId
                    && v.TableKey == tableKey && v.Name == cleanName, ct))
                throw new BusinessRuleException($"A view named '{cleanName}' is already shared with this section.");
        }
        else
        {
            if (sectionId is not null)
                throw new BusinessRuleException("A personal view does not belong to a section.");
            var existing = await _db.TableViews.AsNoTracking()
                .Where(v => v.Scope == TableViewScope.Personal && v.OwnerUserId == userId && v.TableKey == tableKey)
                .Select(v => v.Name)
                .ToListAsync(ct);
            if (existing.Contains(cleanName))
                throw new BusinessRuleException($"You already have a view named '{cleanName}' for this table.");
            if (existing.Count >= MaxPersonalViewsPerTable)
                throw new BusinessRuleException($"You can keep at most {MaxPersonalViewsPerTable} personal views per table. Delete one to save another.");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var view = new TableView
        {
            OwnerUserId = userId,
            TableKey = tableKey,
            Name = cleanName,
            Scope = scope,
            SectionId = scope == TableViewScope.Section ? sectionId : null,
            StateJson = stateJson,
            CreatedAt = now,
            UpdatedAt = now,
            UpdatedByUserId = userId
        };
        _db.TableViews.Add(view);
        await SaveRefusingNameClashAsync(cleanName, ct);

        return (await ListAsync(userId, tableKey, ct)).Single(v => v.Id == view.Id);
    }

    public async Task<TableViewDto> UpdateAsync(int userId, int viewId, string? name, string? stateJson, CancellationToken ct = default)
    {
        if (name is null && stateJson is null)
            throw new BusinessRuleException("Give a new name or new settings to save.");

        var view = await LoadVisibleAsync(userId, viewId, ct);
        await EnsureCanEditAsync(userId, view, ct);

        if (name is not null)
        {
            var cleanName = CleanName(name);
            if (cleanName != view.Name)
            {
                var clash = view.Scope == TableViewScope.Section
                    ? _db.TableViews.Where(v => v.Scope == TableViewScope.Section && v.SectionId == view.SectionId)
                    : _db.TableViews.Where(v => v.Scope == TableViewScope.Personal && v.OwnerUserId == view.OwnerUserId);
                if (await clash.AnyAsync(v => v.TableKey == view.TableKey && v.Name == cleanName && v.Id != view.Id, ct))
                    throw new BusinessRuleException($"A view named '{cleanName}' already exists here.");
                view.Name = cleanName;
            }
        }
        if (stateJson is not null)
        {
            ValidateState(stateJson);
            view.StateJson = stateJson;
        }

        view.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        view.UpdatedByUserId = userId;
        await SaveRefusingNameClashAsync(view.Name, ct);

        return (await ListAsync(userId, view.TableKey, ct)).Single(v => v.Id == view.Id);
    }

    public async Task DeleteAsync(int userId, int viewId, CancellationToken ct = default)
    {
        var view = await LoadVisibleAsync(userId, viewId, ct);
        await EnsureCanEditAsync(userId, view, ct);

        // Removed explicitly (not left to the database cascade) so each user's lost default is audited.
        var defaults = await _db.UserTableDefaults.Where(d => d.TableViewId == viewId).ToListAsync(ct);
        _db.UserTableDefaults.RemoveRange(defaults);
        _db.TableViews.Remove(view);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetDefaultAsync(int userId, string tableKey, int? viewId, CancellationToken ct = default)
    {
        EnsureKnownTable(tableKey);
        var current = await _db.UserTableDefaults.SingleOrDefaultAsync(d => d.UserId == userId && d.TableKey == tableKey, ct);

        if (viewId is null)
        {
            if (current is null) return;
            _db.UserTableDefaults.Remove(current);
            await _db.SaveChangesAsync(ct);
            return;
        }

        var view = await LoadVisibleAsync(userId, viewId.Value, ct);
        if (view.TableKey != tableKey)
            throw new BusinessRuleException("That view belongs to a different table.");

        if (current is null)
            _db.UserTableDefaults.Add(new UserTableDefault { UserId = userId, TableKey = tableKey, TableViewId = view.Id });
        else if (current.TableViewId != view.Id)
            current.TableViewId = view.Id;
        else
            return;
        await _db.SaveChangesAsync(ct);
    }

    // ---- visibility and edit rights ---------------------------------------------------------------------------------

    private IQueryable<TableView> VisibleTo(int userId, IReadOnlyList<int> memberSections) =>
        _db.TableViews.AsNoTracking().Where(v =>
            (v.Scope == TableViewScope.Personal && v.OwnerUserId == userId)
            || (v.Scope == TableViewScope.Section && v.SectionId != null && memberSections.Contains(v.SectionId.Value)));

    // Tracked, for a change. A view the caller cannot see answers exactly like one that does not exist.
    private async Task<TableView> LoadVisibleAsync(int userId, int viewId, CancellationToken ct)
    {
        var view = await _db.TableViews.SingleOrDefaultAsync(v => v.Id == viewId, ct);
        if (view is not null)
        {
            if (view.Scope == TableViewScope.Personal && view.OwnerUserId == userId)
                return view;
            if (view.Scope == TableViewScope.Section && view.SectionId is { } sectionId
                && (await _scope.GetMemberSectionIdsAsync(userId, ct)).Contains(sectionId))
                return view;
        }
        throw new NotFoundException($"Table view {viewId} was not found.");
    }

    // The view is already visible to the caller. A personal view is changed only by its owner. A section view
    // is changed only by a member of that section (visibility already proved membership) who holds the share
    // permission at that moment - the creator included (user decision 2026-11-15).
    private async Task EnsureCanEditAsync(int userId, TableView view, CancellationToken ct)
    {
        if (view.Scope == TableViewScope.Personal)
        {
            if (view.OwnerUserId == userId) return;
        }
        else if (await _permissions.UserHasPermissionAsync(userId, PermissionConstants.TableViewsShare, ct))
        {
            return;
        }
        throw new UnauthorizedAccessException(view.Scope == TableViewScope.Section
            ? "Changing a view shared with a section needs the permission to share table views. Save a copy as your own view instead."
            : "You can see this view but not change it. Save a copy as your own view instead.");
    }

    // Two people saving the same name at the same moment both pass the name check; the unique index then
    // refuses the second save. Report that as the same plain message instead of a server error.
    private async Task SaveRefusingNameClashAsync(string name, CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsNameIndexViolation(ex))
        {
            throw new BusinessRuleException($"A view named '{name}' already exists here. Choose another name.");
        }
    }

    private static bool IsNameIndexViolation(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains(PersonalNameIndex, StringComparison.OrdinalIgnoreCase)
            || message.Contains(SectionNameIndex, StringComparison.OrdinalIgnoreCase);
    }

    private const string PersonalNameIndex = "IX_TableViews_Personal_Name";
    private const string SectionNameIndex = "IX_TableViews_Section_Name";

    // ---- validation -------------------------------------------------------------------------------------------------

    private static void EnsureKnownTable(string tableKey)
    {
        if (!TableKeyRegistry.IsKnown(tableKey))
            throw new BusinessRuleException("This table does not support saved views.");
    }

    private static string CleanName(string? name)
    {
        var clean = name?.Trim() ?? string.Empty;
        if (clean.Length == 0)
            throw new BusinessRuleException("A view needs a name.");
        if (clean.Length > MaxNameLength)
            throw new BusinessRuleException($"A view name can be at most {MaxNameLength} characters.");
        return clean;
    }

    // A JSON object with an integer property "v" (the state format version), at most 8 KB.
    private static void ValidateState(string? stateJson)
    {
        if (string.IsNullOrWhiteSpace(stateJson))
            throw new BusinessRuleException("The view settings are missing.");
        if (Encoding.UTF8.GetByteCount(stateJson) > MaxStateBytes)
            throw new BusinessRuleException($"The view settings are too large (at most {MaxStateBytes / 1024} KB).");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(stateJson);
        }
        catch (JsonException)
        {
            throw new BusinessRuleException("The view settings are not valid JSON.");
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("v", out var version)
                || version.ValueKind != JsonValueKind.Number
                || !version.TryGetInt32(out _))
                throw new BusinessRuleException("The view settings must be a JSON object with an integer property 'v'.");
        }
    }

    // ---- mapping ----------------------------------------------------------------------------------------------------

    private sealed record ViewRow(
        int Id, string TableKey, string Name, TableViewScope Scope, int? SectionId, string? SectionName,
        string StateJson, int OwnerUserId, string OwnerName);

    private static TableViewDto ToDto(ViewRow r, int userId, bool holdsShare, IReadOnlyList<int> memberSections, int? defaultViewId)
    {
        var isMine = r.OwnerUserId == userId;
        var canEdit = r.Scope == TableViewScope.Personal
            ? isMine
            : holdsShare && r.SectionId is { } s && memberSections.Contains(s);
        return new TableViewDto(r.Id, r.TableKey, r.Name, r.Scope.ToString(), r.SectionId, r.SectionName,
            r.StateJson, isMine, canEdit, defaultViewId == r.Id, r.OwnerName);
    }
}
