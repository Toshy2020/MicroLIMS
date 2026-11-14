using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Responses;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// "Items using this test": which items list, how the SOP link resolves, paging
// limits, the single query per page, and the reader's section scope. Each test
// arranges, acts and asserts on separate contexts (CLAUDE.md Rule 4.1).
[Collection("PostgresDatabaseCollection")]
public class TestItemsPostgresTests
{
    private readonly PostgresTestFixture _fixture;

    public TestItemsPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed class CommandLog : DbCommandInterceptor
    {
        private readonly List<string> _commands = new();
        public IReadOnlyList<string> Commands { get { lock (_commands) return _commands.ToList(); } }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            lock (_commands) _commands.Add(command.CommandText);
            return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
        }
    }

    private static string Tag() => Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();

    private async Task<(int TestId, string Code)> AddTestAsync(string tag, string? sectionCode = null)
    {
        await using var db = _fixture.CreateDbContext();
        var section = sectionCode is null
            ? _fixture.SeededSectionId
            : (await db.DocumentSections.AsNoTracking().SingleAsync(s => s.Code == sectionCode)).Id;
        var t = new TestDefinition
        {
            Code = "TI-" + tag, DisplayName = "Items test " + tag, SectionId = section,
            WorkflowType = WorkflowType.Observation, EquationType = EquationType.None, IsActive = true
        };
        db.TestDefinitions.Add(t);
        await db.SaveChangesAsync();
        return (t.Id, t.Code);
    }

    private async Task<int> AddItemAsync(string code, string name, string sop = "", SampleCategory category = SampleCategory.FinishedProduct)
    {
        await using var db = _fixture.CreateDbContext();
        var item = new Item { Code = code, Name = name, SopNumber = sop, Category = category };
        db.Items.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    private async Task AddSpecAsync(int itemId, string testCode)
    {
        await using var db = _fixture.CreateDbContext();
        db.Specifications.Add(new Specification { ItemId = itemId, TestCode = testCode, ParameterName = "p" });
        await db.SaveChangesAsync();
    }

    private async Task AddAutoAsync(int itemId, string testCode)
    {
        await using var db = _fixture.CreateDbContext();
        db.SampleTests.Add(new SampleTest { ItemId = itemId, TestCode = testCode, DisplayName = testCode });
        await db.SaveChangesAsync();
    }

    private async Task<int> AddActiveMasterAsync(string code, string? effectiveRevision)
    {
        await using var db = _fixture.CreateDbContext();
        var m = new DocumentMaster
        {
            MicroLimsDocumentId = ("DOC-" + Guid.NewGuid().ToString("N"))[..16].ToUpperInvariant(),
            CompanyDocumentCode = code, Title = "SOP " + code,
            DocumentTypeId = _fixture.SeededDocTypeId, DepartmentId = _fixture.SeededDepartmentId,
            SectionId = _fixture.SeededSectionId, DocumentOwnerUserId = _fixture.SeededUserId,
            CreatedByUserId = _fixture.SeededUserId, RecordStatus = DocumentRecordStatus.Active
        };
        db.DocumentMasters.Add(m);
        await db.SaveChangesAsync();
        if (effectiveRevision is not null)
        {
            var r = new DocumentRevision
            {
                DocumentMasterId = m.Id, RevisionNumber = effectiveRevision, RevisionSequence = 1,
                RevisionStatus = DocumentRevisionStatus.Effective,
                EffectiveDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), CreatedByUserId = _fixture.SeededUserId
            };
            db.DocumentRevisions.Add(r);
            await db.SaveChangesAsync();
            m.CurrentEffectiveRevisionId = r.Id;
            await db.SaveChangesAsync();
        }
        return m.Id;
    }

    // A non-administrator analyst who belongs to the given section (and physchem area, if any).
    private async Task<int> AddAnalystAsync(string tag, int? sectionId, PhyschemArea? area = null)
    {
        await using var db = _fixture.CreateDbContext();
        var role = await db.Roles.AsNoTracking().FirstAsync(r => r.Type == RoleType.Analyst);
        var u = new User { Username = "ti_" + tag, FullName = "Analyst " + tag, RoleId = role.Id, IsActive = true, PasswordHash = "x" };
        db.Users.Add(u);
        await db.SaveChangesAsync();
        if (sectionId.HasValue)
        {
            var dept = (await db.DocumentSections.AsNoTracking().SingleAsync(s => s.Id == sectionId.Value)).DepartmentId;
            db.UserOrgMemberships.Add(new UserOrgMembership { UserId = u.Id, DepartmentId = dept, SectionId = sectionId, PhyschemArea = area });
            await db.SaveChangesAsync();
        }
        return u.Id;
    }

    private async Task<PagedResult<TestItemRow>> QueryAsync(
        int testId, int userId, string? search = null, int page = 1, int pageSize = 25, CommandLog? log = null)
    {
        await using var db = log is null ? _fixture.CreateDbContext() : _fixture.CreateDbContext(log);
        return await TestServiceFactory.TestDefinitionMaster(db).GetItemsForTestAsync(testId, search, page, pageSize, userId);
    }

    [PostgresFact]
    public async Task ListsItemsWithSpecificationOrAutoAssign_ExcludesNotApplicable()
    {
        var tag = Tag();
        var (testId, code) = await AddTestAsync(tag);
        var withSpec = await AddItemAsync("S-" + tag, "Spec item");
        var withAuto = await AddItemAsync("A-" + tag, "Auto item");
        var withBoth = await AddItemAsync("B-" + tag, "Both item");
        var naItem = await AddItemAsync("N-" + tag, "Not applicable item");
        var unrelated = await AddItemAsync("U-" + tag, "Unrelated item");
        await AddSpecAsync(withSpec, code);
        await AddAutoAsync(withAuto, code);
        await AddSpecAsync(withBoth, code);
        await AddAutoAsync(withBoth, code);
        await AddSpecAsync(naItem, code);
        await using (var arrange = _fixture.CreateDbContext())
        {
            arrange.NotApplicableTests.Add(new NotApplicableTest
            {
                ItemId = naItem, TestCode = code, Grade = null, Status = NotApplicableStatus.Approved,
                Justification = "j", Criticality = "c", ImmediateAction = "i", LongTermAction = "l", CreatedByUserId = _fixture.SeededUserId
            });
            await arrange.SaveChangesAsync();
        }
        var analyst = await AddAnalystAsync(tag, _fixture.SeededSectionId);

        var page = await QueryAsync(testId, analyst);

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(new[] { "A-" + tag, "B-" + tag, "S-" + tag }, page.Items.Select(r => r.ItemCode).ToArray());
        Assert.True(page.Items.Single(r => r.ItemId == withBoth).HasSpecification);
        Assert.True(page.Items.Single(r => r.ItemId == withBoth).AutoAssignedOnReceipt);
        Assert.False(page.Items.Single(r => r.ItemId == withSpec).AutoAssignedOnReceipt);
        Assert.False(page.Items.Single(r => r.ItemId == withAuto).HasSpecification);
        Assert.DoesNotContain(page.Items, r => r.ItemId == naItem || r.ItemId == unrelated);
    }

    [PostgresFact]
    public async Task SopStatus_Linked_NotFound_NoNumber()
    {
        var tag = Tag();
        var (testId, code) = await AddTestAsync(tag);
        var linkedCode = "SOP-L-" + tag;
        var masterId = await AddActiveMasterAsync(linkedCode, "03");
        var linked = await AddItemAsync("L-" + tag, "Linked", "  " + linkedCode.ToLowerInvariant() + " ");
        var missing = await AddItemAsync("M-" + tag, "Missing", "SOP-NOPE-" + tag);
        var none = await AddItemAsync("O-" + tag, "No number", "");
        foreach (var id in new[] { linked, missing, none }) await AddSpecAsync(id, code);
        var analyst = await AddAnalystAsync(tag, _fixture.SeededSectionId);

        var page = await QueryAsync(testId, analyst);

        var l = page.Items.Single(r => r.ItemId == linked);
        Assert.Equal("Linked", l.SopStatus);
        Assert.Equal(masterId, l.DocumentMasterId);
        Assert.Equal("03", l.EffectiveRevisionNumber);
        Assert.Equal(linkedCode.ToLowerInvariant(), l.SopNumber);
        var m = page.Items.Single(r => r.ItemId == missing);
        Assert.Equal("NotFound", m.SopStatus);
        Assert.Null(m.DocumentMasterId);
        var o = page.Items.Single(r => r.ItemId == none);
        Assert.Equal("NoNumber", o.SopStatus);
        Assert.Null(o.SopNumber);
    }

    [PostgresFact]
    public async Task SopStatus_LinkedWithoutEffectiveRevision_HasNoRevisionNumber()
    {
        var tag = Tag();
        var (testId, code) = await AddTestAsync(tag);
        var sop = "SOP-D-" + tag;
        await AddActiveMasterAsync(sop, null);
        var item = await AddItemAsync("D-" + tag, "Draft only", sop);
        await AddSpecAsync(item, code);
        var analyst = await AddAnalystAsync(tag, _fixture.SeededSectionId);

        var row = Assert.Single((await QueryAsync(testId, analyst)).Items);

        Assert.Equal("Linked", row.SopStatus);
        Assert.Null(row.EffectiveRevisionNumber);
    }

    [PostgresFact]
    public async Task Paged_ClampedTo100_SearchByCodeOrName()
    {
        var tag = Tag();
        var (testId, code) = await AddTestAsync(tag);
        await using (var arrange = _fixture.CreateDbContext())
        {
            for (var i = 1; i <= 105; i++)
            {
                var item = new Item { Code = $"P{i:000}-{tag}", Name = i == 7 ? "Needle product" : "Plain " + i };
                item.Specifications.Add(new Specification { TestCode = code, ParameterName = "p" });
                arrange.Items.Add(item);
            }
            await arrange.SaveChangesAsync();
        }
        var analyst = await AddAnalystAsync(tag, _fixture.SeededSectionId);

        var big = await QueryAsync(testId, analyst, pageSize: 5000);
        var zero = await QueryAsync(testId, analyst, pageSize: 0);
        var second = await QueryAsync(testId, analyst, page: 2, pageSize: 100);
        var byName = await QueryAsync(testId, analyst, search: "needle");
        var byCode = await QueryAsync(testId, analyst, search: $"p105-{tag.ToLowerInvariant()}");

        Assert.Equal(100, big.Items.Count);
        Assert.Equal(100, big.PageSize);
        Assert.Equal(105, big.TotalCount);
        Assert.Equal(25, zero.Items.Count);
        Assert.Equal(25, zero.PageSize);
        Assert.Equal(5, second.Items.Count);
        Assert.Equal($"P101-{tag}", second.Items[0].ItemCode);
        Assert.Equal($"P007-{tag}", Assert.Single(byName.Items).ItemCode);
        Assert.Equal(1, byName.TotalCount);
        Assert.Equal($"P105-{tag}", Assert.Single(byCode.Items).ItemCode);
    }

    [PostgresFact]
    public async Task ThreeHundredItems_OneQueryPerPage()
    {
        var tag = Tag();
        var (testId, code) = await AddTestAsync(tag);
        var analyst = await AddAnalystAsync(tag, _fixture.SeededSectionId);
        var sop = "SOP-3-" + tag;
        await AddActiveMasterAsync(sop, "01");

        async Task SeedAsync(int from, int to)
        {
            await using var db = _fixture.CreateDbContext();
            for (var i = from; i <= to; i++)
            {
                var item = new Item { Code = $"T{i:000}-{tag}", Name = "Bulk " + i, SopNumber = i % 2 == 0 ? sop : "" };
                item.Specifications.Add(new Specification { TestCode = code, ParameterName = "p" });
                db.Items.Add(item);
            }
            await db.SaveChangesAsync();
        }

        await SeedAsync(1, 5);
        var smallLog = new CommandLog();
        var small = await QueryAsync(testId, analyst, pageSize: 5, log: smallLog);
        await SeedAsync(6, 300);
        var bigLog = new CommandLog();
        var big = await QueryAsync(testId, analyst, pageSize: 100, log: bigLog);

        Assert.Equal(5, small.Items.Count);
        Assert.Equal(300, big.TotalCount);
        Assert.Equal(100, big.Items.Count);
        // The page costs the same number of commands whatever it holds, and the document
        // lookup rides inside the single page query: nothing is fetched per item.
        Assert.Equal(smallLog.Commands.Count, bigLog.Commands.Count);
        Assert.Equal(1, bigLog.Commands.Count(c => c.Contains("DocumentMasters", StringComparison.OrdinalIgnoreCase)
                                                || c.Contains("document_masters", StringComparison.OrdinalIgnoreCase)));
        Assert.True(bigLog.Commands.Count <= 6, "commands: " + bigLog.Commands.Count);
        Assert.All(big.Items.Where(r => r.SopNumber is not null), r => Assert.Equal("Linked", r.SopStatus));
    }

    [PostgresFact]
    public async Task ItemsOfSectionICannotRead_Excluded()
    {
        var tag = Tag();
        // The test belongs to the seeded MICRO laboratory.
        var (microTest, microCode) = await AddTestAsync(tag);
        var microItem = await AddItemAsync("MI-" + tag, "Micro item");
        await AddSpecAsync(microItem, microCode);
        var inScope = await AddAnalystAsync("in" + tag, _fixture.SeededSectionId);
        var outOfScope = await AddAnalystAsync("out" + tag, null);

        // Physicochemical: one test with a finished-product item and a raw-material item.
        var (fpTest, fpCode) = await AddTestAsync("F" + tag, "FP");
        var fpItem = await AddItemAsync("FI-" + tag, "FP item", category: SampleCategory.FinishedProduct);
        var rmItem = await AddItemAsync("RI-" + tag, "RM item", category: SampleCategory.RawMaterial);
        await AddSpecAsync(fpItem, fpCode);
        await AddSpecAsync(rmItem, fpCode);
        int fpSection;
        await using (var db = _fixture.CreateDbContext())
            fpSection = (await db.DocumentSections.AsNoTracking().SingleAsync(s => s.Code == "FP")).Id;
        var rmOnly = await AddAnalystAsync("rm" + tag, fpSection, PhyschemArea.RawPackaging);
        var fpOnly = await AddAnalystAsync("fp" + tag, fpSection, PhyschemArea.FinishedProduct);

        var mine = await QueryAsync(microTest, inScope);
        var theirs = await QueryAsync(microTest, outOfScope);
        var rmView = await QueryAsync(fpTest, rmOnly);
        var fpView = await QueryAsync(fpTest, fpOnly);
        var microOnFpTest = await QueryAsync(fpTest, inScope);
        var fpOnMicroTest = await QueryAsync(microTest, fpOnly);

        Assert.Equal(microItem, Assert.Single(mine.Items).ItemId);
        Assert.Empty(theirs.Items);
        Assert.Equal(0, theirs.TotalCount);
        Assert.Equal(rmItem, Assert.Single(rmView.Items).ItemId);
        Assert.Equal(fpItem, Assert.Single(fpView.Items).ItemId);
        Assert.Empty(microOnFpTest.Items);
        Assert.Empty(fpOnMicroTest.Items);
        Assert.Equal(0, fpOnMicroTest.TotalCount);
    }

    [PostgresFact]
    public async Task UnknownTest_IsNotFound()
    {
        var analyst = await AddAnalystAsync(Tag(), _fixture.SeededSectionId);
        await Assert.ThrowsAsync<NotFoundException>(() => QueryAsync(int.MaxValue, analyst));
    }
}
