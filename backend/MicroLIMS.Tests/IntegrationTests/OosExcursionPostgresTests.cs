using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Oos;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public sealed class OosExcursionPostgresTests(PostgresTestFixture fixture)
{
    private readonly ActionGuardScenarios _scenarios = new(fixture);

    [PostgresFact]
    public async Task Action_limit_result_opens_excursion_and_does_not_block_approval_or_certificate()
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var analyst = await _scenarios.AnalystAsync(section);
        var head = await _scenarios.HeadAsync(section);
        var seed = await _scenarios.UnderReviewAsync(section, analystId: analyst.Id);
        await using (var arrange = fixture.CreateDbContext())
        {
            arrange.SampleLocations.Add(new SampleLocation
            {
                SampleId = seed.Sample.Id,
                TestOrderId = seed.Orders[0].Id,
                Status = ResultStatus.ActionLimitExceeded,
                ReportedResult = "60 CFU"
            });
            await arrange.SaveChangesAsync();
        }

        var clock = new FixedTimeProvider(new DateTimeOffset(2042, 4, 1, 9, 30, 0, TimeSpan.Zero));
        await using (var act = fixture.CreateDbContext(clock))
        {
            await TestServiceFactory.SampleReview(act, clock)
                .CompleteReviewAsync(seed.Sample.Id, reviewer.Id, ActionGuardScenarios.Password, null, null, section.Id);
        }

        OosInvestigation excursion;
        await using (var verify = fixture.CreateDbContext())
        {
            excursion = await verify.OosInvestigations.AsNoTracking().Include(i => i.Results)
                .SingleAsync(i => i.SampleId == seed.Sample.Id);
            Assert.Equal(OosInvestigationKind.Excursion, excursion.Kind);
            Assert.Equal(OosInvestigationStatus.PhaseOne, excursion.Status);
            Assert.Equal(section.Id, excursion.SectionId);
            Assert.Equal(reviewer.Id, excursion.OpenedByUserId);
            Assert.Equal(clock.GetUtcNow().UtcDateTime, excursion.OpenedAtUtc);
            Assert.StartsWith("OOS0442", excursion.Number);
            var result = Assert.Single(excursion.Results);
            Assert.Equal(seed.Orders[0].Id, result.TestOrderId);
            Assert.Equal(OosOutcomeClass.ActionLimitExceeded, result.OutcomeAtOpening);

            var sample = await verify.Samples.AsNoTracking().SingleAsync(s => s.Id == seed.Sample.Id);
            Assert.Null(sample.OosGroupCode);
            Assert.True(await verify.AuditLogs.AsNoTracking().AnyAsync(a =>
                a.EntityName == nameof(OosInvestigation) && a.EntityId == excursion.Id.ToString() &&
                a.UserId == reviewer.Id && a.Action == "Create"));
            Assert.True(await verify.AuditLogs.AsNoTracking().AnyAsync(a =>
                a.EntityName == nameof(OosInvestigationResult) && a.EntityId == result.Id.ToString() &&
                a.UserId == reviewer.Id && a.Action == "Create"));
        }

        await using (var act = fixture.CreateDbContext(clock))
        {
            await TestServiceFactory.SampleApproval(act).DecideAsync(
                seed.Sample.Id, head.Id, ActionGuardScenarios.Password, ApprovalDecision.Approve, null, null, sectionId: section.Id);
        }
        await using (var act = fixture.CreateDbContext(clock))
        {
            Assert.NotNull(await TestServiceFactory.SampleSummary(act).GetCertificateSummaryAsync(seed.Sample.Id, null));
        }

        await using (var verify = fixture.CreateDbContext())
        {
            var sample = await verify.Samples.AsNoTracking().SingleAsync(s => s.Id == seed.Sample.Id);
            Assert.Equal(SampleStatus.Approved, sample.Status);
        }
    }

    [PostgresFact]
    public async Task Alert_limit_opens_excursion_only_when_switched_on()
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var analyst = await _scenarios.AnalystAsync(section);
        var head = await OosHeadAsync(section);
        var offSample = await _scenarios.UnderReviewAsync(section, analystId: analyst.Id);
        await using (var arrange = fixture.CreateDbContext())
        {
            arrange.SampleLocations.Add(new SampleLocation
            {
                SampleId = offSample.Sample.Id,
                TestOrderId = offSample.Orders[0].Id,
                Status = ResultStatus.AlertLimitExceeded,
                ReportedResult = "15 CFU"
            });
            await arrange.SaveChangesAsync();
        }

        await _scenarios.CompleteReviewAsync(new(reviewer.Id, offSample.Sample.Id));
        await using (var verify = fixture.CreateDbContext())
        {
            Assert.False(await verify.OosInvestigations.AsNoTracking().AnyAsync(i => i.SampleId == offSample.Sample.Id));
        }

        await using (var arrange = fixture.CreateDbContext())
        {
            await new OosTemplateService(arrange, new UserSectionScopeService(arrange), new PermissionService(arrange))
                .SaveAsync(section.Id, OosInvestigationKind.Excursion,
                    new SaveOosTemplateRequest(3, 10, AlertOpensExcursionReview: true, []), expectedVersion: 0, head.Id);
        }

        var onSample = await _scenarios.UnderReviewAsync(section, analystId: analyst.Id);
        await using (var arrange = fixture.CreateDbContext())
        {
            arrange.SampleLocations.Add(new SampleLocation
            {
                SampleId = onSample.Sample.Id,
                TestOrderId = onSample.Orders[0].Id,
                Status = ResultStatus.AlertLimitExceeded,
                ReportedResult = "15 CFU"
            });
            await arrange.SaveChangesAsync();
        }

        await _scenarios.CompleteReviewAsync(new(reviewer.Id, onSample.Sample.Id));
        await using (var verify = fixture.CreateDbContext())
        {
            var excursion = await verify.OosInvestigations.AsNoTracking().Include(i => i.Results)
                .SingleAsync(i => i.SampleId == onSample.Sample.Id);
            Assert.Equal(OosInvestigationKind.Excursion, excursion.Kind);
            var result = Assert.Single(excursion.Results);
            Assert.Equal(OosOutcomeClass.AlertLimitExceeded, result.OutcomeAtOpening);
            Assert.NotNull(excursion.PhaseOneDueDate);
            Assert.NotNull(excursion.DueDate);
        }
    }

    [PostgresFact]
    public async Task Monitoring_plan_sample_opens_no_excursion()
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var analyst = await _scenarios.AnalystAsync(section);
        var head = await _scenarios.HeadAsync(section);
        var seed = await _scenarios.UnderReviewAsync(section, analystId: analyst.Id);

        await using (var arrange = fixture.CreateDbContext())
        {
            var room = new Room
            {
                Name = $"Room-{Guid.NewGuid():N}"[..16],
                Department = new Department { Name = $"Dept-{Guid.NewGuid():N}"[..16] },
                GradeClassification = "A"
            };
            arrange.Rooms.Add(room);
            await arrange.SaveChangesAsync();

            var revision = new MonitoringPlanRevision
            {
                Year = 2042,
                RevisionNumber = 1,
                Status = PlanRevisionStatus.Approved,
                EffectiveFrom = new DateOnly(2042, 1, 1),
                GeneratedAt = DateTime.UtcNow,
                GeneratedByUserId = head.Id
            };
            arrange.MonitoringPlanRevisions.Add(revision);
            await arrange.SaveChangesAsync();
            var occurrence = new MonitoringOccurrence
            {
                CreatedByRevisionId = revision.Id,
                Category = MonitoringCategory.EnvironmentalMonitoring,
                TargetType = MonitoringTargetType.Room,
                RoomId = room.Id,
                PeriodKey = "2042-04",
                FrequencySnapshot = "Monthly",
                WindowStart = new DateOnly(2042, 4, 1),
                WindowEnd = new DateOnly(2042, 4, 30),
                OriginalWindowEnd = new DateOnly(2042, 4, 30)
            };
            arrange.MonitoringOccurrences.Add(occurrence);
            await arrange.SaveChangesAsync();

            arrange.SampleLocations.Add(new SampleLocation
            {
                SampleId = seed.Sample.Id,
                TestOrderId = seed.Orders[0].Id,
                MonitoringOccurrenceId = occurrence.Id,
                Status = ResultStatus.ActionLimitExceeded,
                ReportedResult = "60 CFU"
            });
            await arrange.SaveChangesAsync();
        }

        await _scenarios.CompleteReviewAsync(new(reviewer.Id, seed.Sample.Id));
        await using var verify = fixture.CreateDbContext();
        Assert.False(await verify.OosInvestigations.AsNoTracking().AnyAsync(i => i.SampleId == seed.Sample.Id));
    }

    [PostgresFact]
    public async Task Failing_and_action_limit_orders_in_one_section_open_one_of_each_kind()
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var analyst = await _scenarios.AnalystAsync(section);
        var seed = await _scenarios.UnderReviewAsync(section, orders: 2, analystId: analyst.Id);
        await using (var arrange = fixture.CreateDbContext())
        {
            arrange.SampleLocations.AddRange(
                new SampleLocation
                {
                    SampleId = seed.Sample.Id,
                    TestOrderId = seed.Orders[0].Id,
                    Status = ResultStatus.OutOfSpecification,
                    ReportedResult = "200 CFU"
                },
                new SampleLocation
                {
                    SampleId = seed.Sample.Id,
                    TestOrderId = seed.Orders[1].Id,
                    Status = ResultStatus.ActionLimitExceeded,
                    ReportedResult = "60 CFU"
                });
            await arrange.SaveChangesAsync();
        }

        await _scenarios.CompleteReviewAsync(new(reviewer.Id, seed.Sample.Id));
        await using var verify = fixture.CreateDbContext();
        var investigations = await verify.OosInvestigations.AsNoTracking().Include(i => i.Results)
            .Where(i => i.SampleId == seed.Sample.Id).OrderBy(i => i.Id).ToListAsync();
        Assert.Equal(2, investigations.Count);

        var oos = Assert.Single(investigations, i => i.Kind == OosInvestigationKind.Oos);
        var excursion = Assert.Single(investigations, i => i.Kind == OosInvestigationKind.Excursion);
        Assert.NotEqual(oos.Number, excursion.Number);
        Assert.Equal(seed.Orders[0].Id, Assert.Single(oos.Results).TestOrderId);
        Assert.Equal(OosOutcomeClass.Fails, oos.Results[0].OutcomeAtOpening);
        Assert.Equal(seed.Orders[1].Id, Assert.Single(excursion.Results).TestOrderId);
        Assert.Equal(OosOutcomeClass.ActionLimitExceeded, excursion.Results[0].OutcomeAtOpening);

        var sample = await verify.Samples.AsNoTracking().SingleAsync(s => s.Id == seed.Sample.Id);
        Assert.Equal(oos.Number, sample.OosGroupCode);
    }

    [PostgresFact]
    public async Task Manual_excursion_opening_respects_permission_and_section_scope()
    {
        var section = await _scenarios.SectionAsync();
        var otherSection = await _scenarios.SectionAsync();
        var analyst = await OosAnalystAsync(section);
        var reviewer = await _scenarios.ReviewerAsync(section);
        var otherHead = await OosHeadAsync(otherSection);
        var seed = await _scenarios.UnderReviewAsync(section, analystId: analyst.Id);

        var deniedRole = await Assert.ThrowsAsync<CodedAccessDeniedException>(() => RunManualOpenAsync(seed.Orders[0].Id, "OOT rule 1", reviewer.Id));
        Assert.Equal(MessageCodes.OosInvestigationAccessDenied, deniedRole.Code);

        var deniedScope = await Assert.ThrowsAsync<CodedAccessDeniedException>(() => RunManualOpenAsync(seed.Orders[0].Id, "OOT rule 1", otherHead.Id));
        Assert.Equal(MessageCodes.OosSectionAccessDenied, deniedScope.Code);

        var opened = await RunManualOpenAsync(seed.Orders[0].Id, "Control chart Run Rule 2", analyst.Id);
        await using var verify = fixture.CreateDbContext();
        var excursion = await verify.OosInvestigations.AsNoTracking().Include(i => i.Results)
            .SingleAsync(i => i.Id == opened.Id);
        Assert.Equal(OosInvestigationKind.Excursion, excursion.Kind);
        Assert.Equal(OosInvestigationStatus.PhaseOne, excursion.Status);
        Assert.Equal(analyst.Id, excursion.OpenedByUserId);
        Assert.Equal(seed.Orders[0].Id, Assert.Single(excursion.Results).TestOrderId);
        Assert.True(await verify.OosInvestigationEntrys.AsNoTracking().AnyAsync(e =>
            e.InvestigationId == excursion.Id && e.Kind == OosEntryKind.Note &&
            e.Text == "Control chart Run Rule 2" && e.RecordedByUserId == analyst.Id));
        Assert.True(await verify.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityName == nameof(OosInvestigation) && a.EntityId == excursion.Id.ToString() &&
            a.UserId == analyst.Id && a.Action == "Create"));
    }

    [PostgresFact]
    public async Task Excursion_closure_enforces_checklist_category_sod_password_version_and_scope()
    {
        var section = await _scenarios.SectionAsync();
        var otherSection = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var performingAnalyst = await OosAnalystAsync(section);
        var head = await OosHeadAsync(section);
        var otherHead = await OosHeadAsync(otherSection);

        string itemKey;
        int activeCategoryId;
        int inactiveCategoryId;
        await using (var arrange = fixture.CreateDbContext())
        {
            var template = await new OosTemplateService(arrange, new UserSectionScopeService(arrange), new PermissionService(arrange))
                .SaveAsync(section.Id, OosInvestigationKind.Excursion,
                    new SaveOosTemplateRequest(null, null, false, [new OosTemplateItem(null, 1, 1, "Verify sampling conditions", true)]),
                    expectedVersion: 0, head.Id);
            itemKey = Assert.Single(template.Items).Key!;
            var activeCat = await arrange.OosCauseCategories.AsNoTracking().FirstAsync(c => c.IsActive && !c.IsNoCauseFound);
            activeCategoryId = activeCat.Id;
            var inactive = new OosCauseCategory { Name = $"Retired {Guid.NewGuid():N}"[..24], IsActive = false };
            arrange.OosCauseCategories.Add(inactive);
            await arrange.SaveChangesAsync();
            inactiveCategoryId = inactive.Id;
        }

        var seed = await _scenarios.UnderReviewAsync(section, analystId: performingAnalyst.Id);
        await using (var arrange = fixture.CreateDbContext())
        {
            arrange.SampleLocations.Add(new SampleLocation
            {
                SampleId = seed.Sample.Id,
                TestOrderId = seed.Orders[0].Id,
                Status = ResultStatus.ActionLimitExceeded,
                ReportedResult = "60 CFU"
            });
            await arrange.SaveChangesAsync();
        }
        await _scenarios.CompleteReviewAsync(new(reviewer.Id, seed.Sample.Id));

        // Grant Oos.Manage to performingHead who also entered a result on seed.Orders[0].
        var performingHead = await OosHeadAsync(section);
        await using (var arrange = fixture.CreateDbContext())
        {
            arrange.Results.Add(new Result
            {
                TestOrderId = seed.Orders[0].Id,
                InterpretedValue = nameof(ResultStatus.ActionLimitExceeded),
                EnteredByUserId = performingHead.Id
            });
            await arrange.SaveChangesAsync();
        }

        uint currentVersion;
        int excursionId;
        await using (var read = fixture.CreateDbContext())
        {
            var inv = await read.OosInvestigations.AsNoTracking().SingleAsync(i => i.SampleId == seed.Sample.Id);
            excursionId = inv.Id;
            currentVersion = inv.Version;
        }

        // Unanswered checklist item refused.
        var unanswered = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            RunCloseAsync(excursionId, activeCategoryId, "Transient drift", "Retrained operator", ActionGuardScenarios.Password, currentVersion, head.Id));
        Assert.Equal(MessageCodes.OosExcursionChecklistIncomplete, unanswered.Code);

        // Answer checklist item.
        await using (var act = fixture.CreateDbContext())
        {
            act.ExpectedVersion = currentVersion;
            await TestServiceFactory.OosInvestigation(act).AnswerChecklistAsync(
                excursionId, 1, itemKey, OosChecklistAnswer.Yes, "Checked", performingAnalyst.Id);
        }
        await using (var read = fixture.CreateDbContext())
        {
            currentVersion = (await read.OosInvestigations.AsNoTracking().SingleAsync(i => i.Id == excursionId)).Version;
        }

        // Other-section Section Head refused.
        var wrongScope = await Assert.ThrowsAsync<CodedAccessDeniedException>(() =>
            RunCloseAsync(excursionId, activeCategoryId, "Transient drift", "Retrained operator", ActionGuardScenarios.Password, currentVersion, otherHead.Id));
        Assert.Equal(MessageCodes.OosSectionAccessDenied, wrongScope.Code);

        // Signer who performed the test refused.
        var sod = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            RunCloseAsync(excursionId, activeCategoryId, "Transient drift", "Retrained operator", ActionGuardScenarios.Password, currentVersion, performingHead.Id));
        Assert.Equal(MessageCodes.OosSignerPerformedTest, sod.Code);

        // Inactive cause category refused.
        var inactiveErr = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            RunCloseAsync(excursionId, inactiveCategoryId, "Transient drift", "Retrained operator", ActionGuardScenarios.Password, currentVersion, head.Id));
        Assert.Equal(MessageCodes.OosConclusionCauseInactive, inactiveErr.Code);

        // Stale version refused.
        var staleErr = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            RunCloseAsync(excursionId, activeCategoryId, "Transient drift", "Retrained operator", ActionGuardScenarios.Password, currentVersion - 1, head.Id));
        Assert.Equal(MessageCodes.OosVersionConflict, staleErr.Code);

        // Wrong password refused and changes nothing.
        var badPwd = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            RunCloseAsync(excursionId, activeCategoryId, "Transient drift", "Retrained operator", "wrong-password", currentVersion, head.Id));
        Assert.Equal(MessageCodes.OosConclusionPasswordFailed, badPwd.Code);
        await using (var verify = fixture.CreateDbContext())
        {
            var unchanged = await verify.OosInvestigations.AsNoTracking().SingleAsync(i => i.Id == excursionId);
            Assert.Equal(OosInvestigationStatus.PhaseOne, unchanged.Status);
            Assert.Null(unchanged.Conclusion);
            Assert.Null(unchanged.ConclusionSignatureId);
        }

        // Valid closure succeeds.
        var clock = new FixedTimeProvider(new DateTimeOffset(2042, 4, 2, 11, 0, 0, TimeSpan.Zero));
        await using (var act = fixture.CreateDbContext(clock))
        {
            await TestServiceFactory.OosInvestigation(act, clock).CloseExcursionAsync(
                excursionId,
                new CloseExcursionRequest(activeCategoryId, "Transient drift", "Cleaned probe and retrained", ActionGuardScenarios.Password),
                currentVersion, head.Id, "127.0.0.1");
        }

        await using (var verify = fixture.CreateDbContext())
        {
            var closed = await verify.OosInvestigations.AsNoTracking().SingleAsync(i => i.Id == excursionId);
            Assert.Equal(OosInvestigationStatus.Concluded, closed.Status);
            Assert.Equal(OosConclusion.ExcursionClosed, closed.Conclusion);
            Assert.Equal(activeCategoryId, closed.RootCauseCategoryId);
            Assert.Equal("Transient drift", closed.RootCauseDescription);
            Assert.Equal("Cleaned probe and retrained", closed.ConclusionSummary);
            Assert.Equal(head.Id, closed.ConcludedByUserId);
            Assert.Equal(clock.GetUtcNow().UtcDateTime, closed.ConcludedAtUtc);
            Assert.NotNull(closed.ConclusionSignatureId);

            var signature = await verify.ElectronicSignatures.AsNoTracking()
                .SingleAsync(s => s.Id == closed.ConclusionSignatureId);
            Assert.Equal(SignatureMeaning.ExcursionReviewClosed, signature.MeaningOfSignature);
            Assert.Equal(nameof(OosInvestigation), signature.EntityType);
            Assert.Equal(excursionId, signature.EntityId);
            Assert.Equal(head.Id, signature.UserId);
            Assert.Equal(clock.GetUtcNow().UtcDateTime, signature.SignedAt);

            var audit = await verify.AuditLogs.AsNoTracking()
                .Where(a => a.EntityName == nameof(OosInvestigation) && a.EntityId == excursionId.ToString() &&
                    a.Action == "Update" && a.UserId == head.Id)
                .OrderByDescending(a => a.Id)
                .FirstAsync();
            using var before = JsonDocument.Parse(Assert.IsType<string>(audit.PreviousValue));
            using var after = JsonDocument.Parse(Assert.IsType<string>(audit.NewValue));
            Assert.Equal((int)OosInvestigationStatus.PhaseOne, before.RootElement.GetProperty(nameof(OosInvestigation.Status)).GetInt32());
            Assert.Equal((int)OosInvestigationStatus.Concluded, after.RootElement.GetProperty(nameof(OosInvestigation.Status)).GetInt32());
            Assert.Equal((int)OosConclusion.ExcursionClosed, after.RootElement.GetProperty(nameof(OosInvestigation.Conclusion)).GetInt32());
        }
    }

    private Task<User> OosHeadAsync(DocumentSection section) =>
        _scenarios.UserAsync(RoleType.SectionHead, [PermissionConstants.OosManage], section);

    private Task<User> OosAnalystAsync(DocumentSection section) =>
        _scenarios.UserAsync(RoleType.Analyst, [PermissionConstants.OosContribute], section);

    private async Task<OosManualOpeningDto> RunManualOpenAsync(int testOrderId, string reason, int userId)
    {
        await using var act = fixture.CreateDbContext();
        return await TestServiceFactory.OosInvestigation(act).OpenManualExcursionAsync(testOrderId, reason, userId);
    }

    private async Task RunCloseAsync(int investigationId, int categoryId, string causeDescription, string actionTaken,
        string password, uint expectedVersion, int userId)
    {
        await using var act = fixture.CreateDbContext();
        await TestServiceFactory.OosInvestigation(act).CloseExcursionAsync(
            investigationId,
            new CloseExcursionRequest(categoryId, causeDescription, actionTaken, password),
            expectedVersion, userId, null);
    }
}
