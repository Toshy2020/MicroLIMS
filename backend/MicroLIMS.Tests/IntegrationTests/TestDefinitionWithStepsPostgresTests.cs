using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class TestDefinitionWithStepsPostgresTests
{
    private readonly PostgresTestFixture _fixture;

    public TestDefinitionWithStepsPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed record World(
        int UserId,
        int SectionId,
        int ForbiddenSectionId,
        int MediaProductId,
        int ConditionId,
        int ConfirmatoryMedia1Id,
        int ConfirmatoryCondition1Id,
        int ConfirmatoryMedia2Id,
        int ConfirmatoryCondition2Id,
        int OrganismId);

    private static string NewTag() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private async Task<World> ArrangeWorldAsync()
    {
        var tag = NewTag();
        await using var arrangeDb = _fixture.CreateDbContext();
        arrangeDb.CurrentUserId = _fixture.SeededUserId;

        var dept = new DocumentDepartment { Name = "Dept " + tag, Code = "D" + tag, IsActive = true };
        arrangeDb.DocumentDepartments.Add(dept);
        await arrangeDb.SaveChangesAsync();

        var allowedSection = new DocumentSection { Name = "Allowed " + tag, Code = "SEC1-" + tag, DepartmentId = dept.Id, IsActive = true };
        var forbiddenSection = new DocumentSection { Name = "Forbidden " + tag, Code = "SEC2-" + tag, DepartmentId = dept.Id, IsActive = true };
        arrangeDb.DocumentSections.AddRange(allowedSection, forbiddenSection);
        await arrangeDb.SaveChangesAsync();

        var headRole = await arrangeDb.Roles.AsNoTracking().FirstAsync(r => r.Type == RoleType.SectionHead);
        var user = new User
        {
            Username = $"test_head_{tag}",
            FullName = $"Test Head {tag}",
            Email = $"test_head_{tag}@test.local",
            PasswordHash = "x",
            RoleId = headRole.Id,
            IsActive = true
        };
        arrangeDb.Users.Add(user);
        await arrangeDb.SaveChangesAsync();

        arrangeDb.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = user.Id,
            DepartmentId = dept.Id,
            SectionId = allowedSection.Id
        });

        var media1 = new MediaProduct { Name = $"TSB {tag}", Code = $"M1{tag}", IsActive = true };
        var media2 = new MediaProduct { Name = $"XLD {tag}", Code = $"M2{tag}", IsActive = true };
        var media3 = new MediaProduct { Name = $"TSI {tag}", Code = $"M3{tag}", IsActive = true };
        arrangeDb.MediaProducts.AddRange(media1, media2, media3);

        var organism = new Organism { ScientificName = $"Test organism {tag}", CommonName = $"Org {tag}" };
        arrangeDb.Organisms.Add(organism);
        await arrangeDb.SaveChangesAsync();

        var cond1 = new MediaIncubationCondition { MediaProductId = media1.Id, IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 30m, TemperatureMax = 35m };
        var cond2 = new MediaIncubationCondition { MediaProductId = media2.Id, IncubationMinHours = 24, IncubationMaxHours = 48, TemperatureMin = 35m, TemperatureMax = 37m };
        var cond3 = new MediaIncubationCondition { MediaProductId = media3.Id, IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 35m, TemperatureMax = 37m };
        arrangeDb.MediaIncubationConditions.AddRange(cond1, cond2, cond3);
        await arrangeDb.SaveChangesAsync();

        return new World(
            user.Id,
            allowedSection.Id,
            forbiddenSection.Id,
            media1.Id,
            cond1.Id,
            media2.Id,
            cond2.Id,
            media3.Id,
            cond3.Id,
            organism.Id);
    }

    private static (List<CreateTestDefinitionStageReplicateRequest> Stages, List<CreateTestWorkflowStepRequest> Steps) BuildValidStagesAndSteps(
        World w,
        string stepPrefix)
    {
        var stages = new List<CreateTestDefinitionStageReplicateRequest>
        {
            new(ProductionStageRole.Bulk, 2),
            new(ProductionStageRole.Finished, 3)
        };

        var steps = new List<CreateTestWorkflowStepRequest>
        {
            new(
                StepName: $"{stepPrefix} Enrichment",
                IncubationMinHours: 0,
                IncubationMaxHours: 0,
                TemperatureMin: 0m,
                TemperatureMax: 0m,
                IsFinalStep: false,
                StepType: StepType.BrothEnrichment,
                TargetOrganismId: null,
                StepMedia: new List<StepMediaRequest>
                {
                    new(w.MediaProductId, IsRequired: true, DisplayOrder: 0, MediaIncubationConditionId: w.ConditionId)
                },
                RequiresIncubationTransfer: false,
                IncubationStages: null,
                ConfirmatoryMediaCount: null,
                PhenotypicTestType: null),
            new(
                StepName: $"{stepPrefix} Confirmatory",
                IncubationMinHours: 0,
                IncubationMaxHours: 0,
                TemperatureMin: 0m,
                TemperatureMax: 0m,
                IsFinalStep: false,
                StepType: StepType.ConfirmatoryPlating,
                TargetOrganismId: w.OrganismId,
                StepMedia: new List<StepMediaRequest>
                {
                    new(w.ConfirmatoryMedia1Id, IsRequired: false, DisplayOrder: 0, MediaIncubationConditionId: w.ConfirmatoryCondition1Id),
                    new(w.ConfirmatoryMedia2Id, IsRequired: false, DisplayOrder: 1, MediaIncubationConditionId: w.ConfirmatoryCondition2Id)
                },
                RequiresIncubationTransfer: false,
                IncubationStages: null,
                ConfirmatoryMediaCount: 2,
                PhenotypicTestType: null),
            new(
                StepName: $"{stepPrefix} Identification",
                IncubationMinHours: 0,
                IncubationMaxHours: 0,
                TemperatureMin: 0m,
                TemperatureMax: 0m,
                IsFinalStep: true,
                StepType: StepType.BiochemicalTest,
                TargetOrganismId: null,
                StepMedia: new List<StepMediaRequest>(),
                RequiresIncubationTransfer: false,
                IncubationStages: null,
                ConfirmatoryMediaCount: null,
                PhenotypicTestType: PhenotypicTestType.Gram,
                PhenotypicTestTypes: new List<PhenotypicTestType> { PhenotypicTestType.Gram, PhenotypicTestType.Oxidase })
        };

        return (stages, steps);
    }

    private static void AssertMappedPropertiesEqual<TEntity>(
        MicroLimsDbContext db,
        TEntity expected,
        TEntity actual,
        IReadOnlySet<string> excludedProperties)
    {
        var entityType = db.Model.FindEntityType(typeof(TEntity));
        Assert.NotNull(entityType);

        foreach (var efProperty in entityType.GetProperties())
        {
            if (excludedProperties.Contains(efProperty.Name))
                continue;

            var clrProperty = typeof(TEntity).GetProperty(efProperty.Name);
            Assert.NotNull(clrProperty);

            var expectedValue = clrProperty.GetValue(expected);
            var actualValue = clrProperty.GetValue(actual);
            Assert.True(
                Equals(expectedValue, actualValue),
                $"Mapped property {typeof(TEntity).Name}.{efProperty.Name} differed: expected <{expectedValue ?? "null"}>, actual <{actualValue ?? "null"}>.");
        }
    }

    [PostgresFact]
    public async Task WithSteps_CreatesTestStagesSteps_Atomically()
    {
        var world = await ArrangeWorldAsync();
        var tag = NewTag();
        var testCode = "ATOM-" + tag;
        var (stages, steps) = BuildValidStagesAndSteps(world, "Atomic " + tag);

        var testRequest = new CreateTestDefinitionRequest(
            Code: testCode,
            DisplayName: "Atomic Test " + tag,
            SectionId: world.SectionId,
            WorkflowType: WorkflowType.Observation)
        {
            CoaDisplayOrder = 4
        };

        int createdId;
        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = world.UserId;
            var svc = TestServiceFactory.TestDefinitionMaster(actDb);
            var response = await svc.CreateTestWithStepsAsync(
                world.UserId,
                new SaveTestWithStepsRequest(testRequest, steps, stages));
            createdId = response.Id;
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persistedTest = await assertDb.TestDefinitions
            .AsNoTracking()
            .SingleAsync(t => t.Id == createdId);
        Assert.Equal(testCode, persistedTest.Code);
        Assert.Equal("Atomic Test " + tag, persistedTest.DisplayName);
        Assert.Equal(world.SectionId, persistedTest.SectionId);
        Assert.Equal(WorkflowType.Observation, persistedTest.WorkflowType);
        Assert.Equal(4, persistedTest.CoaDisplayOrder);
        Assert.True(persistedTest.IsActive);

        var persistedStages = await assertDb.TestDefinitionStageReplicates
            .AsNoTracking()
            .Where(s => s.TestDefinitionId == createdId)
            .OrderBy(s => s.Role)
            .ToListAsync();
        Assert.Equal(2, persistedStages.Count);
        Assert.Equal(ProductionStageRole.Bulk, persistedStages[0].Role);
        Assert.Equal(2, persistedStages[0].SampleReplicates);
        Assert.Equal(ProductionStageRole.Finished, persistedStages[1].Role);
        Assert.Equal(3, persistedStages[1].SampleReplicates);

        var persistedSteps = await assertDb.TestWorkflowSteps
            .AsNoTracking()
            .Include(s => s.StepMedia)
            .Include(s => s.PhenotypicTests)
            .Where(s => s.TestDefinitionId == createdId)
            .OrderBy(s => s.StepOrder)
            .ToListAsync();
        Assert.Equal(3, persistedSteps.Count);
        Assert.Equal(new[] { 1, 2, 3 }, persistedSteps.Select(s => s.StepOrder).ToArray());
        Assert.False(persistedSteps[0].IsFinalStep);
        Assert.Single(persistedSteps[0].StepMedia);
        Assert.Equal(30m, persistedSteps[0].StepMedia[0].TempMin);
        Assert.Equal(2, persistedSteps[1].StepMedia.Count);
        Assert.Equal(world.OrganismId, persistedSteps[1].TargetOrganismId);
        Assert.True(persistedSteps[2].IsFinalStep);
        Assert.Equal(2, persistedSteps[2].PhenotypicTests.Count);

        var testAudit = await assertDb.AuditLogs
            .AsNoTracking()
            .SingleAsync(a => a.EntityName == nameof(TestDefinition) && a.EntityId == createdId.ToString() && a.Action == "Create");
        Assert.Equal(world.UserId, testAudit.UserId);

        var stageIds = persistedStages.Select(s => s.Id.ToString()).ToList();
        var stageAudits = await assertDb.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityName == nameof(TestDefinitionStageReplicate) && stageIds.Contains(a.EntityId) && a.Action == "Create")
            .ToListAsync();
        Assert.Equal(2, stageAudits.Count);
        Assert.All(stageAudits, a => Assert.Equal(world.UserId, a.UserId));

        var stepIds = persistedSteps.Select(s => s.Id.ToString()).ToList();
        var stepAudits = await assertDb.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityName == nameof(TestWorkflowStep) && stepIds.Contains(a.EntityId) && a.Action == "Create")
            .ToListAsync();
        Assert.Equal(3, stepAudits.Count);
        Assert.All(stepAudits, a => Assert.Equal(world.UserId, a.UserId));
    }

    [PostgresFact]
    public async Task WithSteps_InvalidStep_NothingCreated_FieldErrorNamesTheStep()
    {
        var world = await ArrangeWorldAsync();
        var tag = NewTag();
        var testCode = "INV-" + tag;
        var invalidStepName = "Broken Selective Step " + tag;
        var (stages, validSteps) = BuildValidStagesAndSteps(world, "InvalidRun " + tag);

        // Step index 1 violates WorkflowTemplateValidator rule 2 (SelectivePlating without TargetOrganismId).
        var badStep = new CreateTestWorkflowStepRequest(
            StepName: invalidStepName,
            IncubationMinHours: 0,
            IncubationMaxHours: 0,
            TemperatureMin: 0m,
            TemperatureMax: 0m,
            IsFinalStep: false,
            StepType: StepType.SelectivePlating,
            TargetOrganismId: null,
            StepMedia: new List<StepMediaRequest>
            {
                new(world.MediaProductId, IsRequired: true, DisplayOrder: 0, MediaIncubationConditionId: world.ConditionId)
            },
            RequiresIncubationTransfer: false,
            IncubationStages: null,
            ConfirmatoryMediaCount: null,
            PhenotypicTestType: null);

        var steps = new List<CreateTestWorkflowStepRequest> { validSteps[0], badStep };
        var request = new SaveTestWithStepsRequest(
            new CreateTestDefinitionRequest(
                Code: testCode,
                DisplayName: "Invalid Step Test " + tag,
                SectionId: world.SectionId,
                WorkflowType: WorkflowType.Observation),
            steps,
            stages);

        FieldValidationException ex;
        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = world.UserId;
            var svc = TestServiceFactory.TestDefinitionMaster(actDb);
            ex = await Assert.ThrowsAsync<FieldValidationException>(() =>
                svc.CreateTestWithStepsAsync(world.UserId, request));
        }

        var error = Assert.Single(ex.FieldErrors);
        Assert.Equal("steps", error.Field);
        Assert.Equal(1, error.Row);
        Assert.Contains("Step 2", error.Message);
        Assert.Contains(invalidStepName, error.Message);

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.TestDefinitions.AsNoTracking().AnyAsync(t => t.Code == testCode));
        Assert.False(await assertDb.TestWorkflowSteps.AsNoTracking().AnyAsync(s => s.StepName.StartsWith("InvalidRun " + tag) || s.StepName == invalidStepName));
        Assert.False(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a => a.UserId == world.UserId && a.EntityName == nameof(TestDefinition)));
        Assert.False(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a => a.UserId == world.UserId && a.EntityName == nameof(TestDefinitionStageReplicate)));
        Assert.False(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a => a.UserId == world.UserId && a.EntityName == nameof(TestWorkflowStep)));
    }

    [PostgresFact]
    public async Task WithSteps_ResultEqualsCreatingTestThenStepsSeparately()
    {
        var world = await ArrangeWorldAsync();
        var tag = NewTag();
        var (stages, steps) = BuildValidStagesAndSteps(world, "Parity " + tag);

        var separateReq = new CreateTestDefinitionRequest(
            Code: "SEP-" + tag,
            DisplayName: "Parity Test SEP " + tag,
            SectionId: world.SectionId,
            WorkflowType: WorkflowType.Observation)
        {
            CoaDisplayOrder = 7
        };
        var combinedReq = separateReq with
        {
            Code = "CMB-" + tag,
            DisplayName = "Parity Test CMB " + tag
        };

        int separateId;
        await using (var separateDb = _fixture.CreateDbContext())
        {
            separateDb.CurrentUserId = world.UserId;
            var scope = new UserSectionScopeService(separateDb);
            var testSvc = TestServiceFactory.TestDefinitionMaster(separateDb, scope);
            var stageSvc = new TestStageReplicateMasterDataService(separateDb, scope);
            var stepSvc = new TestWorkflowStepMasterDataService(separateDb, scope);

            var created = await testSvc.CreateTestDefinitionAsync(world.UserId, separateReq);
            separateId = created.Id;
            foreach (var stage in stages)
                await stageSvc.CreateTestDefinitionStageReplicateAsync(world.UserId, separateId, stage);
            foreach (var step in steps)
                await stepSvc.CreateTestWorkflowStepAsync(world.UserId, separateId, step);
        }

        int combinedId;
        await using (var combinedDb = _fixture.CreateDbContext())
        {
            combinedDb.CurrentUserId = world.UserId;
            var svc = TestServiceFactory.TestDefinitionMaster(combinedDb);
            var created = await svc.CreateTestWithStepsAsync(
                world.UserId,
                new SaveTestWithStepsRequest(combinedReq, steps, stages));
            combinedId = created.Id;
        }

        await using var assertDb = _fixture.CreateDbContext();
        var sepTest = await assertDb.TestDefinitions.AsNoTracking().SingleAsync(t => t.Id == separateId);
        var cmbTest = await assertDb.TestDefinitions.AsNoTracking().SingleAsync(t => t.Id == combinedId);

        // Code and DisplayName are excluded because unique test Code constraint requires distinct codes on the two rows;
        // Id and Version (xmin) are database-generated identity/concurrency columns.
        var testExclusions = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(TestDefinition.Id),
            nameof(TestDefinition.Version),
            nameof(TestDefinition.Code),
            nameof(TestDefinition.DisplayName)
        };
        AssertMappedPropertiesEqual(assertDb, sepTest, cmbTest, testExclusions);

        var sepStages = await assertDb.TestDefinitionStageReplicates.AsNoTracking()
            .Where(s => s.TestDefinitionId == separateId)
            .OrderBy(s => s.Role)
            .ToListAsync();
        var cmbStages = await assertDb.TestDefinitionStageReplicates.AsNoTracking()
            .Where(s => s.TestDefinitionId == combinedId)
            .OrderBy(s => s.Role)
            .ToListAsync();
        Assert.Equal(sepStages.Count, cmbStages.Count);

        // Id, Version and foreign key TestDefinitionId differ by parent row identity.
        var stageExclusions = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(TestDefinitionStageReplicate.Id),
            nameof(TestDefinitionStageReplicate.Version),
            nameof(TestDefinitionStageReplicate.TestDefinitionId)
        };
        foreach (var (sSep, sCmb) in sepStages.Zip(cmbStages))
            AssertMappedPropertiesEqual(assertDb, sSep, sCmb, stageExclusions);

        var sepSteps = await assertDb.TestWorkflowSteps.AsNoTracking()
            .Include(s => s.StepMedia)
            .Include(s => s.IncubationStages)
            .Include(s => s.PhenotypicTests)
            .Where(s => s.TestDefinitionId == separateId)
            .OrderBy(s => s.StepOrder)
            .ToListAsync();
        var cmbSteps = await assertDb.TestWorkflowSteps.AsNoTracking()
            .Include(s => s.StepMedia)
            .Include(s => s.IncubationStages)
            .Include(s => s.PhenotypicTests)
            .Where(s => s.TestDefinitionId == combinedId)
            .OrderBy(s => s.StepOrder)
            .ToListAsync();
        Assert.Equal(sepSteps.Count, cmbSteps.Count);

        var stepExclusions = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(TestWorkflowStep.Id),
            nameof(TestWorkflowStep.Version),
            nameof(TestWorkflowStep.TestDefinitionId)
        };
        // Entities without IVersionedEntity map PostgreSQL's system `xmin` xid column as an EF shadow concurrency token
        // (distinct per transaction, identical in role to IVersionedEntity.Version above).
        var stepMediaExclusions = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(TestWorkflowStepMedia.Id),
            nameof(TestWorkflowStepMedia.TestWorkflowStepId),
            "xmin"
        };
        var incubationStageExclusions = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(TestWorkflowStepIncubationStage.Id),
            nameof(TestWorkflowStepIncubationStage.TestWorkflowStepId),
            "xmin"
        };
        var phenotypicExclusions = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(TestWorkflowStepPhenotypicTest.Id),
            nameof(TestWorkflowStepPhenotypicTest.TestWorkflowStepId),
            "xmin"
        };

        foreach (var (stepSep, stepCmb) in sepSteps.Zip(cmbSteps))
        {
            AssertMappedPropertiesEqual(assertDb, stepSep, stepCmb, stepExclusions);

            var mediaSep = stepSep.StepMedia.OrderBy(m => m.DisplayOrder).ToList();
            var mediaCmb = stepCmb.StepMedia.OrderBy(m => m.DisplayOrder).ToList();
            Assert.Equal(mediaSep.Count, mediaCmb.Count);
            foreach (var (mSep, mCmb) in mediaSep.Zip(mediaCmb))
                AssertMappedPropertiesEqual(assertDb, mSep, mCmb, stepMediaExclusions);

            var incSep = stepSep.IncubationStages.OrderBy(x => x.StageNumber).ToList();
            var incCmb = stepCmb.IncubationStages.OrderBy(x => x.StageNumber).ToList();
            Assert.Equal(incSep.Count, incCmb.Count);
            foreach (var (iSep, iCmb) in incSep.Zip(incCmb))
                AssertMappedPropertiesEqual(assertDb, iSep, iCmb, incubationStageExclusions);

            var phenoSep = stepSep.PhenotypicTests.OrderBy(p => p.DisplayOrder).ToList();
            var phenoCmb = stepCmb.PhenotypicTests.OrderBy(p => p.DisplayOrder).ToList();
            Assert.Equal(phenoSep.Count, phenoCmb.Count);
            foreach (var (pSep, pCmb) in phenoSep.Zip(phenoCmb))
                AssertMappedPropertiesEqual(assertDb, pSep, pCmb, phenotypicExclusions);
        }
    }

    [PostgresFact]
    public async Task WithSteps_SectionCallerCannotConfigure_Refused_NothingCreated()
    {
        var world = await ArrangeWorldAsync();
        var tag = NewTag();
        var forbiddenCode = "FORB-" + tag;
        var (stages, steps) = BuildValidStagesAndSteps(world, "Forbidden " + tag);

        var request = new SaveTestWithStepsRequest(
            new CreateTestDefinitionRequest(
                Code: forbiddenCode,
                DisplayName: "Forbidden Section Test " + tag,
                SectionId: world.ForbiddenSectionId,
                WorkflowType: WorkflowType.Observation),
            steps,
            stages);

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = world.UserId;
            var svc = TestServiceFactory.TestDefinitionMaster(actDb);
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                svc.CreateTestWithStepsAsync(world.UserId, request));
            Assert.Contains("not a member of the selected laboratory section", ex.Message);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.TestDefinitions.AsNoTracking().AnyAsync(t => t.Code == forbiddenCode));
        Assert.False(await assertDb.TestWorkflowSteps.AsNoTracking().AnyAsync(s => s.StepName.StartsWith("Forbidden " + tag)));
        Assert.False(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a => a.UserId == world.UserId));
    }
}
