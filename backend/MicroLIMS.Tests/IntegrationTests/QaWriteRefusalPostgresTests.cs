using System.Reflection;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Competency;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.ArchitectureTests;
using MicroLIMS.Tests.Fixtures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Xunit;
using World = MicroLIMS.Tests.IntegrationTests.QaScopeTestSeed.World;

namespace MicroLIMS.Tests.IntegrationTests;

/// <summary>
/// Characterisation test for B4 Task 3 write/execution refusal for the QA system role
/// across non-document GxP execution and approval permission codes.
/// Runs each implemented endpoint case independently and collects failures.
/// Uses real PostgreSQL via PostgresTestFixture with separate DbContexts for
/// Arrange, Act, and AsNoTracking Assert phases.
/// </summary>
[Collection("PostgresDatabaseCollection")]
public sealed class QaWriteRefusalPostgresTests
{
    private const string Password = QaScopeTestSeed.Password;
    private readonly PostgresTestFixture _fixture;

    public QaWriteRefusalPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private enum RefusalMechanism
    {
        ServiceScopeCheck,
        ControllerWriteScope,
        EndpointPolicyOnly
    }

    private sealed record WriteEndpointDescriptor(
        string PermissionCode,
        Type ControllerType,
        string ActionName,
        string ServiceDescription,
        RefusalMechanism Mechanism,
        Func<MicroLimsDbContext, World, Task>? AssertRefusalAndNoSideEffects,
        Func<IUserSectionScopeService, World, Task>? AssertControllerWriteScopeRefusal = null);

    [PostgresFact]
    public async Task Qa_is_refused_every_write_endpoint_group()
    {
        var w = await QaScopeTestSeed.SeedWorldAsync(_fixture);

        var expectedCodes = SegregationOfDutiesRules.GxpExecutionAndApprovalCodes
            .Where(c => c != PermissionConstants.DocumentsReview && c != PermissionConstants.DocumentsApprove)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(30, expectedCodes.Count);

        // Oos.Contribute is provisioned in D1 Task 4. Its first endpoint follows in Task 5.
        var endpointCodes = expectedCodes.Except([PermissionConstants.OosContribute]);
        var descriptors = BuildWriteDescriptors();
        Assert.Equal(endpointCodes, descriptors.Select(d => d.PermissionCode).OrderBy(c => c, StringComparer.Ordinal));

        // 1) Fresh DB check: the QA role holds none of the GxP execution/approval codes
        await using (var assertDb = _fixture.CreateDbContext())
        {
            var qaGrantedCodes = await assertDb.RolePermissions.AsNoTracking()
                .Where(rp => rp.RoleId == w.QaRoleId)
                .Select(rp => rp.Permission!.Code)
                .ToListAsync();

            foreach (var code in expectedCodes)
                Assert.DoesNotContain(code, qaGrantedCodes);
        }

        // 2) Run the per-code checks for implemented endpoints and collect every failure.
        var failures = new List<string>();
        foreach (var d in descriptors)
        {
            try
            {
                var action = d.ControllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .FirstOrDefault(m => m.Name == d.ActionName);
                Assert.NotNull(action);

                var authPolicies = d.ControllerType.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                    .Concat(action!.GetCustomAttributes<AuthorizeAttribute>())
                    .Select(a => a.Policy)
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToList();
                Assert.Contains(d.PermissionCode, authPolicies);

                var allowedRoles = AuthorizationMatrixTests.AllowedRoles(d.ControllerType, action);
                Assert.NotNull(allowedRoles);
                Assert.DoesNotContain(RoleType.Qa, allowedRoles);

                switch (d.Mechanism)
                {
                    case RefusalMechanism.ServiceScopeCheck:
                        Assert.NotNull(d.AssertRefusalAndNoSideEffects);
                        await using (var actDb = _fixture.CreateDbContext())
                        {
                            await d.AssertRefusalAndNoSideEffects!(actDb, w);
                        }
                        break;

                    case RefusalMechanism.ControllerWriteScope:
                        Assert.NotNull(d.AssertControllerWriteScopeRefusal);
                        await using (var actDb = _fixture.CreateDbContext())
                        {
                            IUserSectionScopeService scope = new UserSectionScopeService(actDb);
                            await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() => d.AssertControllerWriteScopeRefusal!(scope, w));
                        }
                        break;

                    case RefusalMechanism.EndpointPolicyOnly:
                        // Refused by endpoint policy only: verified above via (a) QA role lacks the permission
                        // in RolePermissions and (b) AuthorizationMatrixTests.AllowedRoles excludes RoleType.Qa.
                        break;

                    default:
                        throw new InvalidOperationException($"Unhandled refusal mechanism {d.Mechanism}.");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"[{d.PermissionCode}] {d.ControllerType.Name}.{d.ActionName} ({d.ServiceDescription}): {ex.GetType().Name}: {ex.Message}");
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} write-refusal case(s) failed:\n" + string.Join("\n", failures));
    }

    private IReadOnlyList<WriteEndpointDescriptor> BuildWriteDescriptors() => new List<WriteEndpointDescriptor>
    {
        // --- Service-enforced refusals ---
        new(
            PermissionConstants.CompetencyOverride,
            typeof(CompetencyController),
            nameof(CompetencyController.GrantOverride),
            "CompetencyOverrideService.GrantAsync",
            RefusalMechanism.ServiceScopeCheck,
            async (actDb, w) =>
            {
                var beforeAuditId = await MaxAuditLogIdAsync();
                var svc = new CompetencyOverrideService(actDb, new PermissionService(actDb),
                    new UserSectionScopeService(actDb), LabClock.Default, _fixture.CreateSequenceHelper(actDb),
                    new ElectronicSignatureService(actDb), TestServiceFactory.Competency(actDb));
                var denied = await Assert.ThrowsAsync<CodedAccessDeniedException>(() =>
                    svc.GrantAsync(w.OrderAId, w.AnalystAId, "QA override", Password, w.QaUserId));
                Assert.Equal("Competency.OverridePermissionRequired", denied.Code);
                await using var verify = _fixture.CreateDbContext();
                Assert.False(await verify.CompetencyOverrides.AsNoTracking().AnyAsync(o => o.TestOrderId == w.OrderAId));
                Assert.False(await verify.ElectronicSignatures.AsNoTracking().AnyAsync(s => s.UserId == w.QaUserId && s.EntityType == "CompetencyOverride"));
                await AssertNoNewAuditLogAsync(w.QaUserId, beforeAuditId);
            }),

        new(
            PermissionConstants.CompetencyManage,
            typeof(CompetencyController),
            nameof(CompetencyController.Qualify),
            "QualificationService.QualifyAsync",
            RefusalMechanism.ServiceScopeCheck,
            async (actDb, w) =>
            {
                var beforeAuditId = await MaxAuditLogIdAsync();
                var svc = new QualificationService(actDb, new PermissionService(actDb),
                    new UserSectionScopeService(actDb), LabClock.Default, _fixture.CreateSequenceHelper(actDb),
                    new ElectronicSignatureService(actDb), new InMemoryFileStorageService());
                await Assert.ThrowsAsync<CodedAccessDeniedException>(() =>
                    svc.QualifyAsync(new QualifyRequest(w.AnalystAId, 0, QualificationBasis.ObservedPractice,
                        "Observed the test", null, LabClock.Default.LabToday, Password), w.QaUserId));
                await AssertNoNewAuditLogAsync(w.QaUserId, beforeAuditId);
            }),

        new(
            PermissionConstants.SamplesReview,
            typeof(SampleReviewController),
            nameof(SampleReviewController.Complete),
            "SampleReviewService.CompleteReviewAsync",
            RefusalMechanism.ServiceScopeCheck,
            async (actDb, w) =>
            {
                var beforeAuditId = await MaxAuditLogIdAsync();
                var svc = TestServiceFactory.SampleReview(actDb);
                await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() =>
                    svc.CompleteReviewAsync(w.ReviewSampleAId, w.QaUserId, Password, "QA review", null));
                await AssertSampleUnchangedAndNoAuditAsync(w.ReviewSampleAId, SampleStatus.UnderReview, w.QaUserId, beforeAuditId);
            }),

        new(
            PermissionConstants.SamplesApprove,
            typeof(SampleApprovalController),
            nameof(SampleApprovalController.Decide),
            "SampleApprovalService.DecideAsync",
            RefusalMechanism.ServiceScopeCheck,
            async (actDb, w) =>
            {
                var beforeAuditId = await MaxAuditLogIdAsync();
                var svc = TestServiceFactory.SampleApproval(actDb);
                await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() =>
                    svc.DecideAsync(w.ApprovalSampleAId, w.QaUserId, Password, ApprovalDecision.Approve, "QA approve", null));
                await AssertSampleUnchangedAndNoAuditAsync(w.ApprovalSampleAId, SampleStatus.UnderApproval, w.QaUserId, beforeAuditId);
            }),

        new(
            PermissionConstants.SamplesReceiveOwnLab,
            typeof(SampleController),
            nameof(SampleController.Receive),
            "ReceiptLabService.ResolveTargetsAsync",
            RefusalMechanism.ServiceScopeCheck,
            async (actDb, w) =>
            {
                var beforeAuditId = await MaxAuditLogIdAsync();
                var svc = new ReceiptLabService(actDb, new UserSectionScopeService(actDb));
                await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() =>
                    svc.ResolveTargetsAsync(w.QaUserId, canReceiveForAnyLab: false, new[] { w.SectionAId }));
                await AssertNoNewAuditLogAsync(w.QaUserId, beforeAuditId);
            }),

        new(
            PermissionConstants.SamplesAssignAnalyst,
            typeof(SampleController),
            nameof(SampleController.AssignAnalyst),
            "SampleAssignmentService.AssignAnalystAsync",
            RefusalMechanism.ServiceScopeCheck,
            async (actDb, w) =>
            {
                var beforeAuditId = await MaxAuditLogIdAsync();
                var scope = new UserSectionScopeService(actDb);
                var svc = new SampleAssignmentService(actDb, TestServiceFactory.TestingWorkspace(actDb, scope), scope);
                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    svc.AssignAnalystAsync(w.SampleAId, w.AnalystAId, w.QaUserId, "QA assign"));
                Assert.Contains("no open tests you can assign", ex.Message);

                await using (var assertDb = _fixture.CreateDbContext())
                {
                    var order = await assertDb.TestOrders.AsNoTracking().SingleAsync(o => o.Id == w.OrderAId);
                    Assert.Null(order.AssignedAnalystId);
                }
                await AssertNoNewAuditLogAsync(w.QaUserId, beforeAuditId);

                // Contrast: Section A member succeeds with the same call.
                await using var contrastDb = _fixture.CreateDbContext();
                var contrastScope = new UserSectionScopeService(contrastDb);
                var contrastSvc = new SampleAssignmentService(contrastDb, TestServiceFactory.TestingWorkspace(contrastDb, contrastScope), contrastScope);
                await contrastSvc.AssignAnalystAsync(w.SampleAId, w.AnalystAId, w.AnalystA2Id, "Auth assign");
            }),

        new(
            PermissionConstants.TestWorkflowExecute,
            typeof(TestWorkflowController),
            nameof(TestWorkflowController.RecordUvResult),
            "TestWorkflowEngine.RecordUvResultAsync",
            RefusalMechanism.ServiceScopeCheck,
            async (actDb, w) =>
            {
                var beforeAuditId = await MaxAuditLogIdAsync();
                var engine = TestServiceFactory.TestWorkflow(actDb);
                var validParameters = new List<UvParameterInput>
                {
                    new(w.UvSpecAId, 254m, null, new List<UvReplicateInput> { new(null, 0.25m) })
                };
                await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() =>
                    engine.RecordUvResultAsync(w.UvOrderAId, new UvPayload(DateTime.UtcNow, w.UvEquipmentAId, null, null, null, validParameters, Password), w.QaUserId));
                await using var assertDb = _fixture.CreateDbContext();
                var order = await assertDb.TestOrders.AsNoTracking().SingleAsync(o => o.Id == w.UvOrderAId);
                Assert.Equal(WorkflowStep.Waiting, order.CurrentStep);
                await AssertNoNewAuditLogAsync(w.QaUserId, beforeAuditId);
            }),

        new(
            PermissionConstants.HplcOperate,
            typeof(HplcWorkspaceController),
            nameof(HplcWorkspaceController.Abandon),
            "HplcRunService.AbandonRunAsync",
            RefusalMechanism.ServiceScopeCheck,
            async (actDb, w) =>
            {
                var beforeAuditId = await MaxAuditLogIdAsync();
                var svc = TestServiceFactory.HplcRun(actDb);
                await Assert.ThrowsAsync<NotFoundException>(() =>
                    svc.AbandonRunAsync(w.HplcRunAId, "QA abandon", w.QaUserId));
                await using (var assertDb = _fixture.CreateDbContext())
                {
                    var run = await assertDb.HplcRuns.AsNoTracking().SingleAsync(r => r.Id == w.HplcRunAId);
                    Assert.Equal(HplcRunStatus.Open, run.Status);
                }
                await AssertNoNewAuditLogAsync(w.QaUserId, beforeAuditId);

                // Contrast: Section A member succeeds with the same call.
                await using var contrastDb = _fixture.CreateDbContext();
                var contrastSvc = TestServiceFactory.HplcRun(contrastDb);
                var abandoned = await contrastSvc.AbandonRunAsync(w.HplcRunAId, "Auth abandon", w.AnalystAId);
                Assert.Equal(HplcRunStatus.Abandoned, abandoned.Status);
            }),

        new(
            PermissionConstants.SolutionsPrepare,
            typeof(SolutionPreparationController),
            nameof(SolutionPreparationController.Start),
            "SolutionPreparationService.StartAsync",
            RefusalMechanism.ServiceScopeCheck,
            async (actDb, w) =>
            {
                var beforeAuditId = await MaxAuditLogIdAsync();
                var beforeCount = await CountSolutionPrepsAsync(w.SolutionMasterAId);
                var svc = TestServiceFactory.SolutionPreparation(actDb);
                var req = new StartPreparationRequest(w.SolutionMasterAId, w.HplcMethodAId);
                await Assert.ThrowsAsync<NotFoundException>(() =>
                    svc.StartAsync(req, w.QaUserId));
                await using (var assertDb = _fixture.CreateDbContext())
                {
                    Assert.Equal(beforeCount, await assertDb.SolutionPreparations.AsNoTracking().CountAsync(p => p.SolutionMasterId == w.SolutionMasterAId));
                }
                await AssertNoNewAuditLogAsync(w.QaUserId, beforeAuditId);

                // Contrast: Section A member succeeds with the same payload.
                await using var contrastDb = _fixture.CreateDbContext();
                var contrastSvc = TestServiceFactory.SolutionPreparation(contrastDb);
                var started = await contrastSvc.StartAsync(req, w.AnalystAId);
                Assert.Equal(SolutionPreparationStatus.InProgress, started.Status);
            }),

        new(
            PermissionConstants.SolutionsRelease,
            typeof(SolutionPreparationController),
            nameof(SolutionPreparationController.Release),
            "SolutionPreparationReleaseService.DecideAsync",
            RefusalMechanism.ServiceScopeCheck,
            async (actDb, w) =>
            {
                var beforeAuditId = await MaxAuditLogIdAsync();
                var svc = TestServiceFactory.SolutionPreparationRelease(actDb);
                var req = new ReleaseSolutionPreparationRequest(true, "QA release", Password);
                await Assert.ThrowsAsync<NotFoundException>(() =>
                    svc.DecideAsync(w.SolutionPrepAId, req, w.QaUserId, null));
                await using (var assertDb = _fixture.CreateDbContext())
                {
                    var prep = await assertDb.SolutionPreparations.AsNoTracking().SingleAsync(p => p.Id == w.SolutionPrepAId);
                    Assert.Equal(ApprovalGateStatus.PendingReview, prep.ApprovalStatus);
                }
                await AssertNoNewAuditLogAsync(w.QaUserId, beforeAuditId);

                // Contrast: non-preparer Section A member succeeds with the same payload.
                await using var contrastDb = _fixture.CreateDbContext();
                var contrastSvc = TestServiceFactory.SolutionPreparationRelease(contrastDb);
                await contrastSvc.DecideAsync(w.SolutionPrepAId, req, w.AnalystA2Id, null);
            }),

        new(
            PermissionConstants.WorkingStandardsQualify,
            typeof(WorkingStandardsController),
            nameof(WorkingStandardsController.UpdateDraft),
            "WorkingStandardService.UpdateDraftAsync",
            RefusalMechanism.ServiceScopeCheck,
            async (actDb, w) =>
            {
                var beforeAuditId = await MaxAuditLogIdAsync();
                var svc = TestServiceFactory.WorkingStandard(actDb);
                var req = new UpdateQualificationRequest(1.0m, "Shelf A", 0.5m);
                await Assert.ThrowsAsync<NotFoundException>(() =>
                    svc.UpdateDraftAsync(w.WorkingStandardQualificationAId, req, w.QaUserId));
                await using (var assertDb = _fixture.CreateDbContext())
                {
                    var ws = await assertDb.WorkingStandardQualifications.AsNoTracking().SingleAsync(x => x.Id == w.WorkingStandardQualificationAId);
                    Assert.Equal(WorkingStandardQualificationStatus.Draft, ws.Status);
                }
                await AssertNoNewAuditLogAsync(w.QaUserId, beforeAuditId);

                // Contrast: Section A member succeeds with the same payload.
                await using var contrastDb = _fixture.CreateDbContext();
                var contrastSvc = TestServiceFactory.WorkingStandard(contrastDb);
                var updated = await contrastSvc.UpdateDraftAsync(w.WorkingStandardQualificationAId, req, w.AnalystAId);
                Assert.Equal(1.0m, updated.QuantityGrams);
            }),

        new(
            PermissionConstants.MediaPrepare,
            typeof(MediaController),
            nameof(MediaController.Prepare),
            "MediaPreparationService.PrepareAsync",
            RefusalMechanism.ServiceScopeCheck,
            async (actDb, w) =>
            {
                var beforeAuditId = await MaxAuditLogIdAsync();
                var beforeMediaCount = await CountMediaAsync(w.DehydratedMaterialAId);
                var svc = TestServiceFactory.MediaPreparation(actDb);
                await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() =>
                    svc.PrepareAsync(new PrepareMediaRequest(
                        w.DehydratedMaterialAId, 10m, "100 ml", w.AutoclaveEquipmentAId, "Liquid", "Agar", 121m, 15, 1, 7.2m,
                        DateTime.UtcNow.AddDays(30), w.QaUserId)));
                await using var assertDb = _fixture.CreateDbContext();
                Assert.Equal(beforeMediaCount, await assertDb.Media.AsNoTracking().CountAsync(m => m.MaterialId == w.DehydratedMaterialAId));
                await AssertNoNewAuditLogAsync(w.QaUserId, beforeAuditId);
            }),

        new(
            PermissionConstants.MaterialsRelease,
            typeof(MaterialReleaseController),
            nameof(MaterialReleaseController.Decide),
            "MaterialReleaseService.DecideAsync",
            RefusalMechanism.ServiceScopeCheck,
            async (actDb, w) =>
            {
                var beforeAuditId = await MaxAuditLogIdAsync();
                var svc = TestServiceFactory.MaterialRelease(actDb);
                await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() =>
                    svc.DecideAsync(w.RefMaterialAId, w.QaUserId, Password, true, "QA material release", null));
                await using var assertDb = _fixture.CreateDbContext();
                var mat = await assertDb.Materials.AsNoTracking().SingleAsync(m => m.Id == w.RefMaterialAId);
                Assert.Equal(ApprovalGateStatus.PendingReview, mat.ApprovalStatus);
                await AssertNoNewAuditLogAsync(w.QaUserId, beforeAuditId);
            }),

        new(
            PermissionConstants.CryovialsManage,
            typeof(CryovialController),
            nameof(CryovialController.PrepareCryovials),
            "CryovialService.PrepareCryovialsAsync",
            RefusalMechanism.ServiceScopeCheck,
            async (actDb, w) =>
            {
                var beforeAuditId = await MaxAuditLogIdAsync();
                var beforeCryoCount = await CountCryovialsAsync(w.MaterialAId);
                var svc = TestServiceFactory.Cryovial(actDb);
                var panel = new List<IdentityConfirmationRow>
                {
                    new(w.MediaAId, w.EquipmentAId, DateTime.UtcNow.AddHours(-24), DateTime.UtcNow, "Growth confirmed")
                };
                await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() =>
                    svc.PrepareCryovialsAsync(new PrepareCryovialsRequest(
                        w.MaterialAId, 3, DateTime.UtcNow.AddDays(30), "-80C", true, "OK",
                        panel, 1, w.QaUserId)));
                await using var assertDb = _fixture.CreateDbContext();
                Assert.Equal(beforeCryoCount, await assertDb.Cryovials.AsNoTracking().CountAsync(c => c.MaterialId == w.MaterialAId));
                await AssertNoNewAuditLogAsync(w.QaUserId, beforeAuditId);
            }),

        new(
            PermissionConstants.TestsOutsource,
            typeof(TestWorkflowController),
            nameof(TestWorkflowController.Outsource),
            "TestWorkflowEngine.OutsourceTestAsync",
            RefusalMechanism.ServiceScopeCheck,
            async (actDb, w) =>
            {
                var beforeAuditId = await MaxAuditLogIdAsync();
                var engine = TestServiceFactory.TestWorkflow(actDb);
                await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() =>
                    engine.OutsourceTestAsync(w.UvOrderAId, new OutsourceTestPayload("External Lab", "Note", Password), w.QaUserId));
                await using var assertDb = _fixture.CreateDbContext();
                var order = await assertDb.TestOrders.AsNoTracking().SingleAsync(o => o.Id == w.UvOrderAId);
                Assert.Null(order.OutsourcedToLab);
                await AssertNoNewAuditLogAsync(w.QaUserId, beforeAuditId);
            }),

        new(
            PermissionConstants.TestsSelfAssign,
            typeof(TestSelfAssignmentController),
            nameof(TestSelfAssignmentController.Take),
            "TestSelfAssignmentService.TakeAsync",
            RefusalMechanism.ServiceScopeCheck,
            async (actDb, w) =>
            {
                var beforeAuditId = await MaxAuditLogIdAsync();
                var service = new TestSelfAssignmentService(actDb, new UserSectionScopeService(actDb));
                await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() => service.TakeAsync(w.OrderAId, w.QaUserId));
                await using var assertDb = _fixture.CreateDbContext();
                var order = await assertDb.TestOrders.AsNoTracking().SingleAsync(o => o.Id == w.OrderAId);
                Assert.NotEqual(w.QaUserId, order.AssignedAnalystId);
                await AssertNoNewAuditLogAsync(w.QaUserId, beforeAuditId);
            }),

        // --- Controller policy + controller section write scope (7 codes, no write-scope check inside service) ---
        new(
            PermissionConstants.SamplesCorrect,
            typeof(SampleController),
            nameof(SampleController.Void),
            "SampleCorrectionService.VoidAsync",
            RefusalMechanism.ControllerWriteScope,
            null,
            (s, w) => s.EnsureSampleAccessAsync(w.QaUserId, w.SampleAId, CancellationToken.None, SectionAccess.Write)),

        new(
            PermissionConstants.TestWorkflowBiochemicalDecision,
            typeof(TestWorkflowController),
            nameof(TestWorkflowController.RecordBiochemicalDecision),
            "TestWorkflowEngine.RecordBiochemicalReviewDecisionAsync",
            RefusalMechanism.ControllerWriteScope,
            null,
            (s, w) => s.EnsureTestOrderAccessAsync(w.QaUserId, w.OrderAId, CancellationToken.None, SectionAccess.Write)),

        new(
            PermissionConstants.TestWorkflowSupervise,
            typeof(TestWorkflowController),
            nameof(TestWorkflowController.OverrideMinimumDuration),
            "TestWorkflowEngine.OverrideMinimumDurationAsync",
            RefusalMechanism.ControllerWriteScope,
            null,
            (s, w) => s.EnsureTestOrderAccessAsync(w.QaUserId, w.OrderAId, CancellationToken.None, SectionAccess.Write)),

        new(
            PermissionConstants.MediaRelease,
            typeof(MediaController),
            nameof(MediaController.DecideRelease),
            "MediaReleaseService.DecideAsync",
            RefusalMechanism.ControllerWriteScope,
            null,
            (s, w) => s.EnsureMediaAccessAsync(w.QaUserId, w.MediaAId, CancellationToken.None, SectionAccess.Write)),

        new(
            PermissionConstants.CryovialsApprove,
            typeof(CryovialController),
            nameof(CryovialController.Approve),
            "CryovialService.ApproveAsync",
            RefusalMechanism.ControllerWriteScope,
            null,
            (s, w) => s.EnsureCryovialAccessAsync(w.QaUserId, w.CryovialAId, CancellationToken.None, SectionAccess.Write)),

        new(
            PermissionConstants.OosManage,
            typeof(OosInvestigationDocumentController),
            nameof(OosInvestigationDocumentController.Upload),
            "OosInvestigationDocumentService.UploadAsync",
            RefusalMechanism.ControllerWriteScope,
            null,
            (s, w) => s.EnsureOosGroupAccessAsync(w.QaUserId, w.OosGroupA, CancellationToken.None, SectionAccess.Write)),

        new(
            PermissionConstants.SamplesPrepare,
            typeof(SamplePreparationController),
            nameof(SamplePreparationController.Prepare),
            "SamplePreparationService.PrepareAsync",
            RefusalMechanism.ControllerWriteScope,
            null,
            (s, w) => s.EnsureSampleAccessAsync(w.QaUserId, w.SampleAId, CancellationToken.None, SectionAccess.Write)),

        // --- Refused by endpoint policy only (6 codes, no section-scope check in controller or service) ---
        new(
            PermissionConstants.SamplesReceive,
            typeof(SampleController),
            nameof(SampleController.AddLaboratory),
            "AddLaboratoryService.AddAsync",
            RefusalMechanism.EndpointPolicyOnly,
            null),

        new(
            PermissionConstants.MonitoringPlanApprove,
            typeof(MonitoringPlanController),
            nameof(MonitoringPlanController.ApproveRevision),
            "MonitoringPlanService.ApproveAsync",
            RefusalMechanism.EndpointPolicyOnly,
            null),

        new(
            PermissionConstants.EquipmentPlanApprove,
            typeof(EquipmentPlanRevisionsController),
            nameof(EquipmentPlanRevisionsController.ApproveRevision),
            "EquipmentPlanService.ApproveAsync",
            RefusalMechanism.EndpointPolicyOnly,
            null),

        new(
            PermissionConstants.EquipmentWorkRecord,
            typeof(EquipmentWorkController),
            nameof(EquipmentWorkController.AddLogEntry),
            "EquipmentWorkService.AddLogEntryAsync",
            RefusalMechanism.EndpointPolicyOnly,
            null),

        new(
            PermissionConstants.NotApplicableTestsManage,
            typeof(NotApplicableTestController),
            nameof(NotApplicableTestController.Create),
            "NotApplicableTestService.CreateAsync",
            RefusalMechanism.EndpointPolicyOnly,
            null),

        new(
            PermissionConstants.NotApplicableTestsFinalApprove,
            typeof(NotApplicableTestController),
            nameof(NotApplicableTestController.FinalApprove),
            "NotApplicableTestService.FinalApproveAsync",
            RefusalMechanism.EndpointPolicyOnly,
            null),
    };

    private async Task<long> MaxAuditLogIdAsync()
    {
        await using var db = _fixture.CreateDbContext();
        return await db.AuditLogs.AsNoTracking().MaxAsync(a => (long?)a.Id) ?? 0L;
    }

    private async Task<int> CountSolutionPrepsAsync(int masterId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.SolutionPreparations.AsNoTracking().CountAsync(p => p.SolutionMasterId == masterId);
    }

    private async Task<int> CountMediaAsync(int materialId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Media.AsNoTracking().CountAsync(m => m.MaterialId == materialId);
    }

    private async Task<int> CountCryovialsAsync(int materialId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Cryovials.AsNoTracking().CountAsync(c => c.MaterialId == materialId);
    }

    private async Task AssertNoNewAuditLogAsync(int qaUserId, long beforeMaxAuditId)
    {
        await using var assertDb = _fixture.CreateDbContext();
        var newRows = await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.Id > beforeMaxAuditId && a.UserId == qaUserId)
            .CountAsync();
        Assert.Equal(0, newRows);
    }

    private async Task AssertSampleUnchangedAndNoAuditAsync(int sampleId, SampleStatus expectedStatus, int qaUserId, long beforeMaxAuditId)
    {
        await using var assertDb = _fixture.CreateDbContext();
        var sample = await assertDb.Samples.AsNoTracking().SingleAsync(s => s.Id == sampleId);
        Assert.Equal(expectedStatus, sample.Status);
        await AssertNoNewAuditLogAsync(qaUserId, beforeMaxAuditId);
    }
}
