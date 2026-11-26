using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Application.Services.Quality;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Entities.Quality;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class ChangeControlGateFixesPostgresTests(PostgresTestFixture fixture)
{
    private static readonly DateTime BaseNow = new(2086, 10, 1, 10, 0, 0, DateTimeKind.Utc);

    private sealed class MutableClock(DateTime utcNow) : TimeProvider, ILabClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => new(UtcNow);
        public DateOnly LabToday => DateOnly.FromDateTime(UtcNow);
        DateTimeOffset ILabClock.UtcNow => new(UtcNow);
        public TimeZoneInfo LabTimeZone => TimeZoneInfo.Utc;
        public DateTime ToLabLocal(DateTime utcDateTime) => utcDateTime;
        public DateOnly ToLabDate(DateTime utcDateTime) => DateOnly.FromDateTime(utcDateTime);
    }

    private MicroLimsDbContext Context(MutableClock clock, int? actor = null)
    {
        var db = fixture.CreateDbContext(clock);
        db.CurrentUserId = actor;
        return db;
    }

    private async Task<(int UserId, int SectionId)> CreateSectionHeadAsync(MicroLimsDbContext db, params string[] extraPerms)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var dept = new DocumentDepartment { Code = $"D-{suffix}", Name = $"Dept {suffix}", IsActive = true };
        db.DocumentDepartments.Add(dept);
        await db.SaveChangesAsync();

        var section = new DocumentSection { DepartmentId = dept.Id, Code = $"S-{suffix}", Name = $"Section {suffix}", IsActive = true };
        db.DocumentSections.Add(section);
        await db.SaveChangesAsync();

        var key = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FullName = $"Gate Fix User {suffix}",
            Username = key,
            IsActive = true,
            Role = new Role { Name = key, Type = RoleType.SectionHead, IsActive = true },
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Gate-test-pw", 4)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var permCodes = extraPerms.Concat([PermissionConstants.MasterDataManage]).Distinct().ToArray();
        var permIds = await db.Permissions.AsNoTracking()
            .Where(p => permCodes.Contains(p.Code))
            .Select(p => p.Id)
            .ToListAsync();
        db.RolePermissions.AddRange(permIds.Select(pid => new RolePermission { RoleId = user.RoleId, PermissionId = pid }));
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, DepartmentId = dept.Id, SectionId = section.Id });
        await db.SaveChangesAsync();

        return (user.Id, section.Id);
    }

    private async Task<(int RecordId, string Number, int LinkId)> SeedApprovedCcAsync(
        MutableClock clock,
        int userId,
        int? sectionId,
        QualityLinkTargetType targetType,
        int? targetId,
        string? newRecordCode = null)
    {
        await using var db = Context(clock);
        var number = "CC-2086-" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        var record = new QualityRecord
        {
            Number = number,
            Type = QualityRecordType.ChangeControl,
            Title = "Fix Gate CC",
            Description = "Fix Gate CC desc",
            SectionId = sectionId,
            RaisedByUserId = userId,
            OwnerUserId = userId,
            RaisedAtUtc = BaseNow
        };
        db.QualityRecords.Add(record);
        await db.SaveChangesAsync();

        db.ChangeControlDetails.Add(new ChangeControlDetail
        {
            RecordId = record.Id,
            Status = ChangeControlStatus.ApprovedForImplementation,
            Reason = "Fix Gate test CC"
        });
        var link = new QualityRecordLink
        {
            RecordId = record.Id,
            TargetType = targetType,
            TargetId = targetId,
            NewRecordCode = newRecordCode,
            IsAffectedByChange = true,
            AddedByUserId = userId,
            AddedAtUtc = BaseNow
        };
        db.QualityRecordLinks.Add(link);
        await db.SaveChangesAsync();
        return (record.Id, number, link.Id);
    }

    [PostgresFact]
    public async Task Noop_gated_write_does_not_mark_link_and_ignores_stale_audit_ids()
    {
        var clock = new MutableClock(BaseNow);
        int userId, sectionId, itemId, specId, frozenTestId;
        string testCode;

        await using (var arrangeDb = Context(clock))
        {
            (userId, sectionId) = await CreateSectionHeadAsync(arrangeDb);
            arrangeDb.QualitySectionSettings.Add(new QualitySectionSetting { SectionId = sectionId, ChangeControlRequired = true });

            var item = new Item { Code = "ITM-" + Guid.NewGuid().ToString("N")[..8], Name = "Noop Item", IsActive = true };
            arrangeDb.Items.Add(item);

            testCode = "TST-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            var testDef = new TestDefinition
            {
                Code = testCode,
                DisplayName = "Noop Test",
                SectionId = sectionId,
                WorkflowType = WorkflowType.Observation,
                EquationType = EquationType.None,
                IsActive = false
            };
            arrangeDb.TestDefinitions.Add(testDef);
            await arrangeDb.SaveChangesAsync();

            itemId = item.Id;
            frozenTestId = testDef.Id;
            arrangeDb.SampleTests.Add(new SampleTest { ItemId = itemId, TestCode = testCode });

            var spec = new Specification
            {
                ItemId = itemId,
                TestCode = testCode,
                ParameterName = "NoopParam",
                LimitType = LimitType.Qualitative,
                ExpectedResultText = "Clear",
                SpecLimit = "Clear"
            };
            arrangeDb.Specifications.Add(spec);
            await arrangeDb.SaveChangesAsync();
            specId = spec.Id;
        }

        var (_, testCc, testLinkId) = await SeedApprovedCcAsync(clock, userId, sectionId, QualityLinkTargetType.TestDefinition, frozenTestId);
        var (_, specCc, specLinkId) = await SeedApprovedCcAsync(clock, userId, sectionId, QualityLinkTargetType.Specification, specId);

        // 1. Freezing an already-frozen test writes no audit row -> link stays unmarked
        await using (var actDb = Context(clock, userId))
        {
            var gate = new ChangeControlGate(actDb, new ChangeControlContext { Number = testCc }, clock);
            var svc = new TestDefinitionMasterDataService(actDb, new UserSectionScopeService(actDb),
                new AuditEventService(actDb, fixture.CreateSequenceHelper(actDb), clock), changeControlGate: gate);
            await svc.FreezeTestDefinitionAsync(userId, frozenTestId, changeReason: "Already frozen");
        }

        await using (var assertDb = Context(clock))
        {
            var tLink = await assertDb.QualityRecordLinks.AsNoTracking().SingleAsync(l => l.Id == testLinkId);
            Assert.Null(tLink.ChangedAtUtc);
            Assert.Null(tLink.ChangeAuditLogId);
        }

        // 2. Identical spec update writes no audit row -> link stays unmarked
        await using (var actDb = Context(clock, userId))
        {
            var gate = new ChangeControlGate(actDb, new ChangeControlContext { Number = specCc }, clock);
            var svc = new SpecificationMasterDataService(actDb, new UserSectionScopeService(actDb), timeProvider: clock, changeControlGate: gate);
            await svc.UpdateSpecificationAsync(userId, specId, new UpdateSpecificationRequest(
                testCode, ParameterName: "NoopParam", DisplayOrder: 0, LimitType: LimitType.Qualitative,
                LowerInclusive: true, UpperInclusive: true, ExpectedResultText: "Clear", SpecLimit: "Clear", Unit: string.Empty));
        }

        await using (var assertDb = Context(clock))
        {
            var sLink = await assertDb.QualityRecordLinks.AsNoTracking().SingleAsync(l => l.Id == specLinkId);
            Assert.Null(sLink.ChangedAtUtc);
            Assert.Null(sLink.ChangeAuditLogId);
        }

        // 3. In the SAME DbContext: real change -> identical no-op -> second real change gets NEW audit id
        var t1 = BaseNow.AddHours(1);
        var t2 = BaseNow.AddHours(2);
        var t3 = BaseNow.AddHours(3);
        int firstAuditId;
        int secondAuditId;

        await using (var actDb = Context(clock, userId))
        {
            var gate = new ChangeControlGate(actDb, new ChangeControlContext { Number = specCc }, clock);
            var svc = new SpecificationMasterDataService(actDb, new UserSectionScopeService(actDb), timeProvider: clock, changeControlGate: gate);

            clock.UtcNow = t1;
            await svc.UpdateSpecificationAsync(userId, specId, new UpdateSpecificationRequest(
                testCode, ParameterName: "Changed1", DisplayOrder: 0, LimitType: LimitType.Qualitative,
                LowerInclusive: true, UpperInclusive: true, ExpectedResultText: "Clear", SpecLimit: "Clear", Unit: string.Empty));

            firstAuditId = (await actDb.QualityRecordLinks.AsNoTracking().SingleAsync(l => l.Id == specLinkId)).ChangeAuditLogId!.Value;

            // No-op in same context at t2 must NOT update ChangedAtUtc or reuse firstAuditId
            clock.UtcNow = t2;
            await svc.UpdateSpecificationAsync(userId, specId, new UpdateSpecificationRequest(
                testCode, ParameterName: "Changed1", DisplayOrder: 0, LimitType: LimitType.Qualitative,
                LowerInclusive: true, UpperInclusive: true, ExpectedResultText: "Clear", SpecLimit: "Clear", Unit: string.Empty));

            var midLink = await actDb.QualityRecordLinks.AsNoTracking().SingleAsync(l => l.Id == specLinkId);
            Assert.Equal(t1, midLink.ChangedAtUtc);
            Assert.Equal(firstAuditId, midLink.ChangeAuditLogId);

            // Second real change in same context at t3 gets NEW audit id
            clock.UtcNow = t3;
            await svc.UpdateSpecificationAsync(userId, specId, new UpdateSpecificationRequest(
                testCode, ParameterName: "Changed2", DisplayOrder: 0, LimitType: LimitType.Qualitative,
                LowerInclusive: true, UpperInclusive: true, ExpectedResultText: "Clear", SpecLimit: "Clear", Unit: string.Empty));

            secondAuditId = (await actDb.QualityRecordLinks.AsNoTracking().SingleAsync(l => l.Id == specLinkId)).ChangeAuditLogId!.Value;
        }

        await using (var assertDb = Context(clock))
        {
            var finalLink = await assertDb.QualityRecordLinks.AsNoTracking().SingleAsync(l => l.Id == specLinkId);
            Assert.Equal(t3, finalLink.ChangedAtUtc);
            Assert.True(secondAuditId > firstAuditId);
            Assert.Equal(secondAuditId, finalLink.ChangeAuditLogId);
        }
    }

    [PostgresFact]
    public async Task Spec_create_matches_NewRecordCode_for_TestDefinition_and_Item_in_separate_requests()
    {
        var clock = new MutableClock(BaseNow);
        int userId, sectionId, existingItemId, newItemId;
        string existingTestCode;
        string newTestCode = "TST-NEW-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        string newItemCode = "ITM-NEW-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        await using (var arrangeDb = Context(clock))
        {
            (userId, sectionId) = await CreateSectionHeadAsync(arrangeDb);
            arrangeDb.QualitySectionSettings.Add(new QualitySectionSetting { SectionId = sectionId, ChangeControlRequired = true });

            var existingItem = new Item { Code = "ITM-EX-" + Guid.NewGuid().ToString("N")[..6], Name = "Existing Item", IsActive = true };
            var newItem = new Item { Code = newItemCode, Name = "New Item", IsActive = true };
            arrangeDb.Items.AddRange(existingItem, newItem);

            existingTestCode = "TST-EX-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            arrangeDb.TestDefinitions.Add(new TestDefinition
            {
                Code = existingTestCode,
                DisplayName = "Existing Test",
                SectionId = sectionId,
                WorkflowType = WorkflowType.Observation,
                EquationType = EquationType.None,
                IsActive = true
            });
            await arrangeDb.SaveChangesAsync();
            existingItemId = existingItem.Id;
            newItemId = newItem.Id;
            arrangeDb.SampleTests.AddRange(
                new SampleTest { ItemId = existingItemId, TestCode = newTestCode },
                new SampleTest { ItemId = newItemId, TestCode = existingTestCode });
            await arrangeDb.SaveChangesAsync();
        }

        // 1. CC lists new test by NewRecordCode (trimmed, case-insensitive)
        var (_, testCc, testLinkId) = await SeedApprovedCcAsync(
            clock, userId, sectionId, QualityLinkTargetType.TestDefinition, null, $"  {newTestCode.ToLowerInvariant()}  ");

        await using (var actDb1 = Context(clock, userId))
        {
            var gate = new ChangeControlGate(actDb1, new ChangeControlContext { Number = testCc }, clock);
            var tdSvc = new TestDefinitionMasterDataService(actDb1, new UserSectionScopeService(actDb1),
                new AuditEventService(actDb1, fixture.CreateSequenceHelper(actDb1), clock), changeControlGate: gate);
            await tdSvc.CreateTestDefinitionAsync(userId, new CreateTestDefinitionRequest(newTestCode, "New Test", sectionId));
        }

        // Create spec in ANOTHER request -> allowed & TestDefinition link marked
        var tSpec1 = BaseNow.AddMinutes(10);
        clock.UtcNow = tSpec1;
        await using (var actDb2 = Context(clock, userId))
        {
            var gate = new ChangeControlGate(actDb2, new ChangeControlContext { Number = testCc }, clock);
            var specSvc = new SpecificationMasterDataService(actDb2, new UserSectionScopeService(actDb2), timeProvider: clock, changeControlGate: gate);
            await specSvc.CreateSpecificationAsync(userId, new CreateSpecificationRequest(
                existingItemId, newTestCode, ParameterName: "ByTestCode", LimitType: LimitType.Qualitative, ExpectedResultText: "Clear"));
        }

        // 2. CC lists new item by NewRecordCode (trimmed, case-insensitive)
        var (_, itemCc, itemLinkId) = await SeedApprovedCcAsync(
            clock, userId, sectionId, QualityLinkTargetType.Item, null, $"  {newItemCode.ToLowerInvariant()}  ");
        var tSpec2 = BaseNow.AddMinutes(20);
        clock.UtcNow = tSpec2;
        await using (var actDb3 = Context(clock, userId))
        {
            var gate = new ChangeControlGate(actDb3, new ChangeControlContext { Number = itemCc }, clock);
            var specSvc = new SpecificationMasterDataService(actDb3, new UserSectionScopeService(actDb3), timeProvider: clock, changeControlGate: gate);
            await specSvc.CreateSpecificationAsync(userId, new CreateSpecificationRequest(
                newItemId, existingTestCode, ParameterName: "ByItemCode", LimitType: LimitType.Qualitative, ExpectedResultText: "Clear"));
        }

        await using var assertDb = Context(clock);
        var testLink = await assertDb.QualityRecordLinks.AsNoTracking().SingleAsync(l => l.Id == testLinkId);
        Assert.Equal(tSpec1, testLink.ChangedAtUtc);
        Assert.NotNull(testLink.ChangeAuditLogId);

        var itemLink = await assertDb.QualityRecordLinks.AsNoTracking().SingleAsync(l => l.Id == itemLinkId);
        Assert.Equal(tSpec2, itemLink.ChangedAtUtc);
        Assert.NotNull(itemLink.ChangeAuditLogId);
    }

    [PostgresFact]
    public async Task ChangeControl_Propose_and_AddAffected_refuse_Specification_with_NewRecordCode_only()
    {
        var clock = new MutableClock(BaseNow);
        int userId, sectionId;
        await using (var arrangeDb = Context(clock))
        {
            (userId, sectionId) = await CreateSectionHeadAsync(arrangeDb, PermissionConstants.QualityRaise, PermissionConstants.QualityView);
        }

        await using var actDb = Context(clock, userId);
        var permissions = new PermissionService(actDb);
        var scope = new UserSectionScopeService(actDb);
        var sigs = new ElectronicSignatureService(actDb, clock);
        var core = new QualityRecordCore(actDb, permissions, scope, new ReferenceNumberGenerator(actDb, clock), sigs, clock);
        var cfg = new QualityConfigService(actDb, core, permissions, scope, clock, sigs);
        var dueDates = new QualityDueDateService(actDb);
        var ccSvc = new ChangeControlService(actDb, core, permissions, scope, clock, dueDates, cfg);

        var proposeEx = await Assert.ThrowsAsync<BusinessRuleException>(() => ccSvc.ProposeAsync(
            new ProposeChangeControlRequest("Spec new code", "Desc", "Reason", sectionId,
                [new ChangeControlAffectedInput(QualityLinkTargetType.Specification, null, "SPEC-NEW-1")]),
            userId));
        Assert.Equal(MessageCodes.ChangeControlSpecificationRequiresId, proposeEx.Code);

        var proposed = await ccSvc.ProposeAsync(
            new ProposeChangeControlRequest("Valid CC", "Desc", "Reason", sectionId, []),
            userId);

        var addEx = await Assert.ThrowsAsync<BusinessRuleException>(() => ccSvc.AddAffectedAsync(
            proposed.Id,
            new ChangeControlAffectedInput(QualityLinkTargetType.Specification, null, "SPEC-NEW-1"),
            proposed.Version,
            userId));
        Assert.Equal(MessageCodes.ChangeControlSpecificationRequiresId, addEx.Code);
    }

    [PostgresFact]
    public async Task Item_delete_refuses_when_any_spec_section_has_gate_on_and_allows_when_off()
    {
        var clock = new MutableClock(BaseNow);
        int userId, sectionOnId, sectionOffId, gatedItemId, gatedSpecId, ungatedItemId, ungatedSpecId;

        await using (var arrangeDb = Context(clock))
        {
            (userId, sectionOnId) = await CreateSectionHeadAsync(arrangeDb);
            (_, sectionOffId) = await CreateSectionHeadAsync(arrangeDb);

            arrangeDb.QualitySectionSettings.Add(new QualitySectionSetting { SectionId = sectionOnId, ChangeControlRequired = true });

            var tdOn = new TestDefinition { Code = "TST-ON-" + Guid.NewGuid().ToString("N")[..6], DisplayName = "On", SectionId = sectionOnId, WorkflowType = WorkflowType.Observation, EquationType = EquationType.None, IsActive = true };
            var tdOff = new TestDefinition { Code = "TST-OFF-" + Guid.NewGuid().ToString("N")[..6], DisplayName = "Off", SectionId = sectionOffId, WorkflowType = WorkflowType.Observation, EquationType = EquationType.None, IsActive = true };
            var itemOn = new Item { Code = "ITM-ON-" + Guid.NewGuid().ToString("N")[..6], Name = "Item On", IsActive = true };
            var itemOff = new Item { Code = "ITM-OFF-" + Guid.NewGuid().ToString("N")[..6], Name = "Item Off", IsActive = true };
            arrangeDb.TestDefinitions.AddRange(tdOn, tdOff);
            arrangeDb.Items.AddRange(itemOn, itemOff);
            await arrangeDb.SaveChangesAsync();

            var specOn = new Specification { ItemId = itemOn.Id, TestCode = tdOn.Code, ParameterName = "P1", LimitType = LimitType.Qualitative, ExpectedResultText = "Ok", SpecLimit = "Ok" };
            var specOff = new Specification { ItemId = itemOff.Id, TestCode = tdOff.Code, ParameterName = "P2", LimitType = LimitType.Qualitative, ExpectedResultText = "Ok", SpecLimit = "Ok" };
            arrangeDb.Specifications.AddRange(specOn, specOff);
            await arrangeDb.SaveChangesAsync();

            gatedItemId = itemOn.Id;
            gatedSpecId = specOn.Id;
            ungatedItemId = itemOff.Id;
            ungatedSpecId = specOff.Id;
        }

        long auditWatermark;
        await using (var wmDb = Context(clock))
            auditWatermark = await wmDb.AuditLogs.AsNoTracking().Select(a => (long?)a.Id).MaxAsync() ?? 0L;

        // Gate ON -> refused, item and specs unchanged, no new audit rows
        await using (var actDb = Context(clock, userId))
        {
            var itemSvc = new ItemService(actDb, new UserSectionScopeService(actDb));
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                itemSvc.DeleteAsync(gatedItemId, "Try delete gated item", userId));
            Assert.Equal(MessageCodes.ItemDeleteBlockedByChangeControlGate, ex.Code);
        }

        await using (var assertDb = Context(clock))
        {
            Assert.True(await assertDb.Items.AsNoTracking().AnyAsync(i => i.Id == gatedItemId));
            Assert.True(await assertDb.Specifications.AsNoTracking().AnyAsync(s => s.Id == gatedSpecId));
            Assert.False(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a => a.Id > auditWatermark));
        }

        // Gate OFF -> deleted as before
        await using (var actDb = Context(clock, userId))
        {
            var itemSvc = new ItemService(actDb, new UserSectionScopeService(actDb));
            await itemSvc.DeleteAsync(ungatedItemId, "Delete ungated item", userId);
        }

        await using (var assertDb2 = Context(clock))
        {
            Assert.False(await assertDb2.Items.AsNoTracking().AnyAsync(i => i.Id == ungatedItemId));
            Assert.False(await assertDb2.Specifications.AsNoTracking().AnyAsync(s => s.Id == ungatedSpecId));
        }
    }

    [PostgresFact]
    public async Task Stale_tracked_change_control_status_is_refused_after_concurrent_close_or_cancel()
    {
        var clock = new MutableClock(BaseNow);
        int userId, sectionId, specId;
        string testCode;

        await using (var arrangeDb = Context(clock))
        {
            (userId, sectionId) = await CreateSectionHeadAsync(arrangeDb);
            arrangeDb.QualitySectionSettings.Add(new QualitySectionSetting { SectionId = sectionId, ChangeControlRequired = true });
            var item = new Item { Code = "ITM-" + Guid.NewGuid().ToString("N")[..8], Name = "Stale Item", IsActive = true };
            arrangeDb.Items.Add(item);
            testCode = "TST-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            arrangeDb.TestDefinitions.Add(new TestDefinition
            {
                Code = testCode, DisplayName = "Stale Test", SectionId = sectionId,
                WorkflowType = WorkflowType.Observation, EquationType = EquationType.None, IsActive = true
            });
            await arrangeDb.SaveChangesAsync();
            var spec = new Specification { ItemId = item.Id, TestCode = testCode, ParameterName = "P", LimitType = LimitType.Qualitative, ExpectedResultText = "Ok", SpecLimit = "Ok" };
            arrangeDb.Specifications.Add(spec);
            await arrangeDb.SaveChangesAsync();
            specId = spec.Id;
        }

        var (ccId, ccNum, _) = await SeedApprovedCcAsync(clock, userId, sectionId, QualityLinkTargetType.Specification, specId);

        await using var actDb = Context(clock, userId);
        // Load change control tracked into actDb while it is still ApprovedForImplementation
        var trackedCc = await actDb.QualityRecords.Include(r => r.ChangeControlDetail).SingleAsync(r => r.Id == ccId);
        Assert.Equal(ChangeControlStatus.ApprovedForImplementation, trackedCc.ChangeControlDetail!.Status);

        // Close it from another context
        await using (var otherDb = Context(clock, userId))
        {
            var detail = await otherDb.ChangeControlDetails.SingleAsync(d => d.RecordId == ccId);
            detail.Status = ChangeControlStatus.Closed;
            await otherDb.SaveChangesAsync();
        }

        var gate = new ChangeControlGate(actDb, new ChangeControlContext { Number = ccNum }, clock);
        var specSvc = new SpecificationMasterDataService(actDb, new UserSectionScopeService(actDb), timeProvider: clock, changeControlGate: gate);
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            specSvc.UpdateSpecificationAsync(userId, specId, new UpdateSpecificationRequest(
                testCode, ParameterName: "ShouldFail", LimitType: LimitType.Qualitative, ExpectedResultText: "Ok")));
        Assert.Equal(MessageCodes.ChangeControlGateNotApproved, ex.Code);
    }

    [PostgresFact]
    public async Task Gate_requires_active_transaction_with_dedicated_code()
    {
        var clock = new MutableClock(BaseNow);
        int userId, sectionId, specId;
        await using (var arrangeDb = Context(clock))
        {
            (userId, sectionId) = await CreateSectionHeadAsync(arrangeDb);
            arrangeDb.QualitySectionSettings.Add(new QualitySectionSetting { SectionId = sectionId, ChangeControlRequired = true });
            var item = new Item { Code = "ITM-" + Guid.NewGuid().ToString("N")[..8], Name = "Tx Item", IsActive = true };
            arrangeDb.Items.Add(item);
            var testCode = "TST-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            arrangeDb.TestDefinitions.Add(new TestDefinition
            {
                Code = testCode, DisplayName = "Tx Test", SectionId = sectionId,
                WorkflowType = WorkflowType.Observation, EquationType = EquationType.None, IsActive = true
            });
            await arrangeDb.SaveChangesAsync();
            var spec = new Specification { ItemId = item.Id, TestCode = testCode, ParameterName = "P", LimitType = LimitType.Qualitative, ExpectedResultText = "Ok", SpecLimit = "Ok" };
            arrangeDb.Specifications.Add(spec);
            await arrangeDb.SaveChangesAsync();
            specId = spec.Id;
        }

        var (_, ccNum, _) = await SeedApprovedCcAsync(clock, userId, sectionId, QualityLinkTargetType.Specification, specId);
        await using var actDb = Context(clock, userId);
        var gate = new ChangeControlGate(actDb, new ChangeControlContext { Number = ccNum }, clock);
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            gate.EnsureAllowedAsync(sectionId, QualityLinkTargetType.Specification, specId, null));
        Assert.Equal(MessageCodes.ChangeControlGateTransactionRequired, ex.Code);
    }
}
