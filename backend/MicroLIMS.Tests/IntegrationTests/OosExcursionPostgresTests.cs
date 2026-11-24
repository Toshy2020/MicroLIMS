using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Notifications;
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
            var createAudit = await verify.AuditLogs.AsNoTracking().SingleAsync(a =>
                a.EntityName == nameof(OosInvestigation) && a.EntityId == excursion.Id.ToString() &&
                a.UserId == reviewer.Id && a.Action == "Create");
            using var createNew = JsonDocument.Parse(Assert.IsType<string>(createAudit.NewValue));
            Assert.Equal((int)OosInvestigationKind.Excursion, createNew.RootElement.GetProperty(nameof(OosInvestigation.Kind)).GetInt32());
            Assert.Equal((int)OosInvestigationStatus.PhaseOne, createNew.RootElement.GetProperty(nameof(OosInvestigation.Status)).GetInt32());
            Assert.Equal(excursion.Number, createNew.RootElement.GetProperty(nameof(OosInvestigation.Number)).GetString());
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

        // Close by a user without Oos.Manage refused.
        var noPerm = await Assert.ThrowsAsync<CodedAccessDeniedException>(() =>
            RunCloseAsync(excursionId, activeCategoryId, "Transient drift", "Retrained operator", ActionGuardScenarios.Password, currentVersion, performingAnalyst.Id));
        Assert.Equal(MessageCodes.OosInvestigationAccessDenied, noPerm.Code);
        await AssertUnchangedAsync(excursionId, currentVersion);

        // Other-section Section Head refused.
        var wrongScope = await Assert.ThrowsAsync<CodedAccessDeniedException>(() =>
            RunCloseAsync(excursionId, activeCategoryId, "Transient drift", "Retrained operator", ActionGuardScenarios.Password, currentVersion, otherHead.Id));
        Assert.Equal(MessageCodes.OosSectionAccessDenied, wrongScope.Code);
        await AssertUnchangedAsync(excursionId, currentVersion);

        // Empty cause description or action taken refused.
        var emptyText = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            RunCloseAsync(excursionId, activeCategoryId, " ", "Retrained operator", ActionGuardScenarios.Password, currentVersion, head.Id));
        Assert.Equal(MessageCodes.OosExcursionTextInvalid, emptyText.Code);
        var emptyAction = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            RunCloseAsync(excursionId, activeCategoryId, "Transient drift", "", ActionGuardScenarios.Password, currentVersion, head.Id));
        Assert.Equal(MessageCodes.OosExcursionTextInvalid, emptyAction.Code);
        await AssertUnchangedAsync(excursionId, currentVersion);

        // Signer who performed the test refused.
        var sod = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            RunCloseAsync(excursionId, activeCategoryId, "Transient drift", "Retrained operator", ActionGuardScenarios.Password, currentVersion, performingHead.Id));
        Assert.Equal(MessageCodes.OosSignerPerformedTest, sod.Code);
        await AssertUnchangedAsync(excursionId, currentVersion);

        // Inactive cause category refused.
        var inactiveErr = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            RunCloseAsync(excursionId, inactiveCategoryId, "Transient drift", "Retrained operator", ActionGuardScenarios.Password, currentVersion, head.Id));
        Assert.Equal(MessageCodes.OosConclusionCauseInactive, inactiveErr.Code);
        await AssertUnchangedAsync(excursionId, currentVersion);

        // Stale version refused.
        var staleErr = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            RunCloseAsync(excursionId, activeCategoryId, "Transient drift", "Retrained operator", ActionGuardScenarios.Password, currentVersion - 1, head.Id));
        Assert.Equal(MessageCodes.OosVersionConflict, staleErr.Code);
        await AssertUnchangedAsync(excursionId, currentVersion);

        // Wrong password refused and changes nothing (including no ElectronicSignature row).
        var badPwd = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            RunCloseAsync(excursionId, activeCategoryId, "Transient drift", "Retrained operator", "wrong-password", currentVersion, head.Id));
        Assert.Equal(MessageCodes.OosConclusionPasswordFailed, badPwd.Code);
        await AssertUnchangedAsync(excursionId, currentVersion);

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

        // Closing an already concluded excursion is refused.
        var alreadyClosed = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            RunCloseAsync(excursionId, activeCategoryId, "Drift", "Action", ActionGuardScenarios.Password, currentVersion, head.Id));
        Assert.Equal(MessageCodes.OosExcursionInvalidState, alreadyClosed.Code);
    }

    [PostgresFact]
    public async Task Manual_oos_open_refuses_action_limit_only_section_and_persists_no_excursion()
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var head = await OosHeadAsync(section);
        var seed = await _scenarios.UnderApprovalAsync(section, reviewer.Id);
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

        uint sampleVersion;
        await using (var read = fixture.CreateDbContext())
            sampleVersion = (await read.Samples.AsNoTracking().SingleAsync(s => s.Id == seed.Sample.Id)).Version;

        await using (var act = fixture.CreateDbContext())
        {
            var err = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                TestServiceFactory.OosInvestigation(act).OpenManuallyAsync(seed.Sample.Id, section.Id, sampleVersion, head.Id));
            Assert.Equal(MessageCodes.OosManualOpenUnavailable, err.Code);
        }

        await using var verify = fixture.CreateDbContext();
        Assert.False(await verify.OosInvestigations.AsNoTracking().AnyAsync(i => i.SampleId == seed.Sample.Id));
    }

    [PostgresFact]
    public async Task Oos_only_entry_points_and_excursion_close_refuse_wrong_kind()
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var analyst = await OosAnalystAsync(section);
        var head = await OosHeadAsync(section);
        var excSeed = await _scenarios.UnderReviewAsync(section, analystId: analyst.Id);
        var oosSeed = await _scenarios.UnderReviewAsync(section, analystId: analyst.Id);
        await using (var arrange = fixture.CreateDbContext())
        {
            arrange.SampleLocations.Add(new SampleLocation
            {
                SampleId = excSeed.Sample.Id,
                TestOrderId = excSeed.Orders[0].Id,
                Status = ResultStatus.ActionLimitExceeded,
                ReportedResult = "60 CFU"
            });
            arrange.SampleLocations.Add(new SampleLocation
            {
                SampleId = oosSeed.Sample.Id,
                TestOrderId = oosSeed.Orders[0].Id,
                Status = ResultStatus.OutOfSpecification,
                ReportedResult = "200 CFU"
            });
            await arrange.SaveChangesAsync();
        }
        await _scenarios.CompleteReviewAsync(new(reviewer.Id, excSeed.Sample.Id));
        await _scenarios.CompleteReviewAsync(new(reviewer.Id, oosSeed.Sample.Id));

        OosInvestigation excursion;
        OosInvestigation oos;
        int categoryId;
        await using (var read = fixture.CreateDbContext())
        {
            excursion = await read.OosInvestigations.AsNoTracking().SingleAsync(i => i.SampleId == excSeed.Sample.Id);
            oos = await read.OosInvestigations.AsNoTracking().SingleAsync(i => i.SampleId == oosSeed.Sample.Id);
            categoryId = (await read.OosCauseCategories.AsNoTracking().FirstAsync(c => c.IsActive)).Id;
        }

        await using (var act = fixture.CreateDbContext())
        {
            var svc = TestServiceFactory.OosInvestigation(act);
            var signErr = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                svc.SignPhaseOneAsync(excursion.Id, new SignPhaseOneRequest(OosPhaseOneAssessment.NoLaboratoryErrorFound, null, null, ActionGuardScenarios.Password), excursion.Version, head.Id, null));
            Assert.Equal(MessageCodes.OosInvestigationKindNotOos, signErr.Code);

            var stmtErr = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                svc.AddAnalystStatementAsync(excursion.Id, "Statement", analyst.Id));
            Assert.Equal(MessageCodes.OosInvestigationKindNotOos, stmtErr.Code);

            var phase2Err = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                svc.AnswerChecklistAsync(excursion.Id, 2, "missing", OosChecklistAnswer.Yes, null, analyst.Id));
            Assert.Equal(MessageCodes.OosInvestigationKindNotOos, phase2Err.Code);

            var entryErr = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                svc.AddEntryAsync(excursion.Id, OosEntryKind.Note, "Note", null, analyst.Id));
            Assert.Equal(MessageCodes.OosEntryInvalid, entryErr.Code);

            var concludeErr = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                svc.ConcludeAsync(excursion.Id, new ConcludeRequest(OosConclusion.Confirmed, categoryId, "Desc", "Sum", ActionGuardScenarios.Password), excursion.Version, head.Id, null));
            Assert.Equal(MessageCodes.OosConclusionPhaseInvalid, concludeErr.Code);

            var closeOosErr = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                svc.CloseExcursionAsync(oos.Id, new CloseExcursionRequest(categoryId, "Desc", "Act", ActionGuardScenarios.Password), oos.Version, head.Id, null));
            Assert.Equal(MessageCodes.OosExcursionInvalidState, closeOosErr.Code);
        }

        await AssertUnchangedAsync(excursion.Id, excursion.Version);
    }

    [PostgresFact]
    public async Task Excursion_closure_refused_with_empty_template()
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var head = await OosHeadAsync(section);
        var seed = await _scenarios.UnderReviewAsync(section);
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

        OosInvestigation excursion;
        int categoryId;
        await using (var read = fixture.CreateDbContext())
        {
            excursion = await read.OosInvestigations.AsNoTracking().SingleAsync(i => i.SampleId == seed.Sample.Id);
            categoryId = (await read.OosCauseCategories.AsNoTracking().FirstAsync(c => c.IsActive)).Id;
        }

        var err = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            RunCloseAsync(excursion.Id, categoryId, "Drift", "Action", ActionGuardScenarios.Password, excursion.Version, head.Id));
        Assert.Equal(MessageCodes.OosExcursionTemplateEmpty, err.Code);
        await AssertUnchangedAsync(excursion.Id, excursion.Version);
    }

    [PostgresFact]
    public async Task Second_exceedance_in_same_section_joins_open_excursion()
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var seed = await _scenarios.UnderReviewAsync(section, orders: 2);
        await using (var arrange = fixture.CreateDbContext())
        {
            arrange.SampleLocations.AddRange(
                new SampleLocation { SampleId = seed.Sample.Id, TestOrderId = seed.Orders[0].Id, Status = ResultStatus.ActionLimitExceeded, ReportedResult = "60 CFU" },
                new SampleLocation { SampleId = seed.Sample.Id, TestOrderId = seed.Orders[1].Id, Status = ResultStatus.ActionLimitExceeded, ReportedResult = "75 CFU" });
            await arrange.SaveChangesAsync();
        }

        await using (var act1 = fixture.CreateDbContext())
        {
            await UnitOfWork.RunAsync(act1, async () =>
            {
                var sample = await act1.Samples.SingleAsync(s => s.Id == seed.Sample.Id);
                await TestServiceFactory.OosInvestigation(act1).OpenForReviewedSectionAsync(sample, section.Id, [seed.Orders[0]], reviewer.Id);
                await act1.SaveChangesAsync();
            });
        }
        await using (var act2 = fixture.CreateDbContext())
        {
            await UnitOfWork.RunAsync(act2, async () =>
            {
                var sample = await act2.Samples.SingleAsync(s => s.Id == seed.Sample.Id);
                await TestServiceFactory.OosInvestigation(act2).OpenForReviewedSectionAsync(sample, section.Id, [seed.Orders[1]], reviewer.Id);
                await act2.SaveChangesAsync();
            });
        }

        await using var verify = fixture.CreateDbContext();
        var excursion = await verify.OosInvestigations.AsNoTracking().Include(i => i.Results)
            .SingleAsync(i => i.SampleId == seed.Sample.Id);
        Assert.Equal(OosInvestigationKind.Excursion, excursion.Kind);
        Assert.Equal(2, excursion.Results.Count);
        Assert.Equal(seed.Orders.Select(o => o.Id).Order(), excursion.Results.Select(r => r.TestOrderId).Order());
    }

    [PostgresFact]
    public async Task OpenManualExcursion_validates_inputs_and_joins_open_excursion()
    {
        var section = await _scenarios.SectionAsync();
        var analyst = await OosAnalystAsync(section);
        var seed = await _scenarios.UnderReviewAsync(section, orders: 4, analystId: analyst.Id);
        var approvedSeed = await _scenarios.UnderReviewAsync(section, analystId: analyst.Id);
        await using (var arrange = fixture.CreateDbContext())
        {
            var superseded = await arrange.TestOrders.SingleAsync(o => o.Id == seed.Orders[1].Id);
            superseded.IsSuperseded = true;
            arrange.SampleLocations.Add(new SampleLocation
            {
                SampleId = seed.Sample.Id,
                TestOrderId = seed.Orders[2].Id,
                Status = ResultStatus.OutOfSpecification,
                ReportedResult = "200 CFU"
            });
            var approvedSample = await arrange.Samples.SingleAsync(s => s.Id == approvedSeed.Sample.Id);
            approvedSample.Status = SampleStatus.Approved;
            await arrange.SaveChangesAsync();
        }

        var notFound = await Assert.ThrowsAsync<NotFoundException>(() => RunManualOpenAsync(int.MaxValue, "Reason", analyst.Id));
        Assert.Equal(MessageCodes.TestOrderNotFound, notFound.Code);

        var badReason = await Assert.ThrowsAsync<BusinessRuleException>(() => RunManualOpenAsync(seed.Orders[0].Id, "  ", analyst.Id));
        Assert.Equal(MessageCodes.OosExcursionReasonInvalid, badReason.Code);

        var supersededErr = await Assert.ThrowsAsync<BusinessRuleException>(() => RunManualOpenAsync(seed.Orders[1].Id, "Reason", analyst.Id));
        Assert.Equal(MessageCodes.OosExcursionOrderSuperseded, supersededErr.Code);

        var failingErr = await Assert.ThrowsAsync<BusinessRuleException>(() => RunManualOpenAsync(seed.Orders[2].Id, "Reason", analyst.Id));
        Assert.Equal(MessageCodes.OosExcursionFailingOrderRequiresOos, failingErr.Code);

        var closedErr = await Assert.ThrowsAsync<BusinessRuleException>(() => RunManualOpenAsync(approvedSeed.Orders[0].Id, "Reason", analyst.Id));
        Assert.Equal(MessageCodes.OosExcursionSampleClosed, closedErr.Code);

        var first = await RunManualOpenAsync(seed.Orders[0].Id, "Chart rule 1", analyst.Id);
        var second = await RunManualOpenAsync(seed.Orders[3].Id, "Chart rule 2", analyst.Id);
        Assert.Equal(first.Id, second.Id);

        await using var verify = fixture.CreateDbContext();
        var excursion = await verify.OosInvestigations.AsNoTracking().Include(i => i.Results).SingleAsync(i => i.Id == first.Id);
        Assert.Equal(2, excursion.Results.Count);
    }

    [PostgresFact]
    public async Task Excursion_notifications_use_excursion_wording_and_notify_both_when_both_open()
    {
        var section = await _scenarios.SectionAsync();
        var reviewer = await _scenarios.ReviewerAsync(section);
        var head = await OosHeadAsync(section);
        var seed = await _scenarios.UnderReviewAsync(section, orders: 2);
        await using (var arrange = fixture.CreateDbContext())
        {
            arrange.SampleLocations.AddRange(
                new SampleLocation { SampleId = seed.Sample.Id, TestOrderId = seed.Orders[0].Id, Status = ResultStatus.OutOfSpecification, ReportedResult = "200 CFU" },
                new SampleLocation { SampleId = seed.Sample.Id, TestOrderId = seed.Orders[1].Id, Status = ResultStatus.ActionLimitExceeded, ReportedResult = "60 CFU" });
            await arrange.SaveChangesAsync();
        }

        (int InvestigationId, bool Opened, bool PhaseTwoStarted, int? ExcursionId, bool ExcursionOpened)? reviewResult;
        await using (var act = fixture.CreateDbContext())
        {
            reviewResult = await TestServiceFactory.SampleReview(act).CompleteReviewAsync(
                seed.Sample.Id, reviewer.Id, ActionGuardScenarios.Password, null, null, section.Id);
        }

        Assert.NotNull(reviewResult);
        Assert.True(reviewResult.Value.Opened);
        Assert.True(reviewResult.Value.ExcursionOpened);
        Assert.NotNull(reviewResult.Value.ExcursionId);

        await using var sp = new ServiceCollection()
            .AddScoped<IMicroLimsDbContext>(_ => fixture.CreateDbContext())
            .AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(2042, 4, 1, 9, 30, 0, TimeSpan.Zero)))
            .AddScoped<SegregationOfDutiesGuard>()
            .AddScoped<OosRecipientResolver>()
            .AddScoped<INotificationPublisher, NotificationPublisher>()
            .BuildServiceProvider();
        var notifier = new OosNotifier(sp.GetRequiredService<IServiceScopeFactory>());
        await notifier.OpenedAsync(reviewResult.Value.InvestigationId);
        await notifier.OpenedAsync(reviewResult.Value.ExcursionId.Value);

        await using var verify = fixture.CreateDbContext();
        var notifications = await verify.NotificationLogs.AsNoTracking()
            .Where(n => n.UserId == head.Id && n.SampleId == seed.Sample.Id &&
                (n.Type == "OosOpened" || n.Type == "ExcursionOpened"))
            .ToListAsync();
        var oosNotif = Assert.Single(notifications, n => n.Type == "OosOpened");
        var excNotif = Assert.Single(notifications, n => n.Type == "ExcursionOpened");
        Assert.StartsWith("OOS investigation ", oosNotif.Message);
        Assert.StartsWith("Excursion review ", excNotif.Message);
    }

    private async Task AssertUnchangedAsync(int excursionId, uint expectedVersion)
    {
        await using var verify = fixture.CreateDbContext();
        var unchanged = await verify.OosInvestigations.AsNoTracking().SingleAsync(i => i.Id == excursionId);
        Assert.Equal(OosInvestigationStatus.PhaseOne, unchanged.Status);
        Assert.Equal(expectedVersion, unchanged.Version);
        Assert.Null(unchanged.Conclusion);
        Assert.Null(unchanged.ConclusionSignatureId);
        Assert.False(await verify.ElectronicSignatures.AsNoTracking()
            .AnyAsync(s => s.EntityType == nameof(OosInvestigation) && s.EntityId == excursionId));
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
