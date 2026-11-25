using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Entities.Quality;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
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
        SettingFalse
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

    public static IEnumerable<object[]> GateOffCases()
    {
        foreach (var method in Enum.GetValues<GatedMethod>())
        {
            yield return [method, GateOffMode.NoSettingRow];
            yield return [method, GateOffMode.SettingFalse];
        }
    }

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

        await using (var arrangeDb = Context(clock))
        {
            (userId, sectionId) = await CreateSectionHeadAsync(arrangeDb);

            if (mode == GateOffMode.SettingFalse)
            {
                arrangeDb.QualitySectionSettings.Add(new QualitySectionSetting
                {
                    SectionId = sectionId,
                    ChangeControlRequired = false
                });
                await arrangeDb.SaveChangesAsync();
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
            var scope = new UserSectionScopeService(actDb);
            var audit = new AuditEventService(actDb, fixture.CreateSequenceHelper(actDb), clock);
            var specService = new SpecificationMasterDataService(actDb, scope, timeProvider: clock);
            var testDefService = new TestDefinitionMasterDataService(actDb, scope, audit);
            var stepService = new TestWorkflowStepMasterDataService(actDb, scope);
            var repService = new TestStageReplicateMasterDataService(actDb, scope);

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

            Assert.Equal(noRow.EntitySnapshot, settingFalse.EntitySnapshot);
            Assert.Equal(noRow.Audits.Count, settingFalse.Audits.Count);
            for (var i = 0; i < noRow.Audits.Count; i++)
            {
                Assert.Equal(noRow.Audits[i].Action, settingFalse.Audits[i].Action);
                Assert.Equal(noRow.Audits[i].EntityName, settingFalse.Audits[i].EntityName);
                Assert.Equal(noRow.Audits[i].Reason, settingFalse.Audits[i].Reason);
                Assert.Equal(noRow.Audits[i].OldValues, settingFalse.Audits[i].OldValues);
                Assert.Equal(noRow.Audits[i].NewValues, settingFalse.Audits[i].NewValues);
            }
        }
    }
}
