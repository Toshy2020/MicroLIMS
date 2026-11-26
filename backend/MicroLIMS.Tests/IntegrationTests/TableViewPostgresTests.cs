using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.TableViews;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Migrations;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Responses;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Saved table views: personal and section-shared settings for a registered table. A view carries settings only;
// these tests prove who may see, create, change and delete one, the limits, and that every change is audited.
[Collection("PostgresDatabaseCollection")]
public class TableViewPostgresTests
{
    private const string Table = "auditReview.reviewEntries";
    private const string OtherTable = "auditReview.reviews";
    private const string State = "{\"v\":1,\"density\":\"compact\"}";
    private static readonly DateTimeOffset FixedNow = new(2031, 3, 4, 5, 6, 7, TimeSpan.Zero);
    private static readonly string[] Sharer = { PermissionConstants.TableViewsShare };

    private readonly PostgresTestFixture _fixture;

    public TableViewPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    // ---- arrange helpers ------------------------------------------------------------------------------------------

    private sealed record Org(int DepartmentId, int SectionA, int SectionB, int OtherDepartmentId, int OtherSection);

    private async Task<Org> CreateOrgAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        await using var db = _fixture.CreateDbContext();
        var dept = new DocumentDepartment { Code = "TD" + tag, Name = "Dept " + tag };
        var other = new DocumentDepartment { Code = "TO" + tag, Name = "Other " + tag };
        db.DocumentDepartments.AddRange(dept, other);
        await db.SaveChangesAsync();
        var a = new DocumentSection { Code = "TA" + tag, Name = "Section A " + tag, DepartmentId = dept.Id };
        var b = new DocumentSection { Code = "TB" + tag, Name = "Section B " + tag, DepartmentId = dept.Id };
        var c = new DocumentSection { Code = "TC" + tag, Name = "Section C " + tag, DepartmentId = other.Id };
        db.DocumentSections.AddRange(a, b, c);
        await db.SaveChangesAsync();
        return new Org(dept.Id, a.Id, b.Id, other.Id, c.Id);
    }

    // A user whose role holds exactly the given permission codes; member of the given sections, or of the whole department.
    private async Task<int> CreateUserAsync(string[] codes, int[]? sectionIds = null, int? wholeDepartmentId = null)
    {
        var tag = Guid.NewGuid().ToString("N")[..10];
        await using var db = _fixture.CreateDbContext();
        var role = new Role { Type = RoleType.Analyst, Name = "TableView test " + tag, IsSystemRole = false, IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var permissionIds = await db.Permissions.Where(p => codes.Contains(p.Code)).Select(p => p.Id).ToListAsync();
        Assert.Equal(codes.Length, permissionIds.Count);
        foreach (var id in permissionIds)
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = id });
        var user = new User
        {
            FullName = "View User " + tag,
            Username = "tv_" + tag,
            PasswordHash = "hashed_dummy",
            RoleId = role.Id,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        foreach (var sectionId in sectionIds ?? Array.Empty<int>())
        {
            var deptId = await db.DocumentSections.Where(s => s.Id == sectionId).Select(s => s.DepartmentId).SingleAsync();
            db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, DepartmentId = deptId, SectionId = sectionId });
        }
        if (wholeDepartmentId is { } d)
            db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, DepartmentId = d, SectionId = null });
        await db.SaveChangesAsync();
        return user.Id;
    }

    private TableViewService NewService(MicroLimsDbContext db, int actingUserId, TimeProvider? clock = null)
    {
        db.CurrentUserId = actingUserId;
        return new TableViewService(db, new UserSectionScopeService(db), new PermissionService(db), clock ?? new FixedTimeProvider(FixedNow));
    }

    private async Task<TableViewDto> CreateAsync(int userId, string name, TableViewScope scope = TableViewScope.Personal,
        int? sectionId = null, string tableKey = Table, string state = State)
    {
        await using var db = _fixture.CreateDbContext();
        return await NewService(db, userId).CreateAsync(userId, tableKey, name, state, scope, sectionId);
    }

    private async Task SetDefaultAsync(int userId, string tableKey, int? viewId)
    {
        await using var db = _fixture.CreateDbContext();
        await NewService(db, userId).SetDefaultAsync(userId, tableKey, viewId);
    }

    private async Task<IReadOnlyList<TableViewDto>> ListAsync(int userId, string tableKey = Table)
    {
        await using var db = _fixture.CreateDbContext();
        return await NewService(db, userId).ListAsync(userId, tableKey);
    }

    private async Task<TableView> LoadAsync(int viewId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.TableViews.AsNoTracking().SingleAsync(v => v.Id == viewId);
    }

    private async Task<int> ViewCountAsync(Func<TableView, bool> where)
    {
        await using var db = _fixture.CreateDbContext();
        return (await db.TableViews.AsNoTracking().ToListAsync()).Count(where);
    }

    private static string StateOfBytes(int bytes)
    {
        const string head = "{\"v\":1,\"p\":\"";
        const string tail = "\"}";
        return head + new string('x', bytes - head.Length - tail.Length) + tail;
    }

    // ---- create, validate, audit ----------------------------------------------------------------------------------

    [PostgresFact]
    public async Task Create_Personal_PersistsAndAudits()
    {
        var user = await CreateUserAsync(Array.Empty<string>());

        TableViewDto dto;
        // The audit row is stamped by the DbContext's own clock, so give it the fixed one too.
        await using (var actDb = _fixture.CreateDbContext(new FixedTimeProvider(FixedNow)))
            dto = await NewService(actDb, user).CreateAsync(user, Table, "  My open items  ", State, TableViewScope.Personal, null);

        await using var assertDb = _fixture.CreateDbContext();
        var row = await assertDb.TableViews.AsNoTracking().SingleAsync(v => v.Id == dto.Id);
        Assert.Equal(user, row.OwnerUserId);
        Assert.Equal(Table, row.TableKey);
        Assert.Equal("My open items", row.Name);
        Assert.Equal(TableViewScope.Personal, row.Scope);
        Assert.Null(row.SectionId);
        Assert.Equal(1, JsonDocument.Parse(row.StateJson).RootElement.GetProperty("v").GetInt32());
        Assert.Equal("compact", JsonDocument.Parse(row.StateJson).RootElement.GetProperty("density").GetString());
        Assert.Equal(FixedNow.UtcDateTime, row.CreatedAt);
        Assert.Equal(FixedNow.UtcDateTime, row.UpdatedAt);
        Assert.Equal(DateTimeKind.Utc, row.CreatedAt.Kind);
        Assert.Equal(user, row.UpdatedByUserId);

        Assert.Equal("Personal", dto.Scope);
        Assert.True(dto.IsMine);
        Assert.True(dto.CanEdit);
        Assert.False(dto.IsDefault);
        Assert.Equal("My open items", dto.Name);

        var audit = await assertDb.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.EntityName == nameof(TableView) && a.EntityId == dto.Id.ToString() && a.Action == "Create");
        Assert.Equal(user, audit.UserId);
        Assert.Equal(FixedNow.UtcDateTime, audit.Timestamp);
        Assert.Null(audit.PreviousValue);
        Assert.Equal("My open items", JsonDocument.Parse(audit.NewValue!).RootElement.GetProperty("Name").GetString());
        Assert.Equal(Table, JsonDocument.Parse(audit.NewValue!).RootElement.GetProperty("TableKey").GetString());
    }

    [PostgresFact]
    public async Task Update_ByOwner_ChangesNameAndState_AndAudits()
    {
        var user = await CreateUserAsync(Array.Empty<string>());
        var created = await CreateAsync(user, "Before");
        var later = new FixedTimeProvider(FixedNow.AddHours(3));

        await using (var actDb = _fixture.CreateDbContext())
        {
            var dto = await NewService(actDb, user, later).UpdateAsync(user, created.Id, "After", "{\"v\":1,\"density\":\"comfortable\"}");
            Assert.Equal("After", dto.Name);
        }

        var row = await LoadAsync(created.Id);
        Assert.Equal("After", row.Name);
        Assert.Equal("comfortable", JsonDocument.Parse(row.StateJson).RootElement.GetProperty("density").GetString());
        Assert.Equal(FixedNow.UtcDateTime, row.CreatedAt);
        Assert.Equal(FixedNow.AddHours(3).UtcDateTime, row.UpdatedAt);
        Assert.Equal(user, row.UpdatedByUserId);

        await using var assertDb = _fixture.CreateDbContext();
        var audit = await assertDb.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.EntityName == nameof(TableView) && a.EntityId == created.Id.ToString() && a.Action == "Update");
        Assert.Equal(user, audit.UserId);
        Assert.Equal("Before", JsonDocument.Parse(audit.PreviousValue!).RootElement.GetProperty("Name").GetString());
        Assert.Equal("After", JsonDocument.Parse(audit.NewValue!).RootElement.GetProperty("Name").GetString());
    }

    [PostgresFact]
    public async Task Update_NothingToChange_Rejected()
    {
        var user = await CreateUserAsync(Array.Empty<string>());
        var created = await CreateAsync(user, "Same");

        await using var actDb = _fixture.CreateDbContext();
        await Assert.ThrowsAsync<BusinessRuleException>(() => NewService(actDb, user).UpdateAsync(user, created.Id, null, null));
    }

    [PostgresFact]
    public async Task Update_ToInvalidStateOrDuplicateName_Rejected_AndRowUnchanged()
    {
        var user = await CreateUserAsync(Array.Empty<string>());
        var first = await CreateAsync(user, "First");
        await CreateAsync(user, "Second");

        await using (var actDb = _fixture.CreateDbContext())
        {
            var service = NewService(actDb, user);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateAsync(user, first.Id, "Second", null));
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateAsync(user, first.Id, null, "[1]"));
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateAsync(user, first.Id, new string('n', 61), null));
        }

        var row = await LoadAsync(first.Id);
        Assert.Equal("First", row.Name);
        Assert.Equal("compact", JsonDocument.Parse(row.StateJson).RootElement.GetProperty("density").GetString());
        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a => a.EntityName == nameof(TableView) && a.EntityId == first.Id.ToString() && a.Action == "Update"));
    }

    [PostgresFact]
    public async Task Delete_ByOwner_RemovesAndAudits()
    {
        var user = await CreateUserAsync(Array.Empty<string>());
        var created = await CreateAsync(user, "Doomed");

        await using (var actDb = _fixture.CreateDbContext())
            await NewService(actDb, user).DeleteAsync(user, created.Id);

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.TableViews.AsNoTracking().AnyAsync(v => v.Id == created.Id));
        var audit = await assertDb.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.EntityName == nameof(TableView) && a.EntityId == created.Id.ToString() && a.Action == "Delete");
        Assert.Equal(user, audit.UserId);
        Assert.Equal("Doomed", JsonDocument.Parse(audit.PreviousValue!).RootElement.GetProperty("Name").GetString());
    }

    // User decision 2026-11-15: changing a section view needs the share permission at that moment, the creator included.
    [PostgresFact]
    public async Task SectionView_CreatorWhoLostSharePermission_CannotChangeOrDelete()
    {
        var org = await CreateOrgAsync();
        var creator = await CreateUserAsync(Sharer, new[] { org.SectionA });
        var view = await CreateAsync(creator, "Team view", TableViewScope.Section, org.SectionA);

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var roleId = await arrangeDb.Users.Where(u => u.Id == creator).Select(u => u.RoleId).SingleAsync();
            arrangeDb.RolePermissions.RemoveRange(await arrangeDb.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync());
            await arrangeDb.SaveChangesAsync();
        }

        Assert.False((await ListAsync(creator)).Single(v => v.Id == view.Id).CanEdit);
        await using (var actDb = _fixture.CreateDbContext())
        {
            var service = NewService(actDb, creator);
            var refused = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UpdateAsync(creator, view.Id, "Renamed", null));
            Assert.Contains("permission to share table views", refused.Message);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteAsync(creator, view.Id));
        }

        var row = await LoadAsync(view.Id);
        Assert.Equal("Team view", row.Name);
        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityName == nameof(TableView) && a.EntityId == view.Id.ToString() && (a.Action == "Update" || a.Action == "Delete")));
    }

    [PostgresFact]
    public async Task PersonalView_OwnerWithoutSharePermission_CanStillChangeIt()
    {
        var owner = await CreateUserAsync(Array.Empty<string>());
        var view = await CreateAsync(owner, "Mine");
        Assert.True((await ListAsync(owner)).Single(v => v.Id == view.Id).CanEdit);

        await using (var actDb = _fixture.CreateDbContext())
            await NewService(actDb, owner).UpdateAsync(owner, view.Id, "Mine v2", null);

        Assert.Equal("Mine v2", (await LoadAsync(view.Id)).Name);
    }

    // Two saves of the same name at the same moment both pass the name check; the unique index refuses the
    // second. The interceptor commits a clashing view from another connection just before this save runs.
    [PostgresFact]
    public async Task SameNameSavedAtTheSameMoment_SecondGetsPlainMessage_NotServerError()
    {
        var user = await CreateUserAsync(Array.Empty<string>());
        var racer = new ClashingViewInterceptor(_fixture, user, Table, "Race");

        await using (var actDb = _fixture.CreateDbContext(racer))
        {
            var refused = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                NewService(actDb, user).CreateAsync(user, Table, "Race", State, TableViewScope.Personal, null));
            Assert.Equal("A view named 'Race' already exists here. Choose another name.", refused.Message);
        }

        Assert.True(racer.Inserted);
        Assert.Equal(1, await ViewCountAsync(v => v.OwnerUserId == user && v.Name == "Race"));
        await using var assertDb = _fixture.CreateDbContext();
        Assert.Equal(1, await assertDb.AuditLogs.AsNoTracking().CountAsync(a =>
            a.EntityName == nameof(TableView) && a.Action == "Create" && a.UserId == user));
    }

    private sealed class ClashingViewInterceptor : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        private readonly PostgresTestFixture _fixture;
        private readonly int _userId;
        private readonly string _tableKey;
        private readonly string _name;
        public bool Inserted { get; private set; }

        public ClashingViewInterceptor(PostgresTestFixture fixture, int userId, string tableKey, string name)
        {
            _fixture = fixture;
            _userId = userId;
            _tableKey = tableKey;
            _name = name;
        }

        public override async ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
            Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!Inserted)
            {
                Inserted = true;
                await using var other = _fixture.CreateDbContext();
                other.CurrentUserId = _userId;
                var now = FixedNow.UtcDateTime;
                other.TableViews.Add(new TableView
                {
                    OwnerUserId = _userId, TableKey = _tableKey, Name = _name, Scope = TableViewScope.Personal,
                    StateJson = State, CreatedAt = now, UpdatedAt = now, UpdatedByUserId = _userId
                });
                await other.SaveChangesAsync(cancellationToken);
            }
            return result;
        }
    }

    [PostgresFact]
    public async Task DuplicateNameSameOwnerAndTable_Rejected()
    {
        var user = await CreateUserAsync(Array.Empty<string>());
        await CreateAsync(user, "Mine");

        await using (var actDb = _fixture.CreateDbContext())
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                NewService(actDb, user).CreateAsync(user, Table, "Mine", State, TableViewScope.Personal, null));

        Assert.Equal(1, await ViewCountAsync(v => v.OwnerUserId == user));

        // The same name on another table is a different view.
        await CreateAsync(user, "Mine", tableKey: OtherTable);
        Assert.Equal(2, await ViewCountAsync(v => v.OwnerUserId == user));
    }

    [PostgresFact]
    public async Task SameNameDifferentUser_Allowed()
    {
        var a = await CreateUserAsync(Array.Empty<string>());
        var b = await CreateUserAsync(Array.Empty<string>());

        await CreateAsync(a, "Shared name");
        await CreateAsync(b, "Shared name");

        Assert.Equal(1, await ViewCountAsync(v => v.OwnerUserId == a));
        Assert.Equal(1, await ViewCountAsync(v => v.OwnerUserId == b));
    }

    [PostgresFact]
    public async Task TwentyFirstPersonalView_Rejected()
    {
        var user = await CreateUserAsync(Array.Empty<string>());
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            for (var i = 0; i < 19; i++)
                arrangeDb.TableViews.Add(new TableView
                {
                    OwnerUserId = user, TableKey = Table, Name = "View " + i, Scope = TableViewScope.Personal,
                    StateJson = State, CreatedAt = FixedNow.UtcDateTime, UpdatedAt = FixedNow.UtcDateTime, UpdatedByUserId = user
                });
            await arrangeDb.SaveChangesAsync();
        }

        // The 20th is accepted.
        await CreateAsync(user, "View 19");
        Assert.Equal(20, await ViewCountAsync(v => v.OwnerUserId == user && v.TableKey == Table));

        // The 21st is not; nothing is written.
        await using (var actDb = _fixture.CreateDbContext())
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                NewService(actDb, user).CreateAsync(user, Table, "View 20", State, TableViewScope.Personal, null));
        Assert.Equal(20, await ViewCountAsync(v => v.OwnerUserId == user && v.TableKey == Table));

        // The cap is per table.
        await CreateAsync(user, "View 20", tableKey: OtherTable);
    }

    [PostgresFact]
    public async Task UnknownTableKey_Rejected()
    {
        var user = await CreateUserAsync(Array.Empty<string>());

        await using (var actDb = _fixture.CreateDbContext())
        {
            var service = NewService(actDb, user);
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.CreateAsync(user, "no.such.table", "Name", State, TableViewScope.Personal, null));
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.ListAsync(user, "no.such.table"));
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.SetDefaultAsync(user, "no.such.table", null));
        }

        Assert.Equal(0, await ViewCountAsync(v => v.OwnerUserId == user));
    }

    [PostgresFact]
    public async Task StateOver8Kb_Rejected()
    {
        var user = await CreateUserAsync(Array.Empty<string>());

        // Exactly 8 KB is accepted; one byte more is not.
        await CreateAsync(user, "Exactly 8 KB", state: StateOfBytes(8192));

        await using (var actDb = _fixture.CreateDbContext())
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                NewService(actDb, user).CreateAsync(user, Table, "Over", StateOfBytes(8193), TableViewScope.Personal, null));

        Assert.Equal(new[] { "Exactly 8 KB" }, (await ListAsync(user)).Select(v => v.Name).ToArray());
    }

    [PostgresFact]
    public async Task StateNotJsonObject_Rejected()
    {
        var user = await CreateUserAsync(Array.Empty<string>());
        var bad = new[]
        {
            "", "   ", "{not json", "[]", "[{\"v\":1}]", "\"text\"", "42", "null", "true",
            "{}",                    // no v
            "{\"version\":1}",       // wrong property
            "{\"v\":\"1\"}",         // v is a string
            "{\"v\":1.5}",           // v is not an integer
            "{\"v\":null}", "{\"v\":true}", "{\"v\":{}}"
        };

        await using (var actDb = _fixture.CreateDbContext())
        {
            var service = NewService(actDb, user);
            foreach (var state in bad)
                await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    service.CreateAsync(user, Table, "Bad", state, TableViewScope.Personal, null));
        }

        Assert.Equal(0, await ViewCountAsync(v => v.OwnerUserId == user));
    }

    [PostgresFact]
    public async Task Name61Chars_Rejected_60Accepted_BlankRejected()
    {
        var user = await CreateUserAsync(Array.Empty<string>());

        await CreateAsync(user, new string('n', 60));

        await using (var actDb = _fixture.CreateDbContext())
        {
            var service = NewService(actDb, user);
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.CreateAsync(user, Table, new string('n', 61), State, TableViewScope.Personal, null));
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.CreateAsync(user, Table, "   ", State, TableViewScope.Personal, null));
        }

        Assert.Equal(1, await ViewCountAsync(v => v.OwnerUserId == user));
    }

    [PostgresFact]
    public async Task ScopeAndSection_MustAgree()
    {
        var org = await CreateOrgAsync();
        var head = await CreateUserAsync(Sharer, new[] { org.SectionA });

        await using (var actDb = _fixture.CreateDbContext())
        {
            var service = NewService(actDb, head);
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.CreateAsync(head, Table, "No section", State, TableViewScope.Section, null));
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.CreateAsync(head, Table, "Personal with section", State, TableViewScope.Personal, org.SectionA));
        }

        Assert.Equal(0, await ViewCountAsync(v => v.OwnerUserId == head));
    }

    // ---- who sees what --------------------------------------------------------------------------------------------

    [PostgresFact]
    public async Task List_ReturnsMineAndMySectionViews_NotOtherUsersPersonal_NotOtherSections()
    {
        var org = await CreateOrgAsync();
        var me = await CreateUserAsync(Array.Empty<string>(), new[] { org.SectionA });
        var colleague = await CreateUserAsync(Sharer, new[] { org.SectionA });
        var outsider = await CreateUserAsync(Sharer, new[] { org.SectionB });

        var mine = await CreateAsync(me, "Mine");
        var mineOtherTable = await CreateAsync(me, "Mine elsewhere", tableKey: OtherTable);
        var colleaguePersonal = await CreateAsync(colleague, "Colleague private");
        var sectionA = await CreateAsync(colleague, "Section A view", TableViewScope.Section, org.SectionA);
        var sectionB = await CreateAsync(outsider, "Section B view", TableViewScope.Section, org.SectionB);
        var sectionAOtherTable = await CreateAsync(colleague, "Section A elsewhere", TableViewScope.Section, org.SectionA, OtherTable);

        var listed = await ListAsync(me);

        Assert.Equal(new[] { mine.Id, sectionA.Id }.OrderBy(i => i), listed.Select(v => v.Id).OrderBy(i => i));
        var shared = listed.Single(v => v.Id == sectionA.Id);
        Assert.Equal("Section", shared.Scope);
        Assert.Equal(org.SectionA, shared.SectionId);
        Assert.StartsWith("Section A ", shared.SectionName);
        Assert.False(shared.IsMine);
        Assert.False(shared.CanEdit);
        Assert.StartsWith("View User ", shared.OwnerName);
        Assert.True(listed.Single(v => v.Id == mine.Id).IsMine);

        var otherTable = await ListAsync(me, OtherTable);
        Assert.Equal(new[] { mineOtherTable.Id, sectionAOtherTable.Id }.OrderBy(i => i), otherTable.Select(v => v.Id).OrderBy(i => i));
        Assert.DoesNotContain(listed, v => v.Id == colleaguePersonal.Id || v.Id == sectionB.Id);
    }

    [PostgresFact]
    public async Task DepartmentWideMember_SeesViewsOfEverySectionInDepartment()
    {
        var org = await CreateOrgAsync();
        var sharerA = await CreateUserAsync(Sharer, new[] { org.SectionA });
        var sharerB = await CreateUserAsync(Sharer, new[] { org.SectionB });
        var sharerOther = await CreateUserAsync(Sharer, new[] { org.OtherSection });
        var wide = await CreateUserAsync(Sharer, wholeDepartmentId: org.DepartmentId);

        var viewA = await CreateAsync(sharerA, "A", TableViewScope.Section, org.SectionA);
        var viewB = await CreateAsync(sharerB, "B", TableViewScope.Section, org.SectionB);
        var viewOther = await CreateAsync(sharerOther, "Other department", TableViewScope.Section, org.OtherSection);

        var listed = await ListAsync(wide);
        Assert.Equal(new[] { viewA.Id, viewB.Id }.OrderBy(i => i), listed.Select(v => v.Id).OrderBy(i => i));
        Assert.DoesNotContain(listed, v => v.Id == viewOther.Id);

        // A department-wide Section Head may also share into any section of the department, and edit what is shared there.
        var fromWide = await CreateAsync(wide, "From the head", TableViewScope.Section, org.SectionB);
        Assert.Equal(org.SectionB, fromWide.SectionId);
        await using (var actDb = _fixture.CreateDbContext())
            await NewService(actDb, wide).UpdateAsync(wide, viewA.Id, "A renamed", null);
        Assert.Equal("A renamed", (await LoadAsync(viewA.Id)).Name);
    }

    [PostgresFact]
    public async Task CreateSectionView_WithoutPermission_Throws()
    {
        var org = await CreateOrgAsync();
        var plain = await CreateUserAsync(Array.Empty<string>(), new[] { org.SectionA });

        await using (var actDb = _fixture.CreateDbContext())
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                NewService(actDb, plain).CreateAsync(plain, Table, "Shared", State, TableViewScope.Section, org.SectionA));

        Assert.Equal(0, await ViewCountAsync(v => v.OwnerUserId == plain));
        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a => a.EntityName == nameof(TableView) && a.UserId == plain));
    }

    [PostgresFact]
    public async Task CreateSectionView_ForSectionIAmNotIn_Throws()
    {
        var org = await CreateOrgAsync();
        var head = await CreateUserAsync(Sharer, new[] { org.SectionA });

        await using (var actDb = _fixture.CreateDbContext())
        {
            var service = NewService(actDb, head);
            // A section of the same department that the user does not belong to, and one of another department.
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                service.CreateAsync(head, Table, "Shared", State, TableViewScope.Section, org.SectionB));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                service.CreateAsync(head, Table, "Shared", State, TableViewScope.Section, org.OtherSection));
        }

        Assert.Equal(0, await ViewCountAsync(v => v.OwnerUserId == head));
    }

    [PostgresFact]
    public async Task CreateSectionView_AsQcManagerMember_Succeeds()
    {
        var org = await CreateOrgAsync();
        var qcManager = await CreateUserAsync(Sharer, new[] { org.SectionA });

        var dto = await CreateAsync(qcManager, "Weekly review", TableViewScope.Section, org.SectionA);

        Assert.Equal("Section", dto.Scope);
        Assert.Equal(org.SectionA, dto.SectionId);
        Assert.StartsWith("Section A ", dto.SectionName);
        Assert.True(dto.IsMine);
        Assert.True(dto.CanEdit);

        var row = await LoadAsync(dto.Id);
        Assert.Equal(TableViewScope.Section, row.Scope);
        Assert.Equal(org.SectionA, row.SectionId);
        Assert.Equal(qcManager, row.OwnerUserId);

        await using var assertDb = _fixture.CreateDbContext();
        var audit = await assertDb.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.EntityName == nameof(TableView) && a.EntityId == dto.Id.ToString() && a.Action == "Create");
        Assert.Equal(qcManager, audit.UserId);
        Assert.Equal(org.SectionA, JsonDocument.Parse(audit.NewValue!).RootElement.GetProperty("SectionId").GetInt32());

        // The same name in another section, and the same name personally, are different views.
        var sharerB = await CreateUserAsync(Sharer, new[] { org.SectionB });
        await CreateAsync(sharerB, "Weekly review", TableViewScope.Section, org.SectionB);
        await CreateAsync(qcManager, "Weekly review");
        // But not twice in one section, whoever creates it.
        var sharerA2 = await CreateUserAsync(Sharer, new[] { org.SectionA });
        await using var dupDb = _fixture.CreateDbContext();
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            NewService(dupDb, sharerA2).CreateAsync(sharerA2, Table, "Weekly review", State, TableViewScope.Section, org.SectionA));
    }

    // ---- change and delete ----------------------------------------------------------------------------------------

    [PostgresFact]
    public async Task UpdateOrDelete_OthersPersonalView_Returns404()
    {
        var owner = await CreateUserAsync(Array.Empty<string>());
        var stranger = await CreateUserAsync(Sharer);
        var view = await CreateAsync(owner, "Private");

        await using (var actDb = _fixture.CreateDbContext())
        {
            var service = NewService(actDb, stranger);
            // NotFoundException is answered 404; a 403 would confirm the view exists.
            await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(stranger, view.Id, "Hijacked", null));
            await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(stranger, view.Id));
            await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(stranger, int.MaxValue, "Nothing", null));
        }

        var row = await LoadAsync(view.Id);
        Assert.Equal("Private", row.Name);
        Assert.Equal(owner, row.UpdatedByUserId);
        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a => a.EntityName == nameof(TableView) && a.UserId == stranger));
    }

    [PostgresFact]
    public async Task UpdateOrDelete_SectionViewOfAnotherSection_Returns404_EvenForASharer()
    {
        var org = await CreateOrgAsync();
        var sharerA = await CreateUserAsync(Sharer, new[] { org.SectionA });
        var sharerB = await CreateUserAsync(Sharer, new[] { org.SectionB });
        var view = await CreateAsync(sharerA, "A only", TableViewScope.Section, org.SectionA);

        await using (var actDb = _fixture.CreateDbContext())
        {
            var service = NewService(actDb, sharerB);
            await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(sharerB, view.Id, "Hijacked", null));
            await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(sharerB, view.Id));
        }

        Assert.Equal("A only", (await LoadAsync(view.Id)).Name);
    }

    [PostgresFact]
    public async Task Update_SectionView_ByAnotherSharerInSection_Succeeds()
    {
        var org = await CreateOrgAsync();
        var owner = await CreateUserAsync(Sharer, new[] { org.SectionA });
        var otherSharer = await CreateUserAsync(Sharer, new[] { org.SectionA });
        var view = await CreateAsync(owner, "Team view", TableViewScope.Section, org.SectionA);

        var listedForOther = (await ListAsync(otherSharer)).Single(v => v.Id == view.Id);
        Assert.True(listedForOther.CanEdit);
        Assert.False(listedForOther.IsMine);

        await using (var actDb = _fixture.CreateDbContext())
            await NewService(actDb, otherSharer).UpdateAsync(otherSharer, view.Id, "Team view v2", "{\"v\":1,\"density\":\"comfortable\"}");

        var row = await LoadAsync(view.Id);
        Assert.Equal("Team view v2", row.Name);
        Assert.Equal(owner, row.OwnerUserId);
        Assert.Equal(otherSharer, row.UpdatedByUserId);
        Assert.Equal(TableViewScope.Section, row.Scope);
        Assert.Equal(org.SectionA, row.SectionId);

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var audit = await assertDb.AuditLogs.AsNoTracking()
                .SingleAsync(a => a.EntityName == nameof(TableView) && a.EntityId == view.Id.ToString() && a.Action == "Update");
            Assert.Equal(otherSharer, audit.UserId);
        }

        // And may delete it.
        await using (var delDb = _fixture.CreateDbContext())
            await NewService(delDb, otherSharer).DeleteAsync(otherSharer, view.Id);
        Assert.Equal(0, await ViewCountAsync(v => v.Id == view.Id));
    }

    [PostgresFact]
    public async Task Update_SectionView_ByPlainMember_Throws()
    {
        var org = await CreateOrgAsync();
        var owner = await CreateUserAsync(Sharer, new[] { org.SectionA });
        var member = await CreateUserAsync(Array.Empty<string>(), new[] { org.SectionA });
        var view = await CreateAsync(owner, "Team view", TableViewScope.Section, org.SectionA);

        await using (var actDb = _fixture.CreateDbContext())
        {
            var service = NewService(actDb, member);
            // The member can see the view, so this is a refusal (403), not a 404.
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UpdateAsync(member, view.Id, "Mine now", null));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteAsync(member, view.Id));
        }

        var row = await LoadAsync(view.Id);
        Assert.Equal("Team view", row.Name);
        Assert.Equal(owner, row.UpdatedByUserId);
        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a => a.EntityName == nameof(TableView) && a.UserId == member));
    }

    // ---- defaults -------------------------------------------------------------------------------------------------

    [PostgresFact]
    public async Task SetDefault_ToViewICannotSee_Throws()
    {
        var org = await CreateOrgAsync();
        var owner = await CreateUserAsync(Sharer, new[] { org.SectionA });
        var outsider = await CreateUserAsync(Array.Empty<string>(), new[] { org.SectionB });
        var sectionView = await CreateAsync(owner, "A only", TableViewScope.Section, org.SectionA);
        var privateView = await CreateAsync(owner, "Private");

        await using (var actDb = _fixture.CreateDbContext())
        {
            var service = NewService(actDb, outsider);
            await Assert.ThrowsAsync<NotFoundException>(() => service.SetDefaultAsync(outsider, Table, sectionView.Id));
            await Assert.ThrowsAsync<NotFoundException>(() => service.SetDefaultAsync(outsider, Table, privateView.Id));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.UserTableDefaults.AsNoTracking().AnyAsync(d => d.UserId == outsider));
    }

    [PostgresFact]
    public async Task SetDefault_Sets_Replaces_Clears_AndAudits()
    {
        var user = await CreateUserAsync(Array.Empty<string>());
        var first = await CreateAsync(user, "First");
        var second = await CreateAsync(user, "Second");
        var elsewhere = await CreateAsync(user, "Elsewhere", tableKey: OtherTable);

        await SetDefaultAsync(user, Table, first.Id);

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var row = await assertDb.UserTableDefaults.AsNoTracking().SingleAsync(d => d.UserId == user && d.TableKey == Table);
            Assert.Equal(first.Id, row.TableViewId);
            Assert.True(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a => a.EntityName == nameof(UserTableDefault) && a.Action == "Create" && a.UserId == user));
        }
        var listed = await ListAsync(user);
        Assert.True(listed.Single(v => v.Id == first.Id).IsDefault);
        Assert.False(listed.Single(v => v.Id == second.Id).IsDefault);

        // Replace: still one row for this user and table.
        await SetDefaultAsync(user, Table, second.Id);
        await using (var assertDb = _fixture.CreateDbContext())
        {
            var row = await assertDb.UserTableDefaults.AsNoTracking().SingleAsync(d => d.UserId == user && d.TableKey == Table);
            Assert.Equal(second.Id, row.TableViewId);
            Assert.True(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a => a.EntityName == nameof(UserTableDefault) && a.Action == "Update" && a.UserId == user));
        }

        // A view of another table cannot be the default here.
        await using (var actDb = _fixture.CreateDbContext())
            await Assert.ThrowsAsync<BusinessRuleException>(() => NewService(actDb, user).SetDefaultAsync(user, Table, elsewhere.Id));

        // Clear.
        await SetDefaultAsync(user, Table, null);
        await using (var assertDb = _fixture.CreateDbContext())
        {
            Assert.False(await assertDb.UserTableDefaults.AsNoTracking().AnyAsync(d => d.UserId == user));
            Assert.True(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a => a.EntityName == nameof(UserTableDefault) && a.Action == "Delete" && a.UserId == user));
        }
    }

    [PostgresFact]
    public async Task DeleteView_RemovesDefaultsPointingAtIt()
    {
        var org = await CreateOrgAsync();
        var owner = await CreateUserAsync(Sharer, new[] { org.SectionA });
        var member = await CreateUserAsync(Array.Empty<string>(), new[] { org.SectionA });
        var view = await CreateAsync(owner, "Team view", TableViewScope.Section, org.SectionA);
        var survivor = await CreateAsync(member, "Survivor");
        var otherTableView = await CreateAsync(member, "Other table view", tableKey: OtherTable);

        await SetDefaultAsync(owner, Table, view.Id);
        await SetDefaultAsync(member, Table, view.Id);
        await SetDefaultAsync(member, OtherTable, otherTableView.Id);

        await using (var actDb = _fixture.CreateDbContext())
            await NewService(actDb, owner).DeleteAsync(owner, view.Id);

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.TableViews.AsNoTracking().AnyAsync(v => v.Id == view.Id));
        Assert.False(await assertDb.UserTableDefaults.AsNoTracking().AnyAsync(d => d.TableViewId == view.Id));
        // Defaults for other tables are untouched, and the member's own view survives.
        Assert.True(await assertDb.UserTableDefaults.AsNoTracking().AnyAsync(d => d.UserId == member && d.TableKey == OtherTable));
        Assert.True(await assertDb.TableViews.AsNoTracking().AnyAsync(v => v.Id == survivor.Id));
        // The removal of each default is audited, not a silent cascade.
        var deletes = await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(UserTableDefault) && a.Action == "Delete" && a.UserId == owner)
            .ToListAsync();
        Assert.Equal(2, deletes.Count);
        Assert.All(deletes, a => Assert.Equal(view.Id, JsonDocument.Parse(a.PreviousValue!).RootElement.GetProperty("TableViewId").GetInt32()));
    }

    // ---- HTTP -----------------------------------------------------------------------------------------------------

    private static ControllerContext ContextFor(int userId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "Test"))
        }
    };

    [PostgresFact]
    public async Task Controller_ActsOnTheCallersId_AndAViewOfAnotherSectionIs404()
    {
        var org = await CreateOrgAsync();
        var sectionAHead = await CreateUserAsync(Sharer, new[] { org.SectionA });
        var sectionBUser = await CreateUserAsync(Sharer, new[] { org.SectionB });
        var view = await CreateAsync(sectionAHead, "Section A view", TableViewScope.Section, org.SectionA);

        await using (var actDb = _fixture.CreateDbContext())
        {
            var asB = new TableViewsController(NewService(actDb, sectionBUser)) { ControllerContext = ContextFor(sectionBUser) };

            // List: B sees nothing of A's.
            var list = Assert.IsType<OkObjectResult>(await asB.List(Table, default));
            Assert.Empty(Assert.IsType<ApiResponse<IReadOnlyList<TableViewDto>>>(list.Value).Data!);

            // PUT / DELETE / default on A's view: NotFoundException, which ExceptionMiddleware answers 404.
            await Assert.ThrowsAsync<NotFoundException>(() => asB.Update(view.Id, new UpdateTableViewRequest("x", null), default));
            await Assert.ThrowsAsync<NotFoundException>(() => asB.Delete(view.Id, default));
            await Assert.ThrowsAsync<NotFoundException>(() => asB.SetDefault(new SetDefaultTableViewRequest(Table, view.Id), default));

            // Create goes to the caller: there is no user id in the body.
            var created = Assert.IsType<OkObjectResult>(await asB.Create(new CreateTableViewRequest(Table, "B's view", State, "Personal", null), default));
            var dto = Assert.IsType<ApiResponse<TableViewDto>>(created.Value).Data!;
            Assert.Equal(sectionBUser, (await LoadAsync(dto.Id)).OwnerUserId);

            // A scope that is neither Personal nor Section is a bad request.
            await Assert.ThrowsAsync<BusinessRuleException>(() => asB.Create(new CreateTableViewRequest(Table, "x", State, "Everyone", null), default));
        }

        Assert.Equal("Section A view", (await LoadAsync(view.Id)).Name);
    }

    [PostgresFact]
    public void Controller_RequiresSignIn_AndHasNoAnonymousAction()
    {
        Assert.NotEmpty(typeof(TableViewsController).GetCustomAttributes(typeof(AuthorizeAttribute), true));
        foreach (var action in typeof(TableViewsController).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
            Assert.Empty(action.GetCustomAttributes(typeof(AllowAnonymousAttribute), true));
    }

    // ---- migration ------------------------------------------------------------------------------------------------

    [PostgresFact]
    public async Task Migration_GrantsShareToSectionHeadAndQcManager_ExportToSectionHeadReviewerQcManager()
    {
        await using var db = _fixture.CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync();

        // The database as it was before this migration: neither code granted nor present.
        await db.Database.ExecuteSqlRawAsync(
            @"DELETE FROM ""RolePermissions"" WHERE ""PermissionId"" IN (SELECT ""Id"" FROM ""Permissions"" WHERE ""Code"" IN ('TableViews.Share', 'Data.Export'));
              DELETE FROM ""Permissions"" WHERE ""Code"" IN ('TableViews.Share', 'Data.Export');");

        var tag = Guid.NewGuid().ToString("N")[..8];
        Role NewRole(RoleType type, bool system) => new() { Type = type, Name = $"{type} {(system ? "system" : "custom")} {tag}", IsSystemRole = system, IsActive = true };
        var sectionHead = await db.Roles.SingleAsync(r => r.IsSystemRole && r.Type == RoleType.SectionHead);
        var reviewer = NewRole(RoleType.Reviewer, true);
        var qcManager = NewRole(RoleType.QcManager, true);
        var analyst = NewRole(RoleType.Analyst, true);
        var qa = NewRole(RoleType.Qa, true);
        var customHead = NewRole(RoleType.SectionHead, false);
        db.Roles.AddRange(reviewer, qcManager, analyst, qa, customHead);
        await db.SaveChangesAsync();

        foreach (var op in new AddTableViews().UpOperations.OfType<SqlOperation>())
            await db.Database.ExecuteSqlRawAsync(op.Sql);

        async Task<List<string>> CodesOf(Role role) => await db.RolePermissions.AsNoTracking()
            .Where(rp => rp.RoleId == role.Id && (rp.Permission!.Code == PermissionConstants.TableViewsShare || rp.Permission!.Code == PermissionConstants.DataExport))
            .Select(rp => rp.Permission!.Code).OrderBy(c => c).ToListAsync();

        var both = new[] { PermissionConstants.DataExport, PermissionConstants.TableViewsShare };
        Assert.Equal(both, await CodesOf(sectionHead));
        Assert.Equal(both, await CodesOf(qcManager));
        Assert.Equal(new[] { PermissionConstants.DataExport }, await CodesOf(reviewer));
        Assert.Empty(await CodesOf(analyst));
        Assert.Empty(await CodesOf(qa));
        Assert.Empty(await CodesOf(customHead));

        var permissions = await db.Permissions.AsNoTracking().Where(p => p.Code == PermissionConstants.TableViewsShare || p.Code == PermissionConstants.DataExport).ToListAsync();
        Assert.True(permissions.Single(p => p.Code == PermissionConstants.TableViewsShare).IsEnforced);
        Assert.False(permissions.Single(p => p.Code == PermissionConstants.DataExport).IsEnforced);

        // The grant bumps PermissionVersion (live sessions pick it up), once per migration step that changed the role.
        Assert.Equal(3, (await db.Roles.AsNoTracking().SingleAsync(r => r.Id == qcManager.Id)).PermissionVersion);
        Assert.Equal(2, (await db.Roles.AsNoTracking().SingleAsync(r => r.Id == reviewer.Id)).PermissionVersion);
        Assert.Equal(1, (await db.Roles.AsNoTracking().SingleAsync(r => r.Id == analyst.Id)).PermissionVersion);

        // Running it again changes nothing.
        var grants = await db.RolePermissions.CountAsync();
        foreach (var op in new AddTableViews().UpOperations.OfType<SqlOperation>())
            await db.Database.ExecuteSqlRawAsync(op.Sql);
        Assert.Equal(grants, await db.RolePermissions.CountAsync());
        Assert.Equal(2, await db.Permissions.CountAsync(p => p.Code == PermissionConstants.TableViewsShare || p.Code == PermissionConstants.DataExport));

        // Down takes both codes and every grant of them away again.
        foreach (var op in new AddTableViews().DownOperations.OfType<SqlOperation>())
            await db.Database.ExecuteSqlRawAsync(op.Sql);
        Assert.Equal(0, await db.Permissions.CountAsync(p => p.Code == PermissionConstants.TableViewsShare || p.Code == PermissionConstants.DataExport));

        await tx.RollbackAsync();
    }
}
