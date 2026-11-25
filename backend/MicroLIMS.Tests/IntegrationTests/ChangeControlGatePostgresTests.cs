using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Middleware;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Application.Services.MasterDataWorkbook;
using MicroLIMS.Application.Services.MasterDataWorkbook.Sheets;
using MicroLIMS.Application.Services.Quality;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Entities.Quality;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Shared.Responses;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class ChangeControlGatePostgresTests(PostgresTestFixture fixture)
{
    private static readonly DateTime BaseNow = new(2086, 9, 1, 10, 0, 0, DateTimeKind.Utc);

    private sealed class MutableClock(DateTime utcNow) : TimeProvider
    {
        public DateTime UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => new(UtcNow);
    }

    public enum GateOffMode
    {
        NoSettingRow,
        SettingFalse,
        SettingFalseWithHeaderSent
    }

    public enum GatedMethod
    {
        SpecCreate,
        SpecUpdate,
        SpecDelete,
        SpecReinstate,
        TestDefCreate,
        TestDefUpdate,
        TestDefFreeze,
        TestDefUnfreeze,
        StepUpdateWorkflowType,
        StepCreate,
        StepUpdate,
        StepMove,
        StepDelete,
        ReplicateCreate,
        ReplicateUpdate,
        ReplicateDelete
    }

    public static IEnumerable<object[]> AllGatedMethods()
    {
        foreach (var method in Enum.GetValues<GatedMethod>())
            yield return [method];
    }

    private const string QaPassword = "Gate-qa-password";

    private MicroLimsDbContext Context(MutableClock clock, int? actor = null)
    {
        var db = fixture.CreateDbContext(clock);
        db.CurrentUserId = actor;
        return db;
    }

    private async Task<(int UserId, int SectionId)> CreateSectionHeadAsync(MicroLimsDbContext db)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var dept = new DocumentDepartment
        {
            Code = $"D-{suffix}",
            Name = $"Dept {suffix}",
            IsActive = true
        };
        db.DocumentDepartments.Add(dept);
        await db.SaveChangesAsync();

        var section = new DocumentSection
        {
            DepartmentId = dept.Id,
            Code = $"S-{suffix}",
            Name = $"Section {suffix}",
            IsActive = true
        };
        db.DocumentSections.Add(section);
        await db.SaveChangesAsync();

        var key = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FullName = $"Gate Test User {suffix}",
            Username = key,
            IsActive = true,
            Role = new Role { Name = key, Type = RoleType.SectionHead, IsActive = true },
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Gate-test-pw", 4)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var permCodes = new[] { PermissionConstants.MasterDataManage };
        var permIds = await db.Permissions.AsNoTracking()
            .Where(p => permCodes.Contains(p.Code))
            .Select(p => p.Id)
            .ToListAsync();
        db.RolePermissions.AddRange(permIds.Select(pid => new RolePermission { RoleId = user.RoleId, PermissionId = pid }));
        db.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = user.Id,
            DepartmentId = dept.Id,
            SectionId = section.Id
        });
        await db.SaveChangesAsync();

        return (user.Id, section.Id);
    }

    private sealed record CharacterisationSnapshot(
        string EntitySnapshot,
        List<AuditSnapshot> Audits);

    private sealed record AuditSnapshot(
        string Action,
        string? EntityName,
        string? Reason,
        string? OldValues,
        string? NewValues);

    private static string? NormalizeAuditJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return json;
        var node = JsonNode.Parse(json);
        if (node is JsonObject obj)
        {
            foreach (var key in new[] { "Id", "Version", "RetiredAt", "RetiredByUserId", "CreatedAt", "UpdatedAt", "TestDefinitionId", "ItemId", "SpecificationId", "SectionId" })
            {
                if (obj.ContainsKey(key))
                    obj[key] = "<normalized>";
            }
            // Normalize random codes/names in JSON if present
            foreach (var key in new[] { "Code", "TestCode" })
            {
                if (obj[key] is JsonValue v && v.TryGetValue<string>(out var s) && s.StartsWith("TST-", StringComparison.Ordinal))
                    obj[key] = "TST-<normalized>";
            }
        }
        return node?.ToJsonString();
    }

    private async Task<CharacterisationSnapshot> RunGateOffScenarioAsync(GatedMethod method, GateOffMode mode)
    {
        var clock = new MutableClock(BaseNow);
        int userId;
        int sectionId;
        int itemId;
        int testDefId;
        string testCode;
        int specId = 0;
        int step1Id = 0;
        int step2Id = 0;
        int replicateId = 0;
        string newTestCode = "TST-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        int? ccLinkId = null;

        await using (var arrangeDb = Context(clock))
        {
            (userId, sectionId) = await CreateSectionHeadAsync(arrangeDb);

            if (mode is GateOffMode.SettingFalse or GateOffMode.SettingFalseWithHeaderSent)
            {
                arrangeDb.QualitySectionSettings.Add(new QualitySectionSetting
                {
                    SectionId = sectionId,
                    ChangeControlRequired = false
                });
                await arrangeDb.SaveChangesAsync();
            }

            if (mode == GateOffMode.SettingFalseWithHeaderSent)
            {
                var ccNum = "CC-2086-" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
                var ccRecord = new QualityRecord
                {
                    Number = ccNum,
                    Type = QualityRecordType.ChangeControl,
                    Title = "Gate Off Header Ignored",
                    Description = "Test",
                    SectionId = sectionId,
                    RaisedByUserId = userId,
                    OwnerUserId = userId,
                    RaisedAtUtc = BaseNow
                };
                arrangeDb.QualityRecords.Add(ccRecord);
                await arrangeDb.SaveChangesAsync();
                arrangeDb.ChangeControlDetails.Add(new ChangeControlDetail
                {
                    RecordId = ccRecord.Id,
                    Status = ChangeControlStatus.ApprovedForImplementation,
                    Reason = "Test"
                });
                var link = new QualityRecordLink
                {
                    RecordId = ccRecord.Id,
                    TargetType = QualityLinkTargetType.Item,
                    TargetId = 1,
                    IsAffectedByChange = true,
                    AddedByUserId = userId,
                    AddedAtUtc = BaseNow
                };
                arrangeDb.QualityRecordLinks.Add(link);
                await arrangeDb.SaveChangesAsync();
                ccLinkId = link.Id;
            }

            var item = new Item
            {
                Code = "ITM-" + Guid.NewGuid().ToString("N")[..8],
                Name = "Gate Char Item",
                IsActive = true
            };
            arrangeDb.Items.Add(item);

            testCode = "TST-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            var testDef = new TestDefinition
            {
                Code = testCode,
                DisplayName = "Gate Char Test",
                SectionId = sectionId,
                WorkflowType = WorkflowType.Observation,
                EquationType = EquationType.None,
                IsActive = method != GatedMethod.TestDefUnfreeze
            };
            arrangeDb.TestDefinitions.Add(testDef);
            await arrangeDb.SaveChangesAsync();

            itemId = item.Id;
            testDefId = testDef.Id;

            arrangeDb.SampleTests.Add(new SampleTest
            {
                ItemId = itemId,
                TestCode = testCode
            });
            await arrangeDb.SaveChangesAsync();

            if (method is GatedMethod.SpecUpdate or GatedMethod.SpecDelete or GatedMethod.SpecReinstate)
            {
                var spec = new Specification
                {
                    ItemId = itemId,
                    TestCode = testCode,
                    ParameterName = "CharParam",
                    LimitType = LimitType.Qualitative,
                    ExpectedResultText = "Clear",
                    SpecLimit = "Clear",
                    IsRetired = method == GatedMethod.SpecReinstate,
                    RetiredAt = method == GatedMethod.SpecReinstate ? BaseNow.AddDays(-1) : null,
                    RetiredByUserId = method == GatedMethod.SpecReinstate ? userId : null
                };
                arrangeDb.Specifications.Add(spec);
                await arrangeDb.SaveChangesAsync();
                specId = spec.Id;
            }

            if (method is GatedMethod.StepUpdate or GatedMethod.StepMove or GatedMethod.StepDelete)
            {
                var s1 = new TestWorkflowStep
                {
                    TestDefinitionId = testDefId,
                    StepOrder = 1,
                    StepName = "Step 1",
                    StepType = StepType.BrothEnrichment,
                    IncubationMinHours = 18,
                    IncubationMaxHours = 24,
                    TemperatureMin = 30,
                    TemperatureMax = 35
                };
                var s2 = new TestWorkflowStep
                {
                    TestDefinitionId = testDefId,
                    StepOrder = 2,
                    StepName = "Step 2",
                    StepType = StepType.SelectiveBroth,
                    IsFinalStep = true,
                    IncubationMinHours = 18,
                    IncubationMaxHours = 24,
                    TemperatureMin = 30,
                    TemperatureMax = 35
                };
                arrangeDb.TestWorkflowSteps.AddRange(s1, s2);
                await arrangeDb.SaveChangesAsync();
                step1Id = s1.Id;
                step2Id = s2.Id;
            }

            if (method is GatedMethod.ReplicateUpdate or GatedMethod.ReplicateDelete)
            {
                var rep = new TestDefinitionStageReplicate
                {
                    TestDefinitionId = testDefId,
                    Role = ProductionStageRole.Bulk,
                    SampleReplicates = 2
                };
                arrangeDb.TestDefinitionStageReplicates.Add(rep);
                await arrangeDb.SaveChangesAsync();
                replicateId = rep.Id;
            }
        }

        long auditWatermark;
        await using (var wmDb = Context(clock))
        {
            auditWatermark = await wmDb.AuditLogs.AsNoTracking().Select(a => (long?)a.Id).MaxAsync() ?? 0L;
        }

        int? createdId = null;
        await using (var actDb = Context(clock, userId))
        {
            var ccCtx = new ChangeControlContext();
            if (mode == GateOffMode.SettingFalseWithHeaderSent)
            {
                var ccNumber = await actDb.QualityRecords.AsNoTracking()
                    .Where(r => r.Links.Any(l => l.Id == ccLinkId!.Value))
                    .Select(r => r.Number)
                    .SingleAsync();
                ccCtx.Number = ccNumber;
            }
            var gate = new ChangeControlGate(actDb, ccCtx, clock);
            var scope = new UserSectionScopeService(actDb);
            var audit = new AuditEventService(actDb, fixture.CreateSequenceHelper(actDb), clock);
            var specService = new SpecificationMasterDataService(actDb, scope, timeProvider: clock, changeControlGate: gate);
            var testDefService = new TestDefinitionMasterDataService(actDb, scope, audit, changeControlGate: gate);
            var stepService = new TestWorkflowStepMasterDataService(actDb, scope, gate);
            var repService = new TestStageReplicateMasterDataService(actDb, scope, gate);

            switch (method)
            {
                case GatedMethod.SpecCreate:
                    var createdSpec = await specService.CreateSpecificationAsync(userId, new CreateSpecificationRequest(
                        ItemId: itemId, TestCode: testCode, ParameterName: "NewParam",
                        LimitType: LimitType.Qualitative, ExpectedResultText: "Pass"));
                    createdId = createdSpec.Id;
                    break;

                case GatedMethod.SpecUpdate:
                    await specService.UpdateSpecificationAsync(userId, specId, new UpdateSpecificationRequest(
                        TestCode: testCode, ParameterName: "UpdatedParam",
                        LimitType: LimitType.Qualitative, ExpectedResultText: "UpdatedPass"));
                    break;

                case GatedMethod.SpecDelete:
                    await specService.DeleteSpecificationAsync(userId, specId, "Deleting unused test specification");
                    break;

                case GatedMethod.SpecReinstate:
                    await specService.ReinstateSpecificationAsync(userId, specId, "Reinstating retired test specification");
                    break;

                case GatedMethod.TestDefCreate:
                    var createdTd = await testDefService.CreateTestDefinitionAsync(userId, new CreateTestDefinitionRequest(
                        Code: newTestCode, DisplayName: "New Gate Test", SectionId: sectionId));
                    createdId = createdTd.Id;
                    break;

                case GatedMethod.TestDefUpdate:
                    await testDefService.UpdateTestDefinitionAsync(userId, testDefId, new UpdateTestDefinitionRequest(
                        Code: testCode, DisplayName: "Updated Gate Test"));
                    break;

                case GatedMethod.TestDefFreeze:
                    await testDefService.FreezeTestDefinitionAsync(userId, testDefId, changeReason: "Freezing test definition for char");
                    break;

                case GatedMethod.TestDefUnfreeze:
                    await testDefService.UnfreezeTestDefinitionAsync(userId, testDefId, changeReason: "Unfreezing test definition for char");
                    break;

                case GatedMethod.StepUpdateWorkflowType:
                    await stepService.UpdateWorkflowTypeAsync(userId, testDefId, new UpdateWorkflowTypeRequest(WorkflowType.CountTest));
                    break;

                case GatedMethod.StepCreate:
                    await stepService.CreateTestWorkflowStepAsync(userId, testDefId, new CreateTestWorkflowStepRequest(
                        "New Step", 12, 24, 30, 35, true, StepType.BiochemicalTest, null, [], false, null, null, PhenotypicTestType.Catalase));
                    break;

                case GatedMethod.StepUpdate:
                    await stepService.UpdateTestWorkflowStepAsync(userId, step1Id, new UpdateTestWorkflowStepRequest(
                        "Renamed Step 1", 10, 20, 30, 35, false, StepType.BiochemicalTest, null, [], false, null, null, PhenotypicTestType.Catalase));
                    break;

                case GatedMethod.StepMove:
                    await stepService.MoveTestWorkflowStepAsync(userId, step1Id, new MoveTestWorkflowStepRequest("down"));
                    break;

                case GatedMethod.StepDelete:
                    await stepService.DeleteTestWorkflowStepAsync(userId, step2Id);
                    break;

                case GatedMethod.ReplicateCreate:
                    await repService.CreateTestDefinitionStageReplicateAsync(userId, testDefId,
                        new CreateTestDefinitionStageReplicateRequest(ProductionStageRole.Finished, 3));
                    break;

                case GatedMethod.ReplicateUpdate:
                    await repService.UpdateTestDefinitionStageReplicateAsync(userId, testDefId, replicateId,
                        new UpdateTestDefinitionStageReplicateRequest(5));
                    break;

                case GatedMethod.ReplicateDelete:
                    await repService.DeleteTestDefinitionStageReplicateAsync(userId, testDefId, replicateId);
                    break;
            }
        }

        await using var assertDb = Context(clock);
        var audits = await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.Id > auditWatermark)
            .OrderBy(a => a.Id)
            .Select(a => new AuditSnapshot(
                a.Action,
                a.EntityName,
                a.Reason,
                NormalizeAuditJson(a.PreviousValue),
                NormalizeAuditJson(a.NewValue)))
            .ToListAsync();

        Assert.NotEmpty(audits);
        if (ccLinkId is int linkId)
        {
            var link = await assertDb.QualityRecordLinks.AsNoTracking().SingleAsync(l => l.Id == linkId);
            Assert.Null(link.ChangedAtUtc);
            Assert.Null(link.ChangeAuditLogId);
        }

        string entitySnapshot = method switch
        {
            GatedMethod.SpecCreate => await assertDb.Specifications.AsNoTracking()
                .Where(s => s.Id == createdId!.Value)
                .Select(s => $"{s.ParameterName}|{s.SpecLimit}|{s.IsRetired}")
                .SingleAsync(),
            GatedMethod.SpecUpdate => await assertDb.Specifications.AsNoTracking()
                .Where(s => s.Id == specId)
                .Select(s => $"{s.ParameterName}|{s.SpecLimit}|{s.IsRetired}")
                .SingleAsync(),
            GatedMethod.SpecDelete => (await assertDb.Specifications.AsNoTracking()
                .AnyAsync(s => s.Id == specId)).ToString(),
            GatedMethod.SpecReinstate => await assertDb.Specifications.AsNoTracking()
                .Where(s => s.Id == specId)
                .Select(s => $"{s.ParameterName}|{s.IsRetired}|{s.RetiredByUserId == null}")
                .SingleAsync(),
            GatedMethod.TestDefCreate => await assertDb.TestDefinitions.AsNoTracking()
                .Where(t => t.Id == createdId!.Value)
                .Select(t => $"{t.DisplayName}|{t.IsActive}")
                .SingleAsync(),
            GatedMethod.TestDefUpdate or GatedMethod.TestDefFreeze or GatedMethod.TestDefUnfreeze or GatedMethod.StepUpdateWorkflowType =>
                await assertDb.TestDefinitions.AsNoTracking()
                    .Where(t => t.Id == testDefId)
                    .Select(t => $"{t.DisplayName}|{t.IsActive}|{t.WorkflowType}")
                    .SingleAsync(),
            GatedMethod.StepCreate => await assertDb.TestWorkflowSteps.AsNoTracking()
                .Where(s => s.TestDefinitionId == testDefId)
                .OrderBy(s => s.StepOrder)
                .Select(s => $"{s.StepOrder}:{s.StepName}")
                .SingleAsync(),
            GatedMethod.StepUpdate or GatedMethod.StepMove or GatedMethod.StepDelete => string.Join(",",
                await assertDb.TestWorkflowSteps.AsNoTracking()
                    .Where(s => s.TestDefinitionId == testDefId)
                    .OrderBy(s => s.StepOrder)
                    .Select(s => $"{s.StepOrder}:{s.StepName}")
                    .ToListAsync()),
            GatedMethod.ReplicateCreate or GatedMethod.ReplicateUpdate or GatedMethod.ReplicateDelete => string.Join(",",
                await assertDb.TestDefinitionStageReplicates.AsNoTracking()
                    .Where(r => r.TestDefinitionId == testDefId)
                    .OrderBy(r => r.Role)
                    .Select(r => $"{r.Role}:{r.SampleReplicates}")
                    .ToListAsync()),
            _ => throw new ArgumentOutOfRangeException(nameof(method))
        };

        return new CharacterisationSnapshot(entitySnapshot, audits);
    }

    [PostgresFact]
    public async Task Gate_off_characterisation_is_identical_with_no_setting_or_setting_false()
    {
        foreach (var method in Enum.GetValues<GatedMethod>())
        {
            var noRow = await RunGateOffScenarioAsync(method, GateOffMode.NoSettingRow);
            var settingFalse = await RunGateOffScenarioAsync(method, GateOffMode.SettingFalse);
            var withHeader = await RunGateOffScenarioAsync(method, GateOffMode.SettingFalseWithHeaderSent);

            foreach (var variant in new[] { settingFalse, withHeader })
            {
                Assert.Equal(noRow.EntitySnapshot, variant.EntitySnapshot);
                Assert.Equal(noRow.Audits.Count, variant.Audits.Count);
                for (var i = 0; i < noRow.Audits.Count; i++)
                {
                    Assert.Equal(noRow.Audits[i].Action, variant.Audits[i].Action);
                    Assert.Equal(noRow.Audits[i].EntityName, variant.Audits[i].EntityName);
                    Assert.Equal(noRow.Audits[i].Reason, variant.Audits[i].Reason);
                    Assert.Equal(noRow.Audits[i].OldValues, variant.Audits[i].OldValues);
                    Assert.Equal(noRow.Audits[i].NewValues, variant.Audits[i].NewValues);
                }
            }
        }
    }

    private sealed record GateWorld(
        int UserId,
        int QaUserId,
        int SectionId,
        int OtherSectionId,
        int ItemId,
        int TestDefId,
        string TestCode,
        int SpecId,
        int Step1Id,
        int Step2Id,
        int ReplicateId,
        string NewTestCode);

    private async Task<GateWorld> ArrangeGateWorldAsync(MutableClock clock, bool gateOn = true, bool retiredSpec = false, bool frozenTest = false)
    {
        await using var db = Context(clock);
        var (userId, sectionId) = await CreateSectionHeadAsync(db);
        var (_, otherSectionId) = await CreateSectionHeadAsync(db);

        var qaKey = Guid.NewGuid().ToString("N");
        var qaUser = new User
        {
            FullName = "Gate QA User",
            Username = qaKey,
            IsActive = true,
            Role = new Role { Name = qaKey, Type = RoleType.Qa, IsActive = true },
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(QaPassword, 4)
        };
        db.Users.Add(qaUser);
        await db.SaveChangesAsync();
        var qaPerms = await db.Permissions.AsNoTracking()
            .Where(p => p.Code == PermissionConstants.QualityApprove || p.Code == PermissionConstants.QualityView || p.Code == PermissionConstants.SectionsViewAll)
            .Select(p => p.Id)
            .ToListAsync();
        db.RolePermissions.AddRange(qaPerms.Select(pid => new RolePermission { RoleId = qaUser.RoleId, PermissionId = pid }));
        var deptId = await db.DocumentSections.AsNoTracking().Where(s => s.Id == sectionId).Select(s => s.DepartmentId).SingleAsync();
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = qaUser.Id, DepartmentId = deptId, SectionId = sectionId });

        if (gateOn)
        {
            db.QualitySectionSettings.Add(new QualitySectionSetting
            {
                SectionId = sectionId,
                ChangeControlRequired = true
            });
        }

        var item = new Item { Code = "ITM-" + Guid.NewGuid().ToString("N")[..8], Name = "Gate Item", IsActive = true };
        db.Items.Add(item);

        var testCode = "TST-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var testDef = new TestDefinition
        {
            Code = testCode,
            DisplayName = "Gate Test",
            SectionId = sectionId,
            WorkflowType = WorkflowType.Observation,
            EquationType = EquationType.None,
            IsActive = !frozenTest
        };
        db.TestDefinitions.Add(testDef);
        await db.SaveChangesAsync();

        db.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = testCode });

        var spec = new Specification
        {
            ItemId = item.Id,
            TestCode = testCode,
            ParameterName = "GateParam",
            LimitType = LimitType.Qualitative,
            ExpectedResultText = "Clear",
            SpecLimit = "Clear",
            IsRetired = retiredSpec,
            RetiredAt = retiredSpec ? BaseNow.AddDays(-1) : null,
            RetiredByUserId = retiredSpec ? userId : null
        };
        db.Specifications.Add(spec);

        var s1 = new TestWorkflowStep
        {
            TestDefinitionId = testDef.Id,
            StepOrder = 1,
            StepName = "Step 1",
            StepType = StepType.BiochemicalTest,
            PhenotypicTestType = PhenotypicTestType.Catalase,
            IncubationMinHours = 18,
            IncubationMaxHours = 24,
            TemperatureMin = 30,
            TemperatureMax = 35
        };
        var s2 = new TestWorkflowStep
        {
            TestDefinitionId = testDef.Id,
            StepOrder = 2,
            StepName = "Step 2",
            StepType = StepType.BiochemicalTest,
            PhenotypicTestType = PhenotypicTestType.Oxidase,
            IsFinalStep = true,
            IncubationMinHours = 18,
            IncubationMaxHours = 24,
            TemperatureMin = 30,
            TemperatureMax = 35
        };
        db.TestWorkflowSteps.AddRange(s1, s2);

        var rep = new TestDefinitionStageReplicate
        {
            TestDefinitionId = testDef.Id,
            Role = ProductionStageRole.Bulk,
            SampleReplicates = 2
        };
        db.TestDefinitionStageReplicates.Add(rep);
        await db.SaveChangesAsync();

        return new GateWorld(userId, qaUser.Id, sectionId, otherSectionId, item.Id, testDef.Id, testCode,
            spec.Id, s1.Id, s2.Id, rep.Id, "TST-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant());
    }

    private async Task<(int RecordId, string Number, int LinkId)> SeedChangeControlAsync(
        MutableClock clock,
        int userId,
        int? sectionId,
        ChangeControlStatus status,
        QualityLinkTargetType targetType,
        int? targetId,
        string? newRecordCode = null,
        bool isCancelled = false)
    {
        await using var db = Context(clock);
        var number = "CC-2086-" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        var record = new QualityRecord
        {
            Number = number,
            Type = QualityRecordType.ChangeControl,
            Title = "Gate CC",
            Description = "Gate CC desc",
            SectionId = sectionId,
            RaisedByUserId = userId,
            OwnerUserId = userId,
            RaisedAtUtc = BaseNow,
            IsCancelled = isCancelled
        };
        db.QualityRecords.Add(record);
        await db.SaveChangesAsync();

        db.ChangeControlDetails.Add(new ChangeControlDetail
        {
            RecordId = record.Id,
            Status = status,
            Reason = "Gate test CC"
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

    private async Task<int?> ExecuteGatedMethodAsync(
        MicroLimsDbContext actDb,
        MutableClock clock,
        GateWorld w,
        GatedMethod method,
        string? ccNumber)
    {
        var ccCtx = new ChangeControlContext { Number = ccNumber };
        var gate = new ChangeControlGate(actDb, ccCtx, clock);
        var scope = new UserSectionScopeService(actDb);
        var audit = new AuditEventService(actDb, fixture.CreateSequenceHelper(actDb), clock);
        var specService = new SpecificationMasterDataService(actDb, scope, timeProvider: clock, changeControlGate: gate);
        var testDefService = new TestDefinitionMasterDataService(actDb, scope, audit, changeControlGate: gate);
        var stepService = new TestWorkflowStepMasterDataService(actDb, scope, gate);
        var repService = new TestStageReplicateMasterDataService(actDb, scope, gate);

        switch (method)
        {
            case GatedMethod.SpecCreate:
                return (await specService.CreateSpecificationAsync(w.UserId, new CreateSpecificationRequest(
                    ItemId: w.ItemId, TestCode: w.TestCode, ParameterName: "CreatedSpec",
                    LimitType: LimitType.Qualitative, ExpectedResultText: "Pass"))).Id;
            case GatedMethod.SpecUpdate:
                await specService.UpdateSpecificationAsync(w.UserId, w.SpecId, new UpdateSpecificationRequest(
                    TestCode: w.TestCode, ParameterName: "UpdatedSpec",
                    LimitType: LimitType.Qualitative, ExpectedResultText: "UpdatedPass"));
                return w.SpecId;
            case GatedMethod.SpecDelete:
                await specService.DeleteSpecificationAsync(w.UserId, w.SpecId, "Delete spec under CC");
                return w.SpecId;
            case GatedMethod.SpecReinstate:
                await specService.ReinstateSpecificationAsync(w.UserId, w.SpecId, "Reinstate spec under CC");
                return w.SpecId;
            case GatedMethod.TestDefCreate:
                return (await testDefService.CreateTestDefinitionAsync(w.UserId, new CreateTestDefinitionRequest(
                    Code: w.NewTestCode, DisplayName: "Created Test", SectionId: w.SectionId))).Id;
            case GatedMethod.TestDefUpdate:
                await testDefService.UpdateTestDefinitionAsync(w.UserId, w.TestDefId, new UpdateTestDefinitionRequest(
                    Code: w.TestCode, DisplayName: "Updated Test"));
                return w.TestDefId;
            case GatedMethod.TestDefFreeze:
                await testDefService.FreezeTestDefinitionAsync(w.UserId, w.TestDefId, changeReason: "Freeze test under CC");
                return w.TestDefId;
            case GatedMethod.TestDefUnfreeze:
                await testDefService.UnfreezeTestDefinitionAsync(w.UserId, w.TestDefId, changeReason: "Unfreeze test under CC");
                return w.TestDefId;
            case GatedMethod.StepUpdateWorkflowType:
                await stepService.UpdateWorkflowTypeAsync(w.UserId, w.TestDefId, new UpdateWorkflowTypeRequest(WorkflowType.CountTest));
                return w.TestDefId;
            case GatedMethod.StepCreate:
                await stepService.CreateTestWorkflowStepAsync(w.UserId, w.TestDefId, new CreateTestWorkflowStepRequest(
                    "Step 3", 12, 24, 30, 35, false, StepType.BiochemicalTest, null, [], false, null, null, PhenotypicTestType.Catalase));
                return null;
            case GatedMethod.StepUpdate:
                await stepService.UpdateTestWorkflowStepAsync(w.UserId, w.Step1Id, new UpdateTestWorkflowStepRequest(
                    "Renamed Step 1", 10, 20, 30, 35, false, StepType.BiochemicalTest, null, [], false, null, null, PhenotypicTestType.Catalase));
                return w.Step1Id;
            case GatedMethod.StepMove:
                await stepService.MoveTestWorkflowStepAsync(w.UserId, w.Step1Id, new MoveTestWorkflowStepRequest("down"));
                return w.Step1Id;
            case GatedMethod.StepDelete:
                await stepService.DeleteTestWorkflowStepAsync(w.UserId, w.Step2Id);
                return w.Step2Id;
            case GatedMethod.ReplicateCreate:
                await repService.CreateTestDefinitionStageReplicateAsync(w.UserId, w.TestDefId,
                    new CreateTestDefinitionStageReplicateRequest(ProductionStageRole.Finished, 4));
                return null;
            case GatedMethod.ReplicateUpdate:
                await repService.UpdateTestDefinitionStageReplicateAsync(w.UserId, w.TestDefId, w.ReplicateId,
                    new UpdateTestDefinitionStageReplicateRequest(6));
                return w.ReplicateId;
            case GatedMethod.ReplicateDelete:
                await repService.DeleteTestDefinitionStageReplicateAsync(w.UserId, w.TestDefId, w.ReplicateId);
                return w.ReplicateId;
            default:
                throw new ArgumentOutOfRangeException(nameof(method));
        }
    }

    [PostgresFact]
    public Task Every_gated_write_is_refused_without_change_control() =>
        PostgresCases.RunAsync(Enum.GetValues<GatedMethod>(), async method =>
        {
            var clock = new MutableClock(BaseNow);
            var w = await ArrangeGateWorldAsync(clock,
                gateOn: true,
                retiredSpec: method == GatedMethod.SpecReinstate,
                frozenTest: method == GatedMethod.TestDefUnfreeze);

            long watermark;
            await using (var wmDb = Context(clock))
                watermark = await wmDb.AuditLogs.AsNoTracking().Select(a => (long?)a.Id).MaxAsync() ?? 0L;

            await using (var actDb = Context(clock, w.UserId))
            {
                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    ExecuteGatedMethodAsync(actDb, clock, w, method, ccNumber: null));
                Assert.Equal(MessageCodes.ChangeControlGateRequired, ex.Code);
                Assert.Equal("This laboratory requires an approved change control for this change.", ex.Message);
            }

            await using var assertDb = Context(clock);
            Assert.False(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a => a.Id > watermark));
            Assert.Equal("GateParam", (await assertDb.Specifications.AsNoTracking().SingleAsync(s => s.Id == w.SpecId)).ParameterName);
            Assert.Equal("Gate Test", (await assertDb.TestDefinitions.AsNoTracking().SingleAsync(t => t.Id == w.TestDefId)).DisplayName);
            Assert.Equal(2, await assertDb.TestWorkflowSteps.AsNoTracking().CountAsync(s => s.TestDefinitionId == w.TestDefId));
            Assert.Equal(1, await assertDb.TestDefinitionStageReplicates.AsNoTracking().CountAsync(r => r.TestDefinitionId == w.TestDefId));
        });

    [PostgresFact]
    public Task Approved_change_control_allows_listed_record_and_marks_link_changed() =>
        PostgresCases.RunAsync(Enum.GetValues<GatedMethod>(), async method =>
        {
            var clock = new MutableClock(BaseNow);
            var w = await ArrangeGateWorldAsync(clock,
                gateOn: true,
                retiredSpec: method == GatedMethod.SpecReinstate,
                frozenTest: method == GatedMethod.TestDefUnfreeze);

            var (targetType, targetId, newCode) = method switch
            {
                GatedMethod.SpecCreate => (QualityLinkTargetType.Item, (int?)w.ItemId, (string?)null),
                GatedMethod.SpecUpdate or GatedMethod.SpecDelete or GatedMethod.SpecReinstate =>
                    (QualityLinkTargetType.Specification, (int?)w.SpecId, (string?)null),
                GatedMethod.TestDefCreate =>
                    (QualityLinkTargetType.TestDefinition, (int?)null, (string?)$"  {w.NewTestCode.ToLowerInvariant()}  "),
                _ => (QualityLinkTargetType.TestDefinition, (int?)w.TestDefId, (string?)null)
            };

            var (_, ccNumber, linkId) = await SeedChangeControlAsync(
                clock, w.UserId, w.SectionId, ChangeControlStatus.ApprovedForImplementation, targetType, targetId, newCode);

            long watermark;
            await using (var wmDb = Context(clock))
                watermark = await wmDb.AuditLogs.AsNoTracking().Select(a => (long?)a.Id).MaxAsync() ?? 0L;

            int? affectedEntityId;
            await using (var actDb = Context(clock, w.UserId))
            {
                affectedEntityId = await ExecuteGatedMethodAsync(actDb, clock, w, method, ccNumber);
            }

            await using var assertDb = Context(clock);
            var link = await assertDb.QualityRecordLinks.AsNoTracking().SingleAsync(l => l.Id == linkId);
            Assert.Equal(BaseNow, link.ChangedAtUtc);
            Assert.NotNull(link.ChangeAuditLogId);

            var newAudits = await assertDb.AuditLogs.AsNoTracking()
                .Where(a => a.Id > watermark)
                .OrderBy(a => a.Id)
                .ToListAsync();

            var expectedEntityName = method switch
            {
                GatedMethod.SpecCreate or GatedMethod.SpecUpdate or GatedMethod.SpecDelete or GatedMethod.SpecReinstate => nameof(Specification),
                GatedMethod.TestDefCreate or GatedMethod.TestDefUpdate or GatedMethod.TestDefFreeze or GatedMethod.TestDefUnfreeze or GatedMethod.StepUpdateWorkflowType => nameof(TestDefinition),
                GatedMethod.StepCreate or GatedMethod.StepUpdate or GatedMethod.StepMove or GatedMethod.StepDelete => nameof(TestWorkflowStep),
                _ => nameof(TestDefinitionStageReplicate)
            };

            var primaryAudit = newAudits
                .Where(a => a.EntityName == expectedEntityName && (affectedEntityId == null || a.EntityId == affectedEntityId.Value.ToString()))
                .OrderByDescending(a => a.Id)
                .First();
            Assert.Equal(primaryAudit.Id, link.ChangeAuditLogId);
            Assert.Contains($"Change control {ccNumber}", primaryAudit.Reason);

            var linkAudit = newAudits.Single(a => a.EntityName == nameof(QualityRecordLink) && a.EntityId == linkId.ToString());
            Assert.Contains($"Change control {ccNumber}", linkAudit.Reason);
        });

    [PostgresFact]
    public async Task Gate_refusal_messages_match_spec_for_every_rule()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeGateWorldAsync(clock, gateOn: true);

        // 1. Unknown number
        await using (var actDb = Context(clock, w.UserId))
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                ExecuteGatedMethodAsync(actDb, clock, w, GatedMethod.SpecUpdate, "CC-2086-9999"));
            Assert.Equal(MessageCodes.ChangeControlGateNotFound, ex.Code);
            Assert.Equal("Change control CC-2086-9999 was not found.", ex.Message);
        }

        // 2. Non-CC number (e.g., DEV-...)
        var devNumber = "DEV-2086-" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        await using (var devDb = Context(clock))
        {
            devDb.QualityRecords.Add(new QualityRecord
            {
                Number = devNumber,
                Type = QualityRecordType.Deviation,
                Title = "Dev",
                Description = "Dev",
                SectionId = w.SectionId,
                RaisedByUserId = w.UserId,
                RaisedAtUtc = BaseNow
            });
            await devDb.SaveChangesAsync();
        }
        await using (var actDb = Context(clock, w.UserId))
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                ExecuteGatedMethodAsync(actDb, clock, w, GatedMethod.SpecUpdate, devNumber));
            Assert.Equal(MessageCodes.ChangeControlGateNotFound, ex.Code);
            Assert.Equal($"Change control {devNumber} was not found.", ex.Message);
        }

        // 3. Non-approved statuses (Proposed, Assessed, Rejected, Closed, Cancelled)
        foreach (var (status, cancelled) in new[]
        {
            (ChangeControlStatus.Proposed, false),
            (ChangeControlStatus.Assessed, false),
            (ChangeControlStatus.Rejected, false),
            (ChangeControlStatus.Closed, false),
            (ChangeControlStatus.ApprovedForImplementation, true)
        })
        {
            var (_, ccNum, _) = await SeedChangeControlAsync(
                clock, w.UserId, w.SectionId, status, QualityLinkTargetType.Specification, w.SpecId, isCancelled: cancelled);
            await using var actDb = Context(clock, w.UserId);
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                ExecuteGatedMethodAsync(actDb, clock, w, GatedMethod.SpecUpdate, ccNum));
            Assert.Equal(MessageCodes.ChangeControlGateNotApproved, ex.Code);
            Assert.Equal($"Change control {ccNum} is not approved for implementation.", ex.Message);
        }

        // 4. Other laboratory
        var (_, otherLabCc, _) = await SeedChangeControlAsync(
            clock, w.UserId, w.OtherSectionId, ChangeControlStatus.ApprovedForImplementation, QualityLinkTargetType.Specification, w.SpecId);
        await using (var actDb = Context(clock, w.UserId))
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                ExecuteGatedMethodAsync(actDb, clock, w, GatedMethod.SpecUpdate, otherLabCc));
            Assert.Equal(MessageCodes.ChangeControlGateWrongLaboratory, ex.Code);
            Assert.Equal($"Change control {otherLabCc} belongs to another laboratory.", ex.Message);
        }

        // 5. Does not list this record (and listed Item / TestDefinition does NOT cover existing SpecUpdate)
        var (_, itemOnlyCc, _) = await SeedChangeControlAsync(
            clock, w.UserId, w.SectionId, ChangeControlStatus.ApprovedForImplementation, QualityLinkTargetType.Item, w.ItemId);
        await using (var actDb = Context(clock, w.UserId))
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                ExecuteGatedMethodAsync(actDb, clock, w, GatedMethod.SpecUpdate, itemOnlyCc));
            Assert.Equal(MessageCodes.ChangeControlGateRecordNotListed, ex.Code);
            Assert.Equal($"Change control {itemOnlyCc} does not list this record.", ex.Message);
        }
    }

    [PostgresFact]
    public async Task Site_wide_change_control_allows_change()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeGateWorldAsync(clock, gateOn: true);
        var (_, siteWideCc, linkId) = await SeedChangeControlAsync(
            clock, w.UserId, sectionId: null, ChangeControlStatus.ApprovedForImplementation, QualityLinkTargetType.Specification, w.SpecId);

        await using (var actDb = Context(clock, w.UserId))
        {
            await ExecuteGatedMethodAsync(actDb, clock, w, GatedMethod.SpecUpdate, siteWideCc);
        }

        await using var assertDb = Context(clock);
        var link = await assertDb.QualityRecordLinks.AsNoTracking().SingleAsync(l => l.Id == linkId);
        Assert.Equal(BaseNow, link.ChangedAtUtc);
    }

    [PostgresFact]
    public async Task Cross_laboratory_move_checks_both_sections()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeGateWorldAsync(clock, gateOn: true);

        // Give w.UserId configure access to OtherSectionId as well
        await using (var grantDb = Context(clock))
        {
            var otherDept = await grantDb.DocumentSections.AsNoTracking().Where(s => s.Id == w.OtherSectionId).Select(s => s.DepartmentId).SingleAsync();
            grantDb.UserOrgMemberships.Add(new UserOrgMembership { UserId = w.UserId, DepartmentId = otherDept, SectionId = w.OtherSectionId });
            await grantDb.SaveChangesAsync();
        }

        var (_, sectionACc, _) = await SeedChangeControlAsync(
            clock, w.UserId, w.SectionId, ChangeControlStatus.ApprovedForImplementation, QualityLinkTargetType.TestDefinition, w.TestDefId);

        // Moving test from SectionId to OtherSectionId with a CC scoped only to SectionId -> refused
        await using (var actDb = Context(clock, w.UserId))
        {
            var ccCtx = new ChangeControlContext { Number = sectionACc };
            var gate = new ChangeControlGate(actDb, ccCtx, clock);
            var scope = new UserSectionScopeService(actDb);
            var svc = new TestDefinitionMasterDataService(actDb, scope, new AuditEventService(actDb, fixture.CreateSequenceHelper(actDb), clock), changeControlGate: gate);

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                svc.UpdateTestDefinitionAsync(w.UserId, w.TestDefId, new UpdateTestDefinitionRequest(w.TestCode, "Moved Test", SectionId: w.OtherSectionId)));
            Assert.Equal(MessageCodes.ChangeControlGateWrongLaboratory, ex.Code);
        }

        // Site-wide CC allows cross-lab move
        var (_, siteCc, linkId) = await SeedChangeControlAsync(
            clock, w.UserId, sectionId: null, ChangeControlStatus.ApprovedForImplementation, QualityLinkTargetType.TestDefinition, w.TestDefId);
        await using (var actDb = Context(clock, w.UserId))
        {
            var ccCtx = new ChangeControlContext { Number = siteCc };
            var gate = new ChangeControlGate(actDb, ccCtx, clock);
            var scope = new UserSectionScopeService(actDb);
            var svc = new TestDefinitionMasterDataService(actDb, scope, new AuditEventService(actDb, fixture.CreateSequenceHelper(actDb), clock), changeControlGate: gate);

            await svc.UpdateTestDefinitionAsync(w.UserId, w.TestDefId, new UpdateTestDefinitionRequest(w.TestCode, "Moved Test", SectionId: w.OtherSectionId));
        }

        await using var assertDb = Context(clock);
        var moved = await assertDb.TestDefinitions.AsNoTracking().SingleAsync(t => t.Id == w.TestDefId);
        Assert.Equal(w.OtherSectionId, moved.SectionId);
        Assert.Equal(BaseNow, (await assertDb.QualityRecordLinks.AsNoTracking().SingleAsync(l => l.Id == linkId)).ChangedAtUtc);
    }

    [PostgresFact]
    public async Task CreateTestWithSteps_and_subsequent_edit_match_by_new_record_code()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeGateWorldAsync(clock, gateOn: true);
        var (_, ccNum, linkId) = await SeedChangeControlAsync(
            clock, w.UserId, w.SectionId, ChangeControlStatus.ApprovedForImplementation, QualityLinkTargetType.TestDefinition, null, $"  {w.NewTestCode.ToLowerInvariant()}  ");

        int createdTestId;
        await using (var actDb = Context(clock, w.UserId))
        {
            var ccCtx = new ChangeControlContext { Number = ccNum };
            var gate = new ChangeControlGate(actDb, ccCtx, clock);
            var scope = new UserSectionScopeService(actDb);
            var svc = new TestDefinitionMasterDataService(actDb, scope, new AuditEventService(actDb, fixture.CreateSequenceHelper(actDb), clock), changeControlGate: gate);

            var resp = await svc.CreateTestWithStepsAsync(w.UserId, new SaveTestWithStepsRequest(
                new CreateTestDefinitionRequest(w.NewTestCode, "Created With Steps", w.SectionId),
                [new CreateTestWorkflowStepRequest("Step A", 12, 24, 30, 35, true, StepType.BiochemicalTest, null, [], false, null, null, PhenotypicTestType.Catalase)],
                [new CreateTestDefinitionStageReplicateRequest(ProductionStageRole.Bulk, 3)]));
            createdTestId = resp.Id;
        }

        // Later edit in a DIFFERENT request with a later timestamp still matches by NewRecordCode and updates ChangedAtUtc
        var laterTime = BaseNow.AddHours(2);
        clock.UtcNow = laterTime;
        await using (var actDb2 = Context(clock, w.UserId))
        {
            var ccCtx = new ChangeControlContext { Number = ccNum };
            var gate = new ChangeControlGate(actDb2, ccCtx, clock);
            var scope = new UserSectionScopeService(actDb2);
            var svc = new TestDefinitionMasterDataService(actDb2, scope, new AuditEventService(actDb2, fixture.CreateSequenceHelper(actDb2), clock), changeControlGate: gate);

            await svc.UpdateTestDefinitionAsync(w.UserId, createdTestId, new UpdateTestDefinitionRequest(w.NewTestCode, "Updated After Create"));
        }

        await using var assertDb = Context(clock);
        var link = await assertDb.QualityRecordLinks.AsNoTracking().SingleAsync(l => l.Id == linkId);
        Assert.Equal(laterTime, link.ChangedAtUtc);
    }

    [PostgresFact]
    public async Task Batch_copy_and_workbook_paths_respect_gate()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeGateWorldAsync(clock, gateOn: true);
        int targetItemId;
        string itemCode;
        await using (var db = Context(clock))
        {
            var targetItem = new Item { Code = "ITM-TGT-" + Guid.NewGuid().ToString("N")[..6], Name = "Target Item", IsActive = true };
            db.Items.Add(targetItem);
            await db.SaveChangesAsync();
            db.SampleTests.Add(new SampleTest { ItemId = targetItem.Id, TestCode = w.TestCode });
            await db.SaveChangesAsync();
            targetItemId = targetItem.Id;
            itemCode = await db.Items.AsNoTracking().Where(i => i.Id == w.ItemId).Select(i => i.Code).SingleAsync();
        }

        // 1. Batch refused without CC
        await using (var actDb = Context(clock, w.UserId))
        {
            var gate = new ChangeControlGate(actDb, new ChangeControlContext(), clock);
            var specSvc = new SpecificationMasterDataService(actDb, new UserSectionScopeService(actDb), timeProvider: clock, changeControlGate: gate);
            var ex = await Assert.ThrowsAsync<FieldValidationException>(() =>
                specSvc.CreateSpecificationsBatchAsync(w.UserId, [new CreateSpecificationRequest(w.ItemId, w.TestCode, ParameterName: "BatchP", LimitType: LimitType.Qualitative, ExpectedResultText: "Ok")]));
            Assert.Contains("requires an approved change control", ex.Message);
        }

        // 2. Copy refused without CC
        await using (var actDb = Context(clock, w.UserId))
        {
            var gate = new ChangeControlGate(actDb, new ChangeControlContext(), clock);
            var scope = new UserSectionScopeService(actDb);
            var specSvc = new SpecificationMasterDataService(actDb, scope, timeProvider: clock, changeControlGate: gate);
            var copySvc = new MasterDataCopyService(actDb, scope, new SpecificationService(actDb), specSvc, new ItemService(actDb));
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                copySvc.CopyAsync(new CopySpecificationsRequest(w.ItemId, targetItemId, [w.SpecId]), w.UserId));
            Assert.Equal(MessageCodes.ChangeControlGateRequired, ex.Code);
        }

        // 3. Workbook per-row savepoint: refusal becomes a row error, nothing persisted
        await using (var actDb = Context(clock, w.UserId))
        {
            var gate = new ChangeControlGate(actDb, new ChangeControlContext(), clock);
            var specSvc = new SpecificationMasterDataService(actDb, new UserSectionScopeService(actDb), timeProvider: clock, changeControlGate: gate);
            var runner = new MasterDataWorkbookRunner(actDb, [new SpecificationsSheet(specSvc)]);
            var sheet = new WorkbookSheet(SpecificationsSheet.SheetName,
                ["ItemCode", "TestCode", "ParameterName", "LimitType", "ExpectedResultText"],
                [new WorkbookRow(2, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ItemCode"] = itemCode,
                    ["TestCode"] = w.TestCode,
                    ["ParameterName"] = "WbParam",
                    ["LimitType"] = nameof(LimitType.Qualitative),
                    ["ExpectedResultText"] = "Pass"
                })]);

            var (report, created) = await UnitOfWork.RunAsync(actDb, () => runner.RunAsync([sheet], w.UserId));
            Assert.Empty(created);
            var err = Assert.Single(report.Errors);
            Assert.Contains("requires an approved change control", err.Message);
        }

        // 4. With approved CC, workbook & copy succeed
        var (_, ccNum, _) = await SeedChangeControlAsync(
            clock, w.UserId, w.SectionId, ChangeControlStatus.ApprovedForImplementation, QualityLinkTargetType.TestDefinition, w.TestDefId);
        await using (var actDb = Context(clock, w.UserId))
        {
            var ccCtx = new ChangeControlContext { Number = ccNum };
            var gate = new ChangeControlGate(actDb, ccCtx, clock);
            var scope = new UserSectionScopeService(actDb);
            var specSvc = new SpecificationMasterDataService(actDb, scope, timeProvider: clock, changeControlGate: gate);
            var copySvc = new MasterDataCopyService(actDb, scope, new SpecificationService(actDb), specSvc, new ItemService(actDb));
            var copied = await copySvc.CopyAsync(new CopySpecificationsRequest(w.ItemId, targetItemId, [w.SpecId]), w.UserId);
            Assert.Equal(1, copied);
        }
    }

    [PostgresFact]
    public async Task Section_settings_and_header_middleware_enforce_rules()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeGateWorldAsync(clock, gateOn: false);

        // Middleware validation
        var middleware = new ChangeControlHeaderMiddleware(_ => Task.CompletedTask);
        var ctxBad = new DefaultHttpContext();
        ctxBad.Request.Headers[ChangeControlHeaderMiddleware.HeaderName] = "BAD_CHAR!";
        var hdrEx = await Assert.ThrowsAsync<BusinessRuleException>(() => middleware.InvokeAsync(ctxBad, new ChangeControlContext()));
        Assert.Equal(MessageCodes.ChangeControlHeaderInvalid, hdrEx.Code);
        Assert.DoesNotContain("BAD_CHAR!", hdrEx.Message);

        var ctxGood = new DefaultHttpContext();
        ctxGood.Request.Headers[ChangeControlHeaderMiddleware.HeaderName] = "  cc-2086-0001  ";
        var ccCtx = new ChangeControlContext();
        await middleware.InvokeAsync(ctxGood, ccCtx);
        Assert.Equal("CC-2086-0001", ccCtx.Number);

        // Non-QA cannot PUT section setting
        await using (var actDb = Context(clock, w.UserId))
        {
            var cfg = BuildConfigService(actDb, clock);
            var own = await cfg.GetSectionSettingAsync(w.SectionId, w.UserId);
            Assert.False(own.ChangeControlRequired);
            await Assert.ThrowsAsync<CodedAccessDeniedException>(() => cfg.GetSectionSettingAsync(w.OtherSectionId, w.UserId));
            await Assert.ThrowsAsync<CodedAccessDeniedException>(() =>
                cfg.SetSectionSettingAsync(w.SectionId, true, "Enable gate", QaPassword, 0u, w.UserId));
        }

        // QA enables gate with reason + signature; blank reason & stale version refused
        uint version;
        await using (var actDb = Context(clock, w.QaUserId))
        {
            var cfg = BuildConfigService(actDb, clock);
            var reasonEx = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                cfg.SetSectionSettingAsync(w.SectionId, true, "   ", QaPassword, 0u, w.QaUserId));
            Assert.Equal(MessageCodes.ChangeControlGateReasonRequired, reasonEx.Code);

            var updated = await cfg.SetSectionSettingAsync(w.SectionId, true, "Enable change control", QaPassword, 0u, w.QaUserId);
            Assert.True(updated.ChangeControlRequired);
            Assert.True(updated.Version > 0);
            version = updated.Version;

            await Assert.ThrowsAnyAsync<DbUpdateConcurrencyException>(() =>
                cfg.SetSectionSettingAsync(w.SectionId, false, "Stale", QaPassword, 0u, w.QaUserId));
        }

        await using var assertDb = Context(clock);
        var persisted = await assertDb.QualitySectionSettings.AsNoTracking().SingleAsync(s => s.SectionId == w.SectionId);
        Assert.True(persisted.ChangeControlRequired);
        Assert.Equal(version, persisted.Version);
        Assert.True(await assertDb.ElectronicSignatures.AsNoTracking().AnyAsync(s =>
            s.EntityType == nameof(QualitySectionSetting) &&
            s.EntityId == w.SectionId &&
            s.MeaningOfSignature == SignatureMeaning.ChangeControlGateChanged));
    }

    [PostgresFact]
    public async Task Gate_and_Close_serialize_on_QualityRecords_lock()
    {
        var clock = new MutableClock(BaseNow);
        var w = await ArrangeGateWorldAsync(clock, gateOn: true);
        var (ccId, ccNum, _) = await SeedChangeControlAsync(
            clock, w.QaUserId, w.SectionId, ChangeControlStatus.ApprovedForImplementation, QualityLinkTargetType.Specification, w.SpecId);

        // 1. Close holds QualityRecords FOR UPDATE -> gate times out with 55P03
        await using (var closeDb = Context(clock, w.QaUserId))
        await using (var closeTx = await closeDb.Database.BeginTransactionAsync())
        {
            await closeDb.Database.ExecuteSqlInterpolatedAsync(
                $"""SELECT "Id" FROM "QualityRecords" WHERE "Id" = {ccId} FOR UPDATE""");

            await using var gateDb = Context(clock, w.UserId);
            await using var gateTx = await gateDb.Database.BeginTransactionAsync();
            await gateDb.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '150ms'");
            var gate = new ChangeControlGate(gateDb, new ChangeControlContext { Number = ccNum }, clock);
            var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
                gate.EnsureAllowedAsync(w.SectionId, QualityLinkTargetType.Specification, w.SpecId, null));
            var pgEx = ex as PostgresException ?? ex.InnerException as PostgresException;
            Assert.NotNull(pgEx);
            Assert.Equal("55P03", pgEx.SqlState);
        }

        // 2. Gate holds QualityRecords FOR SHARE -> Close times out with 55P03
        await using (var gateDb = Context(clock, w.UserId))
        await using (var gateTx = await gateDb.Database.BeginTransactionAsync())
        {
            var gate = new ChangeControlGate(gateDb, new ChangeControlContext { Number = ccNum }, clock);
            await gate.EnsureAllowedAsync(w.SectionId, QualityLinkTargetType.Specification, w.SpecId, null);

            await using var closeDb = Context(clock, w.QaUserId);
            await using var closeTx = await closeDb.Database.BeginTransactionAsync();
            await closeDb.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '150ms'");
            var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
                closeDb.Database.ExecuteSqlInterpolatedAsync(
                    $"""SELECT "Id" FROM "QualityRecords" WHERE "Id" = {ccId} FOR UPDATE"""));
            var pgEx = ex as PostgresException ?? ex.InnerException as PostgresException;
            Assert.NotNull(pgEx);
            Assert.Equal("55P03", pgEx.SqlState);
        }
    }

    private QualityConfigService BuildConfigService(MicroLimsDbContext db, MutableClock clock)
    {
        var permissions = new PermissionService(db);
        var scope = new UserSectionScopeService(db);
        var sigs = new ElectronicSignatureService(db, clock);
        var core = new QualityRecordCore(db, permissions, scope, new ReferenceNumberGenerator(db, clock), sigs, clock);
        return new QualityConfigService(db, core, permissions, scope, clock, sigs);
    }
}
