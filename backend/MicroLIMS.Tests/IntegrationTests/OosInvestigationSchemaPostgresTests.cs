using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class OosInvestigationSchemaPostgresTests(PostgresTestFixture fixture)
{
    [PostgresFact]
    public async Task Generate_code_uses_lab_clock_month()
    {
        await using var db = fixture.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var clock = new FixedTimeProvider(new DateTimeOffset(2037, 1, 31, 23, 0, 0, TimeSpan.Zero));

        var code = await new ReferenceNumberGenerator(db, clock).GenerateOosCodeAsync();

        Assert.Equal("OOS0237001", code);
    }

    [PostgresFact]
    public async Task ReferenceNumberGenerator_counts_distinct_legacy_and_investigation_codes()
    {
        var (sample, _) = await SeedOrderAsync("OOS0439001");
        await SeedOrderAsync("OOS0439001");
        await SeedOrderAsync("OOS0439002");
        await using (var arrange = fixture.CreateDbContext())
            await InsertInvestigationAsync(arrange, sample, "OOS0439001");
        await using var act = fixture.CreateDbContext();
        await using var transaction = await act.Database.BeginTransactionAsync();
        var clock = new FixedTimeProvider(new DateTimeOffset(2039, 4, 15, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal("OOS0439003", await new ReferenceNumberGenerator(act, clock).GenerateOosCodeAsync());
    }

    [PostgresFact]
    public async Task Generate_code_requires_transaction()
    {
        await using var db = fixture.CreateDbContext();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ReferenceNumberGenerator(db).GenerateOosCodeAsync());
    }

    [PostgresFact]
    public async Task No_cause_found_category_seeded_once()
    {
        await using var db = fixture.CreateDbContext();
        var names = await db.Database.SqlQueryRaw<string>(
            """SELECT "Name" AS "Value" FROM "OosCauseCategories" WHERE "IsNoCauseFound" AND "IsActive" """)
            .ToListAsync();

        Assert.Equal(new[] { "No cause found" }, names);
    }

    [PostgresFact]
    public async Task Concurrent_openings_get_distinct_numbers()
    {
        var (firstSample, _) = await SeedOrderAsync();
        var (secondSample, _) = await SeedOrderAsync();
        await using var first = fixture.CreateDbContext();
        await using var second = fixture.CreateDbContext();
        await using var firstTx = await first.Database.BeginTransactionAsync();
        await using var secondTx = await second.Database.BeginTransactionAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var clock = new FixedTimeProvider(new DateTimeOffset(2038, 3, 15, 12, 0, 0, TimeSpan.Zero));
        var firstGenerator = new ReferenceNumberGenerator(first, clock);
        var secondGenerator = new ReferenceNumberGenerator(second, clock);
        var firstCode = await firstGenerator.GenerateOosCodeAsync(timeout.Token);
        await InsertInvestigationAsync(first, firstSample, firstCode);

        var secondPid = ((NpgsqlConnection)second.Database.GetDbConnection()).ProcessID;
        var competingCode = secondGenerator.GenerateOosCodeAsync(timeout.Token);
        await using (var observer = fixture.CreateDbContext())
        {
            while (!competingCode.IsCompleted && !await observer.Database.SqlQuery<bool>(
                       $"SELECT cardinality(pg_blocking_pids({secondPid})) > 0 AS \"Value\"").SingleAsync(timeout.Token))
                await Task.Delay(10, timeout.Token);
        }

        await firstTx.CommitAsync(timeout.Token);
        var secondCode = await competingCode;
        await InsertInvestigationAsync(second, secondSample, secondCode);
        await secondTx.CommitAsync(timeout.Token);

        await using var verify = fixture.CreateDbContext();
        var persisted = await verify.Database.SqlQuery<string>(
            $"""SELECT "Number" AS "Value" FROM "OosInvestigations" WHERE "SampleId" IN ({firstSample}, {secondSample})""")
            .OrderBy(n => n).ToListAsync();
        Assert.Equal(new[] { "OOS0338001", "OOS0338002" }, persisted);
    }

    [PostgresFact]
    public async Task Number_unique_index_rejects_duplicate()
    {
        var (first, _) = await SeedOrderAsync();
        var (second, _) = await SeedOrderAsync();
        var number = NewNumber();
        await using (var arrange = fixture.CreateDbContext())
            await InsertInvestigationAsync(arrange, first, number);
        await using var act = fixture.CreateDbContext();
        var error = await Assert.ThrowsAsync<PostgresException>(() => InsertInvestigationAsync(act, second, number));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("IX_OosInvestigations_Number", error.ConstraintName);
    }

    [PostgresFact]
    public async Task Second_open_investigation_for_same_sample_section_kind_rejected()
    {
        var (sample, _) = await SeedOrderAsync();
        await using (var arrange = fixture.CreateDbContext())
            await InsertInvestigationAsync(arrange, sample, NewNumber());
        await using var act = fixture.CreateDbContext();
        var error = await Assert.ThrowsAsync<PostgresException>(() => InsertInvestigationAsync(act, sample, NewNumber()));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("IX_OosInvestigations_SampleId_SectionId_Kind", error.ConstraintName);
        await act.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "OosInvestigations" SET "Status" = 3 WHERE "SampleId" = {sample}""");
        await InsertInvestigationAsync(act, sample, NewNumber());
    }

    [PostgresFact]
    public async Task Investigation_updates_persist_and_delete_and_truncate_are_rejected()
    {
        var (id, _) = await SeedInvestigationAsync();
        var clock = new FixedTimeProvider(new DateTimeOffset(2037, 2, 1, 12, 0, 0, TimeSpan.Zero));
        uint originalVersion;
        await using (var act = fixture.CreateDbContext(clock))
        {
            act.CurrentUserId = fixture.SeededUserId;
            var investigation = await act.OosInvestigations.SingleAsync(x => x.Id == id);
            originalVersion = investigation.Version;
            investigation.Status = OosInvestigationStatus.PhaseTwo;
            await act.SaveChangesAsync();
        }

        await using var verify = fixture.CreateDbContext();
        var persisted = await verify.OosInvestigations.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(OosInvestigationStatus.PhaseTwo, persisted.Status);
        Assert.NotEqual(originalVersion, persisted.Version);
        var audit = await verify.AuditLogs.AsNoTracking().SingleAsync(a =>
            a.EntityName == nameof(OosInvestigation) && a.EntityId == id.ToString() && a.Action == "Update");
        Assert.Equal(fixture.SeededUserId, audit.UserId);
        Assert.Equal(clock.GetUtcNow().UtcDateTime, audit.Timestamp);
        using var previous = JsonDocument.Parse(Assert.IsType<string>(audit.PreviousValue));
        using var next = JsonDocument.Parse(Assert.IsType<string>(audit.NewValue));
        Assert.Equal((int)OosInvestigationStatus.PhaseOne, previous.RootElement.GetProperty("Status").GetInt32());
        Assert.Equal((int)OosInvestigationStatus.PhaseTwo, next.RootElement.GetProperty("Status").GetInt32());
        await RejectAsync($"""DELETE FROM "OosInvestigations" WHERE "Id" = {id}""");
        await RejectAsync($"""TRUNCATE "OosInvestigations" CASCADE""");
    }

    [PostgresFact]
    public async Task Entry_update_and_delete_rejected_by_trigger()
    {
        var (id, _) = await SeedInvestigationAsync();
        await using (var arrange = fixture.CreateDbContext())
            await arrange.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "OosInvestigationEntries" ("InvestigationId", "Phase", "Kind", "Text", "RecordedByUserId", "RecordedAtUtc")
                VALUES ({id}, 1, 0, 'statement', {fixture.SeededUserId}, now())
                """);
        await RejectAsync($"""UPDATE "OosInvestigationEntries" SET "Text" = 'changed' WHERE "InvestigationId" = {id}""");
        await RejectAsync($"""DELETE FROM "OosInvestigationEntries" WHERE "InvestigationId" = {id}""");
        await RejectAsync($"""TRUNCATE "OosInvestigationEntries" CASCADE""");
    }

    [PostgresFact]
    public async Task Result_link_is_immutable_outside_invalidation_and_signed_reopening()
    {
        var (id, order) = await SeedInvestigationAsync();
        await using (var arrange = fixture.CreateDbContext())
            await arrange.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "OosInvestigationResults" ("InvestigationId", "TestOrderId", "OutcomeAtOpening", "ResultSummaryAtOpening", "AddedAtUtc", "IsInvalidated")
                VALUES ({id}, {order}, 'Fails', 'original', now(), false)
                """);
        await RejectAsync($"""UPDATE "OosInvestigationResults" SET "ResultSummaryAtOpening" = 'changed' WHERE "InvestigationId" = {id}""");
        await RejectAsync($"""UPDATE "OosInvestigationResults" SET "IsInvalidated" = true, "ResultSummaryAtOpening" = 'changed' WHERE "InvestigationId" = {id}""");
        await using (var act = fixture.CreateDbContext())
            Assert.Equal(1, await act.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "OosInvestigationResults" SET "IsInvalidated" = true, "InvalidatedAtUtc" = now() WHERE "InvestigationId" = {id}
                """));
        await using (var verify = fixture.CreateDbContext())
            Assert.True(await verify.Database.SqlQuery<bool>($"""
                SELECT ("IsInvalidated" AND "InvalidatedAtUtc" IS NOT NULL AND "ResultSummaryAtOpening" = 'original') AS "Value"
                FROM "OosInvestigationResults" WHERE "InvestigationId" = {id}
                """).SingleAsync());
        await RejectAsync($"""UPDATE "OosInvestigationResults" SET "IsInvalidated" = false, "InvalidatedAtUtc" = NULL WHERE "InvestigationId" = {id}""");
        await RejectAsync($"""UPDATE "OosInvestigationResults" SET "InvalidatedAtUtc" = now() WHERE "InvestigationId" = {id}""");
        await RejectAsync($"""DELETE FROM "OosInvestigationResults" WHERE "InvestigationId" = {id}""");
        await RejectAsync($"""TRUNCATE "OosInvestigationResults" """);
    }

    [PostgresFact]
    public async Task Signed_plan_update_rejected_except_samples_created()
    {
        var (id, _) = await SeedInvestigationAsync();
        await using (var arrange = fixture.CreateDbContext())
            await arrange.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "OosRetestPlans" ("InvestigationId", "Revision", "Source", "RetestCount", "TestOrderIdsJson", "AnalystUserIdsJson", "JudgingRule")
                VALUES ({id}, 1, 0, 1, '[]', '[]', 'initial')
                """);
        await using (var act = fixture.CreateDbContext())
            Assert.Equal(1, await act.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "OosRetestPlans" SET "JudgingRule" = 'revised', "SignedAtUtc" = now() WHERE "InvestigationId" = {id}
                """));
        await RejectAsync($"""UPDATE "OosRetestPlans" SET "JudgingRule" = 'changed' WHERE "InvestigationId" = {id}""");
        await RejectAsync($"""UPDATE "OosRetestPlans" SET "SignedAtUtc" = NULL WHERE "InvestigationId" = {id}""");
        await using (var act = fixture.CreateDbContext())
            Assert.Equal(1, await act.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "OosRetestPlans" SET "SamplesCreatedAtUtc" = now() WHERE "InvestigationId" = {id}
                """));
        await using (var verify = fixture.CreateDbContext())
            Assert.True(await verify.Database.SqlQuery<bool>($"""
                SELECT ("JudgingRule" = 'revised' AND "SignedAtUtc" IS NOT NULL AND "SamplesCreatedAtUtc" IS NOT NULL) AS "Value"
                FROM "OosRetestPlans" WHERE "InvestigationId" = {id}
                """).SingleAsync());
        await RejectAsync($"""DELETE FROM "OosRetestPlans" WHERE "InvestigationId" = {id}""");
        await RejectAsync($"""TRUNCATE "OosRetestPlans" CASCADE""");
    }

    [PostgresFact]
    public async Task Reopening_is_immutable()
    {
        var (id, _) = await SeedInvestigationAsync();
        await using (var arrange = fixture.CreateDbContext())
        {
            var signature = new ElectronicSignature { UserId = fixture.SeededUserId, EntityType = "OosInvestigation", EntityId = id };
            arrange.ElectronicSignatures.Add(signature);
            await arrange.SaveChangesAsync();
            await arrange.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "OosInvestigationReopenings" ("InvestigationId", "Reason", "PreviousConclusion", "PreviousConclusionSignatureId", "ReopenedByUserId", "ReopenedAtUtc", "SignatureId")
                VALUES ({id}, 'reason', 0, {signature.Id}, {fixture.SeededUserId}, now(), {signature.Id})
                """);
        }
        await RejectAsync($"""UPDATE "OosInvestigationReopenings" SET "Reason" = 'changed' WHERE "InvestigationId" = {id}""");
        await RejectAsync($"""DELETE FROM "OosInvestigationReopenings" WHERE "InvestigationId" = {id}""");
        await RejectAsync($"""TRUNCATE "OosInvestigationReopenings" """);
    }

    private async Task RejectAsync(FormattableString sql)
    {
        await using var db = fixture.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(sql));
        Assert.Equal(PostgresErrorCodes.RaiseException, error.SqlState);
    }

    private async Task<(int Investigation, int Order)> SeedInvestigationAsync()
    {
        var (sample, order) = await SeedOrderAsync();
        await using var db = fixture.CreateDbContext();
        var number = NewNumber();
        await InsertInvestigationAsync(db, sample, number);
        var id = await db.Database.SqlQuery<int>(
            $"""SELECT "Id" AS "Value" FROM "OosInvestigations" WHERE "Number" = {number}""").SingleAsync();
        return (id, order);
    }

    private Task<int> InsertInvestigationAsync(MicroLimsDbContext db, int sample, string number) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "OosInvestigations" ("Number", "Kind", "Status", "SampleId", "SectionId", "OpenedAtUtc", "OpenedByUserId", "TemplateSnapshotJson")
            VALUES ({number}, 0, 0, {sample}, {fixture.SeededSectionId}, now(), {fixture.SeededUserId}, jsonb_build_object())
            """);

    private async Task<(int Sample, int Order)> SeedOrderAsync(string? legacyCode = null)
    {
        await using var db = fixture.CreateDbContext();
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var cause = new CauseOfTesting { Name = $"Schema {suffix}", IsActive = true };
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();
        var sample = new Sample
        {
            ReferenceNumber = $"SC-{suffix}",
            Category = SampleCategory.FinishedProduct,
            CauseOfTestingId = cause.Id,
            ReceivedByUserId = fixture.SeededUserId,
            OosGroupCode = legacyCode
        };
        var order = new TestOrder { Sample = sample, TestCode = suffix, SectionId = fixture.SeededSectionId, IsSuperseded = true };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();
        return (sample.Id, order.Id);
    }

    private static string NewNumber() => $"TEST-{Guid.NewGuid():N}"[..20];
}
