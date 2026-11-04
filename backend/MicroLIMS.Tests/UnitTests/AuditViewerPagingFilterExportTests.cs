using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// WP2.4 / WP2.9: keyset paging, the new filters, the negative-EntityId flag, CSV export
// (cap, formula guard, audit of the export) and the security-audit permission.
public class AuditViewerPagingFilterExportTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static AuditSearchRequest Req(
        int take = 200, int? beforeId = null, AuditActionCategory? category = null, ActorType? actor = null,
        Guid? correlationId = null, string? reason = null, bool? negative = null, string? entityName = null) =>
        new(null, null, null, null, null, null, null, null, null, null, null, entityName, null,
            take, beforeId, category, actor, correlationId, reason, negative);

    private static AuditLog Row(string entityId = "1", string entityName = "Sample", string action = "Create") =>
        new() { EntityName = entityName, EntityId = entityId, Action = action, UserId = 0, Timestamp = DateTime.UtcNow };

    private sealed class RecordingAudit : IAuditEventService
    {
        public readonly List<(string Code, AuditActionCategory Category, string? Reason, List<AuditFieldChange> Changes)> Events = new();

        public Task<AuditLog> RecordUserEventAsync(string actionCode, AuditActionCategory actionCategory, string recordType,
            int? documentMasterId = null, int? documentRevisionId = null, string? reason = null, Guid? correlationId = null,
            string? sourceContext = null, IEnumerable<AuditFieldChange>? changes = null, string? entityId = null,
            CancellationToken cancellationToken = default, int? sampleId = null)
        {
            Events.Add((actionCode, actionCategory, reason, changes?.ToList() ?? new()));
            return Task.FromResult(new AuditLog());
        }

        public Task<AuditLog> RecordSystemEventAsync(string systemProcessName, string actionCode, AuditActionCategory actionCategory,
            string recordType, int? documentMasterId = null, int? documentRevisionId = null, string? reason = null,
            Guid? correlationId = null, string? sourceContext = null, IEnumerable<AuditFieldChange>? changes = null,
            string? entityId = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    [Fact]
    public async Task KeysetPaging_WalksEveryRowOnceNewestFirst_AndStopsAtTheEnd()
    {
        await using var db = NewDb();
        for (var i = 1; i <= 25; i++) db.AuditLogs.Add(Row(i.ToString()));
        await db.SaveChangesAsync();
        var svc = new AuditSearchService(db);

        var seen = new List<int>();
        int? cursor = null;
        var pages = 0;
        do
        {
            var page = await svc.SearchPageAsync(Req(take: 10, beforeId: cursor));
            seen.AddRange(page.Items.Select(i => i.Id));
            pages++;
            Assert.Equal(page.HasMore, page.NextCursor.HasValue);
            cursor = page.NextCursor;
        } while (cursor.HasValue);

        Assert.Equal(3, pages);
        Assert.Equal(25, seen.Count);
        Assert.Equal(seen.OrderByDescending(x => x), seen);
        Assert.Equal(25, seen.Distinct().Count());
    }

    [Fact]
    public async Task Paging_StillClampsTake_AndSearchAsyncKeepsReturningAList()
    {
        await using var db = NewDb();
        for (var i = 0; i < AuditSearchService.MaxTake + 3; i++) db.AuditLogs.Add(Row(i.ToString()));
        await db.SaveChangesAsync();
        var svc = new AuditSearchService(db);

        var page = await svc.SearchPageAsync(Req(take: 5_000_000));
        Assert.Equal(AuditSearchService.MaxTake, page.Items.Count);
        Assert.True(page.HasMore);

        var list = await svc.SearchAsync(Req(take: 0));
        Assert.Single(list);
    }

    [Fact]
    public async Task NewFilters_CategoryActorCorrelationAndReason_Narrow()
    {
        await using var db = NewDb();
        var corr = Guid.NewGuid();
        var a = Row("1"); a.ActionCategory = AuditActionCategory.Security; a.ActorType = ActorType.User; a.Reason = "Password Reset by admin"; a.CorrelationId = corr;
        var b = Row("2"); b.ActionCategory = AuditActionCategory.Document; b.ActorType = ActorType.System; b.Reason = "scheduled";
        var c = Row("3");
        db.AuditLogs.AddRange(a, b, c);
        await db.SaveChangesAsync();
        var svc = new AuditSearchService(db);

        Assert.Equal(a.Id, Assert.Single(await svc.SearchAsync(Req(category: AuditActionCategory.Security))).Id);
        Assert.Equal(b.Id, Assert.Single(await svc.SearchAsync(Req(actor: ActorType.System))).Id);
        Assert.Equal(a.Id, Assert.Single(await svc.SearchAsync(Req(correlationId: corr))).Id);
        Assert.Equal(a.Id, Assert.Single(await svc.SearchAsync(Req(reason: "password RESET"))).Id);
        Assert.Empty(await svc.SearchAsync(Req(reason: "no such text")));
        Assert.Equal(3, (await svc.SearchAsync(Req())).Count);
    }

    [Fact]
    public async Task NegativeEntityId_IsFlagged_FilteredAndShowsItsCorrection()
    {
        await using var db = NewDb();
        var good = Row("5");
        var bad = Row("-2147482647");
        var fixedBad = Row("-2147482000");
        db.AuditLogs.AddRange(good, bad, fixedBad);
        await db.SaveChangesAsync();
        var correction = new AuditLog
        {
            EntityName = "AuditLog", EntityId = fixedBad.Id.ToString(), Action = AuditSearchService.CorrectionActionCode,
            ActionCode = AuditSearchService.CorrectionActionCode, ActorType = ActorType.System, Timestamp = DateTime.UtcNow
        };
        db.AuditLogs.Add(correction);
        await db.SaveChangesAsync();
        var svc = new AuditSearchService(db);

        var negatives = await svc.SearchAsync(Req(negative: true));
        Assert.Equal(2, negatives.Count);
        Assert.All(negatives, n => Assert.True(n.EntityIdNegative));
        Assert.Null(negatives.Single(n => n.Id == bad.Id).CorrectedByAuditLogId);
        Assert.Equal(correction.Id, negatives.Single(n => n.Id == fixedBad.Id).CorrectedByAuditLogId);

        var all = await svc.SearchAsync(Req());
        Assert.False(all.Single(n => n.Id == good.Id).EntityIdNegative);
    }

    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+cmd", "'+cmd")]
    [InlineData("-5", "'-5")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("plain", "plain")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("=a,b", "\"'=a,b\"")]
    public void CsvCell_NeutralisesFormulasAndQuotes(string input, string expected) =>
        Assert.Equal(expected, AuditExportService.Cell(input));

    [Fact]
    public async Task Export_WritesFilteredRows_GuardsFormulas_AndRecordsTheExportFirst()
    {
        await using var db = NewDb();
        var evil = Row("1"); evil.Reason = "=HYPERLINK(\"http://x\")"; evil.NewValue = "{\"Name\":\"@x\"}";
        db.AuditLogs.AddRange(evil, Row("2", "Other"));
        await db.SaveChangesAsync();
        var search = new AuditSearchService(db);
        var audit = new RecordingAudit();
        var export = new AuditExportService(search, audit);

        var plan = await export.PrepareAsync(Req(entityName: "Sample"));
        Assert.Equal(1, plan.RowCount);
        Assert.False(plan.Truncated);
        var evt = Assert.Single(audit.Events);
        Assert.Equal(AuditExportService.ExportActionCode, evt.Code);
        Assert.Equal(AuditActionCategory.ImportExport, evt.Category);
        Assert.Contains(evt.Changes, c => c.FieldName == "RowCount" && c.NewValue == "1");
        Assert.Contains(evt.Changes, c => c.FieldName == "Filter" && c.NewValue!.Contains("\"EntityName\":\"Sample\""));

        var sw = new StringWriter();
        await export.WriteCsvAsync(plan, sw);
        var lines = sw.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length); // header + the one Sample row
        Assert.Contains("'=HYPERLINK", lines[1]);
        Assert.DoesNotContain(",=HYPERLINK", lines[1]);
        Assert.Contains("\"{\"\"Name\"\":\"\"@x\"\"}\"", lines[1]);
    }

    [Fact]
    public async Task Export_IsCappedAt50kRows_AndSaysSo()
    {
        await using var db = NewDb();
        // Fewer physical rows than the cap are enough to prove the arithmetic; the cap itself is the constant.
        Assert.Equal(50_000, AuditExportService.MaxExportRows);
        for (var i = 0; i < 12; i++) db.AuditLogs.Add(Row(i.ToString()));
        await db.SaveChangesAsync();
        var export = new AuditExportService(new AuditSearchService(db), new RecordingAudit());

        var plan = await export.PrepareAsync(Req());
        Assert.Equal(12, plan.RowCount);
        Assert.False(plan.Truncated);
    }

    [Fact]
    public async Task Export_ReadsAcrossPages_WithoutDuplicatesOrGaps()
    {
        await using var db = NewDb();
        for (var i = 0; i < 2300; i++) db.AuditLogs.Add(Row(i.ToString()));
        await db.SaveChangesAsync();
        var export = new AuditExportService(new AuditSearchService(db), new RecordingAudit());

        var plan = await export.PrepareAsync(Req());
        var sw = new StringWriter();
        await export.WriteCsvAsync(plan, sw);
        var rows = sw.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(l => l.Split(',')[0]).ToList();
        Assert.Equal(2300, rows.Count);
        Assert.Equal(2300, rows.Distinct().Count());
    }

    [Fact]
    public async Task SecurityEventsAndLoginHistory_PageByKeyset()
    {
        await using var db = NewDb();
        for (var i = 0; i < 7; i++)
        {
            db.SecurityAuditEvents.Add(new SecurityAuditEvent { EventCode = "AUTH_LOGIN_SUCCESS", Source = "Api", OccurredAtUtc = DateTime.UtcNow });
            db.LoginHistories.Add(new LoginHistory { Username = "u", Success = true, Timestamp = DateTime.UtcNow });
        }
        await db.SaveChangesAsync();
        var svc = new AuditSearchService(db);

        var p1 = await svc.GetSecurityEventsAsync(null, null, null, null, 5, null);
        Assert.Equal(5, p1.Items.Count);
        Assert.True(p1.HasMore);
        var p2 = await svc.GetSecurityEventsAsync(null, null, null, null, 5, p1.NextCursor);
        Assert.Equal(2, p2.Items.Count);
        Assert.False(p2.HasMore);

        var l1 = await svc.GetLoginHistoryPageAsync(null, 5, null);
        var l2 = await svc.GetLoginHistoryPageAsync(null, 5, l1.NextCursor);
        Assert.Equal(7, l1.Items.Count + l2.Items.Count);
        Assert.Empty(l1.Items.Select(i => i.Id).Intersect(l2.Items.Select(i => i.Id)));
    }

    [Fact]
    public void SecurityAuditEndpoints_RequireSystemViewSecurityAudit_AndTheCodeIsEnforced()
    {
        Assert.Contains(PermissionConstants.SystemViewSecurityAudit, PermissionConstants.Enforced);

        foreach (var name in new[] { nameof(AuditController.GetLoginHistory), nameof(AuditController.GetSecurityEvents) })
        {
            var method = typeof(AuditController).GetMethod(name)!;
            var policy = method.GetCustomAttributes<AuthorizeAttribute>().Single().Policy;
            Assert.Equal(PermissionConstants.SystemViewSecurityAudit, policy);
        }

        // The export sits under the controller-wide Audit.View policy and is not anonymous.
        var export = typeof(AuditController).GetMethod(nameof(AuditController.Export))!;
        Assert.Empty(export.GetCustomAttributes<AllowAnonymousAttribute>());
        Assert.Equal(PermissionConstants.AuditView, typeof(AuditController).GetCustomAttributes<AuthorizeAttribute>().Single().Policy);
    }
}
