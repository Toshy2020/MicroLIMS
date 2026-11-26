using System.Data.Common;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Competency;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;
using static MicroLIMS.Tests.IntegrationTests.ItemSopResolverPostgresTests;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class CompetencyGuardPostgresTests(PostgresTestFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2037, 1, 30, 22, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2037, 1, 31);
    private static readonly DateTime Withdrawal = new(2037, 1, 29, 23, 0, 0, DateTimeKind.Utc);

    [PostgresFact]
    public async Task Gate_off_allows_unqualified_user()
    {
        foreach (var enabled in new bool?[] { null, false })
        {
            var setup = await ArrangeAsync(gate: enabled, sop: "missing");
            var log = new CommandCount();
            var result = await EvaluateAsync(setup, log);
            Assert.True(result.Allowed);
            Assert.Empty(result.Reasons);
            Assert.Null(result.SopNotChecked);
            Assert.Equal(2, log.Reads);
        }
    }

    [PostgresFact]
    public async Task Not_qualified_refused()
    {
        var setup = await ArrangeAsync();
        var reason = AssertRefused(await EvaluateAsync(setup), "Competency.NotQualified", $"You are not qualified for {setup.Code}.");
        Assert.Equal(setup.Code, reason.Params["test"]);
    }

    [PostgresFact]
    public async Task Current_qualification_allows()
    {
        var setup = await ArrangeAsync();
        await QualifyAsync(setup, Today.AddDays(2));
        Assert.True((await EvaluateAsync(setup)).Allowed);
    }

    [PostgresFact]
    public async Task Qualification_for_other_test_or_user_ignored()
    {
        var setup = await ArrangeAsync();
        var other = await ArrangeAsync();
        await QualifyAsync(setup with { DefinitionId = other.DefinitionId });
        await QualifyAsync(setup with { UserId = fixture.SeededControllerUserId });
        var commands = new CommandCount();
        AssertRefused(await EvaluateAsync(setup, commands), "Competency.NotQualified");
        Assert.Contains("WHERE", Assert.Single(commands.Sql, sql => sql.Contains("FROM \"TestDefinitions\"")));
        Assert.Contains("\"TestDefinitionId\" = ANY", Assert.Single(commands.Sql, sql => sql.Contains("FROM \"AnalystQualifications\"")));
    }

    [PostgresFact]
    public async Task EnsureAllowedAsync_with_both_qualification_and_sop_missing_throws_both_coded_details()
    {
        var setup = await ArrangeAsync(sop: "effective");
        await using var act = fixture.CreateDbContext();
        var guard = new CompetencyGuard(act, new LabClock(new FixedTimeProvider(Now), LabClock.ResolveTimeZone("Africa/Cairo")), new ItemSopResolver(act));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            guard.EnsureAllowedAsync(setup.UserId, setup.OrderIds[0]));

        Assert.NotNull(ex.Details);
        Assert.Equal(2, ex.Details!.Count);
        Assert.Equal("Competency.NotQualified", ex.Details[0].Code);
        Assert.Equal("Competency.SopNotAcknowledged", ex.Details[1].Code);
        Assert.Contains($"You are not qualified for {setup.Code}.", ex.Message);
        Assert.Contains($"You have not acknowledged {setup.SopCode} revision 3.", ex.Message);
    }

    [PostgresFact]
    public async Task Missing_definition_fails_closed()
    {
        var setup = await ArrangeAsync(missingDefinition: true);
        await QualifyAsync(setup);
        AssertRefused(await EvaluateAsync(setup), "Competency.NotQualified");
    }

    [PostgresFact]
    public async Task Expires_on_today_still_allowed()
    {
        var setup = await ArrangeAsync();
        await QualifyAsync(setup, Today);
        Assert.True((await EvaluateAsync(setup)).Allowed);
    }

    [PostgresFact]
    public async Task Expired_refused_with_lab_date()
    {
        var setup = await ArrangeAsync();
        await QualifyAsync(setup, Today.AddDays(-3));
        var reason = AssertRefused(await EvaluateAsync(setup), "Competency.QualificationExpired",
            $"Your qualification for {setup.Code} expired on 28 Jan 2037.");
        Assert.Equal("28 Jan 2037", reason.Params["date"]);
    }

    [PostgresFact]
    public async Task Expiry_uses_lab_date()
    {
        var setup = await ArrangeAsync();
        await QualifyAsync(setup, DateOnly.FromDateTime(Now.UtcDateTime));
        AssertRefused(await EvaluateAsync(setup), "Competency.QualificationExpired");
    }

    [PostgresFact]
    public async Task Withdrawn_refused_with_lab_date()
    {
        var setup = await ArrangeAsync();
        await QualifyAsync(setup, withdrawn: true);
        AssertRefused(await EvaluateAsync(setup), "Competency.QualificationWithdrawn",
            $"Your qualification for {setup.Code} was withdrawn on 30 Jan 2037.");
    }

    [PostgresFact]
    public async Task Requalified_after_withdrawal_allowed()
    {
        var setup = await ArrangeAsync();
        await QualifyAsync(setup, withdrawn: true);
        await QualifyAsync(setup);
        Assert.True((await EvaluateAsync(setup)).Allowed);
    }

    [PostgresFact]
    public async Task Latest_withdrawn_governs_even_if_older_valid()
    {
        var setup = await ArrangeAsync();
        await QualifyAsync(setup);
        await QualifyAsync(setup, withdrawn: true);
        AssertRefused(await EvaluateAsync(setup), "Competency.QualificationWithdrawn");
    }

    [PostgresFact]
    public async Task Sop_current_revision_acknowledged_allows()
    {
        foreach (var status in new[] { TrainingAssignmentStatus.Acknowledged, TrainingAssignmentStatus.CompletedPassed })
        {
            var setup = await ArrangeAsync(sop: "effective");
            await QualifyAsync(setup);
            await AssignAsync(setup, 3, status);
            var result = await EvaluateAsync(setup);
            Assert.True(result.Allowed);
            Assert.Null(result.SopNotChecked);
        }
    }

    [PostgresFact]
    public async Task Sop_not_acknowledged_refused()
    {
        var setup = await ArrangeAsync(sop: "effective");
        await QualifyAsync(setup);
        var result = await EvaluateAsync(setup);
        AssertRefused(result, "Competency.SopNotAcknowledged",
            $"You have not acknowledged {setup.SopCode} revision 3.");
        Assert.Null(result.SopNotChecked);
    }

    [PostgresFact]
    public async Task Grace_allows_until_due_date()
    {
        var setup = await ArrangeGraceAsync(TrainingAssignmentStatus.Reading);
        Assert.True((await EvaluateAsync(setup)).Allowed);
    }

    [PostgresFact]
    public async Task Grace_ends_at_due_date()
    {
        var setup = await ArrangeGraceAsync(TrainingAssignmentStatus.Assigned, Now.UtcDateTime.AddSeconds(-1));
        AssertRefused(await EvaluateAsync(setup), "Competency.SopNotAcknowledged");
    }

    [PostgresFact]
    public async Task Grace_needs_open_assignment()
    {
        foreach (var status in new TrainingAssignmentStatus?[]
                 { TrainingAssignmentStatus.Cancelled, TrainingAssignmentStatus.Overdue, null, TrainingAssignmentStatus.KafPending })
        {
            var setup = await ArrangeGraceAsync(status);
            Assert.Equal(status == TrainingAssignmentStatus.KafPending, (await EvaluateAsync(setup)).Allowed);
        }
    }

    [PostgresFact]
    public async Task Acknowledged_two_revisions_back_refused()
    {
        var setup = await ArrangeGraceAsync(TrainingAssignmentStatus.Assigned, previousSequence: 1);
        AssertRefused(await EvaluateAsync(setup), "Competency.SopNotAcknowledged");
    }

    [PostgresFact]
    public async Task Unresolvable_sop_allowed_with_text()
    {
        foreach (var (sop, message) in new[]
                 {
                     ("blank", "SopNotCheckedNoSopNumber"),
                     ("missing", "SopNotCheckedNotFound"),
                     ("draft", "SopNotCheckedNoEffectiveRevision")
                 })
        {
            var setup = await ArrangeAsync(sop: sop);
            await QualifyAsync(setup);
            var result = await EvaluateAsync(setup);
            Assert.True(result.Allowed);
            Assert.Empty(result.Reasons);
            Assert.Equal($"Competency.{message}", result.SopNotChecked?.Code);
            Assert.StartsWith("SOP not checked:", result.SopNotChecked?.Text);
        }
    }

    // CONF-212: case-variant DocumentMasters can no longer exist in PostgreSQL, so Ambiguous is not reachable there.

    [PostgresFact]
    public async Task Sample_without_item_checks_qualification_only()
    {
        var setup = await ArrangeAsync();
        AssertRefused(await EvaluateAsync(setup), "Competency.NotQualified");
        await QualifyAsync(setup);
        var result = await EvaluateAsync(setup);
        Assert.True(result.Allowed);
        Assert.Null(result.SopNotChecked);
    }

    [PostgresFact]
    public async Task Both_reasons_listed_qualification_first()
    {
        var setup = await ArrangeAsync(sop: "effective");
        await using var act = fixture.CreateDbContext();
        var guard = CreateGuard(act);
        var result = await guard.EvaluateAsync(setup.UserId, setup.OrderIds[0]);
        Assert.Equal(new[] { "Competency.NotQualified", "Competency.SopNotAcknowledged" }, result.Reasons.Select(r => r.Code));
        var text = $"You are not qualified for {setup.Code}. You have not acknowledged {setup.SopCode} revision 3.";
        Assert.Equal(text, result.ToActionCheck().Reason);
        Assert.Equal("Competency.NotQualified", result.ToActionCheck().Code);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => guard.EnsureAllowedAsync(setup.UserId, setup.OrderIds[0]));
        Assert.Equal("Competency.NotQualified", error.Code);
        Assert.Equal(text, error.Message);
        Assert.Equal(setup.Code, error.Parameters?["test"]);
    }

    [PostgresFact]
    public async Task EvaluateMany_per_order_and_per_section_gate()
    {
        var refused = await ArrangeAsync();
        var qualified = await ArrangeAsync();
        var disabled = await ArrangeAsync(gate: false, definitionSectionId: qualified.SectionId, sop: "effective");
        await QualifyAsync(qualified);
        await using var act = fixture.CreateDbContext();
        var ids = new[] { refused.OrderIds[0], qualified.OrderIds[0], disabled.OrderIds[0] };
        var decisions = await CreateGuard(act).EvaluateManyAsync(refused.UserId, ids);
        Assert.Equal(3, decisions.Count);
        AssertRefused(decisions[ids[0]], "Competency.NotQualified");
        Assert.True(decisions[ids[1]].Allowed);
        Assert.True(decisions[ids[2]].Allowed);
        Assert.Null(decisions[ids[2]].SopNotChecked);
    }

    [PostgresFact]
    public async Task EvaluateMany_query_count_independent_of_order_count()
    {
        var setup = await ArrangeAsync(sop: "effective", count: 25);
        await QualifyAsync(setup);
        await AssignAsync(setup, 3, TrainingAssignmentStatus.Acknowledged);
        var one = new CommandCount();
        Assert.True((await EvaluateAsync(setup, one)).Allowed);
        var many = new CommandCount();
        await using var act = fixture.CreateDbContext(many);
        var decisions = await CreateGuard(act).EvaluateManyAsync(setup.UserId, setup.OrderIds);
        Assert.Equal(25, decisions.Count);
        Assert.All(decisions.Values, decision => Assert.True(decision.Allowed));
        Assert.Equal(one.Reads, many.Reads);
        Assert.InRange(many.Reads, 1, 8);
    }

    [PostgresFact]
    public async Task Assignee_wording_uses_name()
    {
        var setup = await ArrangeAsync(sop: "effective");
        await using var act = fixture.CreateDbContext();
        var result = (await CreateGuard(act).EvaluateAssigneeAsync(setup.UserId, setup.OrderIds))[setup.OrderIds[0]];
        Assert.Equal(new[] { "Competency.AssigneeNotQualified", "Competency.AssigneeSopNotAcknowledged" },
            result.Reasons.Select(r => r.Code));
        Assert.Equal($"QA Document Admin is not qualified for {setup.Code}.", result.ReasonTexts[0]);
        Assert.Equal($"QA Document Admin has not acknowledged {setup.SopCode} revision 3.", result.ReasonTexts[1]);
        Assert.All(result.Reasons, reason => Assert.Equal("QA Document Admin", reason.Params["analyst"]));
    }

    [PostgresFact]
    public async Task Unknown_test_order_throws_NotFound()
    {
        await using var act = fixture.CreateDbContext();
        await Assert.ThrowsAsync<NotFoundException>(() => CreateGuard(act).EvaluateAsync(fixture.SeededUserId, int.MaxValue));
    }

    [PostgresFact]
    public async Task Guard_writes_nothing()
    {
        var setup = await ArrangeAsync(sop: "effective");
        await QualifyAsync(setup);
        int before;
        await using (var read = fixture.CreateDbContext())
            before = await read.AuditLogs.AsNoTracking().CountAsync();
        await using (var act = fixture.CreateDbContext())
        {
            AssertRefused(await CreateGuard(act).EvaluateAsync(setup.UserId, setup.OrderIds[0]), "Competency.SopNotAcknowledged");
            Assert.Empty(act.ChangeTracker.Entries());
        }
        await using var verify = fixture.CreateDbContext();
        Assert.Equal(before, await verify.AuditLogs.AsNoTracking().CountAsync());
    }

    private static CompetencyReason AssertRefused(CompetencyDecision decision, string code, string? text = null)
    {
        Assert.False(decision.Allowed);
        Assert.Null(decision.ViaOverrideId);
        var reason = Assert.Single(decision.Reasons);
        Assert.Equal(code, reason.Code);
        if (text is not null)
            Assert.Equal(text, reason.Text);
        return reason;
    }

    private static CompetencyGuard CreateGuard(MicroLimsDbContext db) =>
        new(db, new LabClock(new FixedTimeProvider(Now), LabClock.ResolveTimeZone("Africa/Cairo")), new ItemSopResolver(db));

    private async Task<CompetencyDecision> EvaluateAsync(Setup setup, params IInterceptor[] interceptors)
    {
        await using var act = fixture.CreateDbContext(interceptors);
        return await CreateGuard(act).EvaluateAsync(setup.UserId, setup.OrderIds[0]);
    }

    private async Task<Setup> ArrangeGraceAsync(TrainingAssignmentStatus? status, DateTime? due = null, int previousSequence = 2)
    {
        var setup = await ArrangeAsync(sop: "effective");
        await QualifyAsync(setup);
        await AssignAsync(setup, previousSequence, TrainingAssignmentStatus.Acknowledged);
        if (status is { } open)
            await AssignAsync(setup, 3, open, due);
        return setup;
    }

    private sealed record Setup(int UserId, int DefinitionId, int SectionId, string Code, int[] OrderIds, string? SopCode,
        int? MasterId, IReadOnlyDictionary<int, int> Revisions);

    private async Task<Setup> ArrangeAsync(bool? gate = true, string? sop = null, int count = 1,
        bool missingDefinition = false, int? definitionSectionId = null)
    {
        await using var db = fixture.CreateDbContext();
        var section = new DocumentSection { Code = NewCode(), Name = $"Competency lab {NewCode()}", DepartmentId = fixture.SeededDepartmentId };
        db.DocumentSections.Add(section);
        var cause = new CauseOfTesting { Name = NewCode(), IsActive = true };
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();

        if (gate is { } enabled)
            db.CompetencySectionSettings.Add(new CompetencySectionSetting
            {
                SectionId = section.Id, GateEnabled = enabled, ChangedAt = Now.UtcDateTime
            });
        var definition = new TestDefinition
        {
            Code = NewCode(), DisplayName = "Competency test",
            SectionId = definitionSectionId ?? section.Id
        };
        db.TestDefinitions.Add(definition);
        await db.SaveChangesAsync();

        string? sopCode = null;
        Item? item = null;
        DocumentMaster? master = null;
        var revisions = new Dictionary<int, int>();
        if (sop is not null)
        {
            sopCode = sop == "blank" ? " " : NewCode();
            item = await AddItemAsync(db, sopCode);
            if (sop is "effective" or "ambiguous" or "draft")
                master = await AddMasterAsync(db, fixture, sopCode);
            if (sop == "ambiguous")
                await AddMasterAsync(db, fixture, sopCode.ToLowerInvariant());
            if (sop == "effective" && master is not null)
            {
                revisions[1] = (await AddRevisionAsync(db, fixture, master, 1, DocumentRevisionStatus.Superseded)).Id;
                revisions[2] = (await AddRevisionAsync(db, fixture, master, 2, DocumentRevisionStatus.Superseded)).Id;
                revisions[3] = (await AddRevisionAsync(db, fixture, master, 3, DocumentRevisionStatus.Effective, current: true)).Id;
            }
        }

        var code = missingDefinition ? NewCode() : definition.Code;
        var orders = Enumerable.Range(0, count).Select(_ => new TestOrder
        {
            TestCode = code, SectionId = section.Id,
            Sample = new Sample
            {
                ReferenceNumber = NewCode(), ItemId = item?.Id, CauseOfTestingId = cause.Id,
                Category = SampleCategory.FinishedProduct, ReceivedByUserId = fixture.SeededUserId, ReceivedAt = Now.UtcDateTime
            }
        }).ToArray();
        db.TestOrders.AddRange(orders);
        await db.SaveChangesAsync();
        return new(fixture.SeededUserId, definition.Id, section.Id, code, orders.Select(o => o.Id).ToArray(), sopCode, master?.Id, revisions);
    }

    private async Task QualifyAsync(Setup setup, DateOnly? expires = null, bool withdrawn = false)
    {
        await using var db = fixture.CreateDbContext();
        var headId = fixture.SeededControllerUserId;
        var id = await fixture.CreateSequenceHelper(db).ReserveIdAsync<AnalystQualification>();
        var qualification = new AnalystQualification
        {
            Id = id, UserId = setup.UserId, TestDefinitionId = setup.DefinitionId,
            Basis = QualificationBasis.ObservedPractice, Evidence = "Observed the method",
            QualifiedOn = Today.AddMonths(-1), ExpiresOn = expires, SignedByUserId = headId,
            Signature = new ElectronicSignature
            {
                UserId = headId, EntityType = nameof(AnalystQualification), EntityId = id,
                MeaningOfSignature = SignatureMeaning.AnalystQualified, SignedAt = Now.UtcDateTime.AddDays(-4)
            }
        };
        db.AnalystQualifications.Add(qualification);
        await db.SaveChangesAsync();
        if (withdrawn)
        {
            qualification.WithdrawnOn = Withdrawal;
            qualification.WithdrawnByUserId = headId;
            qualification.WithdrawnReason = "Evidence withdrawn";
            qualification.WithdrawnSignature = new ElectronicSignature
            {
                UserId = headId, EntityType = nameof(AnalystQualification), EntityId = id, SignedAt = Withdrawal,
                MeaningOfSignature = SignatureMeaning.AnalystQualificationWithdrawn, Comment = qualification.WithdrawnReason
            };
            await db.SaveChangesAsync();
        }
    }

    private async Task AssignAsync(Setup setup, int sequence, TrainingAssignmentStatus status, DateTime? due = null)
    {
        await using var db = fixture.CreateDbContext();
        db.DocumentTrainingAssignments.Add(new DocumentTrainingAssignment
        {
            DocumentMasterId = setup.MasterId ?? throw new InvalidOperationException(),
            DocumentRevisionId = setup.Revisions[sequence], AssignedUserId = setup.UserId,
            Status = status, DueDateUtc = due ?? Now.UtcDateTime, AssignedDateUtc = Now.UtcDateTime.AddDays(-1),
            CreatedAtUtc = Now.UtcDateTime.AddDays(-1), CreatedByUserId = fixture.SeededControllerUserId
        });
        await db.SaveChangesAsync();
    }

    private sealed class CommandCount : DbCommandInterceptor
    {
        public List<string> Sql { get; } = [];
        public int Reads => Sql.Count;

        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            Sql.Add(command.CommandText);
            return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
        }
    }
}
