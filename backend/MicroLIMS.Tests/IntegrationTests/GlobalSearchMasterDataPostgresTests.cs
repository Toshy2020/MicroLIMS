using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.Search;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Global search offers Specification and TestDefinition (the change-control affected-record picker)
// and must not leak specifications or tests of laboratories the reader cannot read.
[Collection("PostgresDatabaseCollection")]
public sealed class GlobalSearchMasterDataPostgresTests(PostgresTestFixture fixture)
{
    private sealed record World(string Token, int ReaderA, int ReaderB, int ViewAll, int Nobody,
        int TestA, int TestB, int SpecA, int SpecB, int SpecRetiredA, string CodeA, string CodeB);

    private static async Task<(int UserId, int SectionId)> SectionUserAsync(MicroLimsDbContext db, string suffix, params string[] permissions)
    {
        var dept = new DocumentDepartment { Code = $"D-{suffix}", Name = $"Dept {suffix}", IsActive = true };
        db.DocumentDepartments.Add(dept);
        await db.SaveChangesAsync();
        var section = new DocumentSection { DepartmentId = dept.Id, Code = $"S-{suffix}", Name = $"Section {suffix}", IsActive = true };
        db.DocumentSections.Add(section);
        await db.SaveChangesAsync();
        return (await UserAsync(db, suffix, section, permissions), section.Id);
    }

    private static async Task<int> UserAsync(MicroLimsDbContext db, string suffix, DocumentSection? section, params string[] permissions)
    {
        var key = $"{suffix}-{Guid.NewGuid():N}";
        var user = new User
        {
            FullName = $"Search {suffix}", Username = key, IsActive = true,
            Role = new Role { Name = key, Type = RoleType.SectionHead, IsActive = true },
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Search-pw", 4)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var ids = await db.Permissions.AsNoTracking().Where(p => permissions.Contains(p.Code)).Select(p => p.Id).ToListAsync();
        db.RolePermissions.AddRange(ids.Select(id => new RolePermission { RoleId = user.RoleId, PermissionId = id }));
        if (section is not null)
            db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, SectionId = section.Id, DepartmentId = section.DepartmentId });
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<World> ArrangeAsync()
    {
        var token = "zq" + Guid.NewGuid().ToString("N")[..8];
        await using var db = fixture.CreateDbContext();
        var (readerA, _) = await SectionUserAsync(db, "a" + token);
        var (readerB, _) = await SectionUserAsync(db, "b" + token);
        var viewAll = await UserAsync(db, "all" + token, null, PermissionConstants.SectionsViewAll);
        var nobody = await UserAsync(db, "none" + token, null);
        var sectionA = await db.UserOrgMemberships.AsNoTracking().Where(m => m.UserId == readerA).Select(m => m.SectionId).SingleAsync();
        var sectionB = await db.UserOrgMemberships.AsNoTracking().Where(m => m.UserId == readerB).Select(m => m.SectionId).SingleAsync();

        var item = new Item { Code = "ITM-" + token, Name = "Search item", IsActive = true };
        db.Items.Add(item);
        var codeA = $"{token}-A".ToUpperInvariant();
        var codeB = $"{token}-B".ToUpperInvariant();
        var testA = new TestDefinition { Code = codeA, DisplayName = $"Assay {token} A", SectionId = sectionA!.Value, IsActive = true };
        var testB = new TestDefinition { Code = codeB, DisplayName = $"Assay {token} B", SectionId = sectionB!.Value, IsActive = true };
        db.TestDefinitions.AddRange(testA, testB);
        await db.SaveChangesAsync();

        Specification Spec(string code, string parameter, bool retired = false) => new()
        {
            ItemId = item.Id, TestCode = code, ParameterName = parameter, LimitType = LimitType.Qualitative,
            ExpectedResultText = "Clear", SpecLimit = "Clear", IsRetired = retired,
            RetiredAt = retired ? new DateTime(2086, 1, 1, 0, 0, 0, DateTimeKind.Utc) : null
        };
        var specA = Spec(codeA, "ParamA");
        var specB = Spec(codeB, "ParamB");
        var specRetired = Spec(codeA, "ParamRetired", retired: true);
        db.Specifications.AddRange(specA, specB, specRetired);
        await db.SaveChangesAsync();
        return new World(token, readerA, readerB, viewAll, nobody, testA.Id, testB.Id, specA.Id, specB.Id, specRetired.Id, codeA, codeB);
    }

    private async Task<List<GlobalSearchItemDto>> SearchAsync(int userId, string query, string? category = null)
    {
        await using var db = fixture.CreateDbContext();
        var result = await new GlobalSearchService(db, new UserSectionScopeService(db))
            .SearchAsync(new GlobalSearchRequest(query, category, 20), userId);
        return result.Categories.SelectMany(c => c.Items).ToList();
    }

    [PostgresFact]
    public async Task Reader_finds_specifications_and_tests_of_their_own_sections_only()
    {
        var w = await ArrangeAsync();

        var asA = await SearchAsync(w.ReaderA, w.Token);
        Assert.Equal(new[] { w.TestA }, asA.Where(i => i.EntityType == "TestDefinition").Select(i => i.Id));
        Assert.Equal(new[] { w.SpecA, w.SpecRetiredA }.Order(), asA.Where(i => i.EntityType == "Specification").Select(i => i.Id).Order());

        var asB = await SearchAsync(w.ReaderB, w.Token);
        Assert.Equal(new[] { w.TestB }, asB.Where(i => i.EntityType == "TestDefinition").Select(i => i.Id));
        Assert.Equal(new[] { w.SpecB }, asB.Where(i => i.EntityType == "Specification").Select(i => i.Id));

        var all = await SearchAsync(w.ViewAll, w.Token);
        Assert.Equal(2, all.Count(i => i.EntityType == "TestDefinition"));
        Assert.Equal(3, all.Count(i => i.EntityType == "Specification"));

        var none = await SearchAsync(w.Nobody, w.Token);
        Assert.DoesNotContain(none, i => i.EntityType is "TestDefinition" or "Specification");
    }

    [PostgresFact]
    public async Task Specification_hit_names_item_test_and_parameter_and_marks_retired_ones()
    {
        var w = await ArrangeAsync();
        var hits = (await SearchAsync(w.ViewAll, w.CodeA, "specifications")).Where(i => i.EntityType == "Specification").ToList();

        var live = Assert.Single(hits, h => h.Id == w.SpecA);
        Assert.Contains("ITM-" + w.Token, live.Title);
        Assert.Contains(w.CodeA, live.Title);
        Assert.Contains("ParamA", live.Title);
        Assert.Equal("Active", live.Status);
        var retired = Assert.Single(hits, h => h.Id == w.SpecRetiredA);
        Assert.Equal("Retired", retired.Status);
        Assert.Contains("Retired", retired.Title);
    }

    [PostgresFact]
    public async Task Test_definition_hit_carries_code_display_name_and_category_filter()
    {
        var w = await ArrangeAsync();
        var hits = await SearchAsync(w.ReaderA, w.CodeA, "testdefinitions");
        var hit = Assert.Single(hits);
        Assert.Equal("TestDefinition", hit.EntityType);
        Assert.Equal(w.TestA, hit.Id);
        Assert.Contains(w.CodeA, hit.Title);
        Assert.Contains($"Assay {w.Token} A", hit.Title + hit.Subtitle);
        Assert.Empty(await SearchAsync(w.ReaderB, w.CodeA, "testdefinitions"));
        Assert.Empty(await SearchAsync(w.ReaderB, w.CodeA, "specifications"));
    }
}
