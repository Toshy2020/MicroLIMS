using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Responses;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class MasterDataUsagePostgresTests
{
    private readonly PostgresTestFixture _fixture;

    public MasterDataUsagePostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed class CommandCounter : DbCommandInterceptor
    {
        private int _count;
        public int Count => Volatile.Read(ref _count);

        public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
        {
            Interlocked.Increment(ref _count);
            return result;
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return ValueTask.FromResult(result);
        }

        public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
        {
            Interlocked.Increment(ref _count);
            return result;
        }

        public override ValueTask<object?> ScalarExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            object? result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FixedAuthorizationService : IAuthorizationService
    {
        private readonly bool _allowMasterDataManage;
        public FixedAuthorizationService(bool allowMasterDataManage) => _allowMasterDataManage = allowMasterDataManage;

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements) =>
            Task.FromResult(AuthorizationResult.Success());

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user, object? resource, string policyName) =>
            Task.FromResult(policyName == PermissionConstants.MasterDataManage && !_allowMasterDataManage
                ? AuthorizationResult.Failed()
                : AuthorizationResult.Success());
    }

    private static MasterDataUsageService BuildService(MicroLimsDbContext db) =>
        new(db, new UserSectionScopeService(db));

    private static string Tag() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static async Task<(DocumentSection Micro, DocumentSection Fp)> EnsureTwoSectionsAsync(MicroLimsDbContext db)
    {
        var dept = await db.DocumentDepartments.SingleAsync(d => d.Code == "QC");
        var micro = await db.DocumentSections.SingleAsync(s => s.Code == "MICRO");
        var fp = await db.DocumentSections.FirstOrDefaultAsync(s => s.Code == "FP");
        if (fp is null)
        {
            fp = new DocumentSection
            {
                DepartmentId = dept.Id,
                Code = "FP",
                Name = "Physicochemical",
                IsActive = true
            };
            db.DocumentSections.Add(fp);
            await db.SaveChangesAsync();
        }
        return (micro, fp);
    }

    private static async Task<int> CreateScopedUserAsync(MicroLimsDbContext db, int sectionId, RoleType roleType = RoleType.SectionHead)
    {
        var tag = Tag();
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Type == roleType);
        if (role is null)
        {
            role = new Role { Type = roleType, Name = roleType.ToString(), IsSystemRole = true, IsActive = true };
            db.Roles.Add(role);
            await db.SaveChangesAsync();
        }

        var section = await db.DocumentSections.SingleAsync(s => s.Id == sectionId);
        var user = new User
        {
            FullName = "Scoped User " + tag,
            Username = "scoped_" + tag,
            PasswordHash = "hash",
            RoleId = role.Id,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = user.Id,
            DepartmentId = section.DepartmentId,
            SectionId = sectionId
        });
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<CauseOfTesting> EnsureCauseAsync(MicroLimsDbContext db)
    {
        var existing = await db.CausesOfTesting.FirstOrDefaultAsync();
        if (existing is not null) return existing;
        var cause = new CauseOfTesting { Name = "Routine " + Tag(), IsActive = true };
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();
        return cause;
    }

    [PostgresFact]
    public async Task TestDefinition_CountsEachSource()
    {
        string tag = Tag();
        int testDefId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var (micro, _) = await EnsureTwoSectionsAsync(arrangeDb);
            var cause = await EnsureCauseAsync(arrangeDb);

            var td = new TestDefinition
            {
                Code = "TD_" + tag,
                DisplayName = "Test " + tag,
                SectionId = micro.Id,
                WorkflowType = WorkflowType.Observation,
                IsActive = true
            };
            arrangeDb.TestDefinitions.Add(td);

            var item = new Item { Code = "ITM-" + tag, Name = "Item " + tag, Category = SampleCategory.FinishedProduct, IsActive = true };
            arrangeDb.Items.Add(item);

            var dept = new Department { Name = "Dept " + tag, IsActive = true };
            arrangeDb.Departments.Add(dept);

            var machine = new Machine { Name = "Machine " + tag, IsActive = true };
            arrangeDb.Machines.Add(machine);

            var waterDept = new WaterDepartment { Name = "WaterDept " + tag, IsActive = true };
            arrangeDb.WaterDepartments.Add(waterDept);

            await arrangeDb.SaveChangesAsync();

            var room = new Room { Name = "Room " + tag, DepartmentId = dept.Id, GradeClassification = "A", IsActive = true };
            arrangeDb.Rooms.Add(room);

            var part = new MachinePart { Name = "Part " + tag, MachineId = machine.Id, IsActive = true };
            arrangeDb.MachineParts.Add(part);

            var point1 = new WaterSamplingPoint
            {
                Code = "WP1-" + tag,
                Location = "Loc 1",
                WaterDepartmentId = waterDept.Id,
                AssignedTestCodes = new List<string> { td.Code },
                IsActive = true
            };
            var point2 = new WaterSamplingPoint
            {
                Code = "WP2-" + tag,
                Location = "Loc 2",
                WaterDepartmentId = waterDept.Id,
                AssignedTestCodes = new List<string> { td.Code, "OTHER" },
                IsActive = true
            };
            arrangeDb.WaterSamplingPoints.AddRange(point1, point2);

            arrangeDb.Specifications.Add(new Specification
            {
                ItemId = item.Id,
                TestCode = td.Code,
                ParameterName = "Param",
                LimitType = LimitType.Qualitative
            });
            arrangeDb.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = td.Code, DisplayName = td.DisplayName });

            var sample = new Sample
            {
                ItemId = item.Id,
                Category = SampleCategory.FinishedProduct,
                ReferenceNumber = "SMP-" + tag,
                ControlNumber = "CTL-" + tag,
                CauseOfTestingId = cause.Id,
                ReceivedByUserId = _fixture.SeededUserId,
                ReceivedAt = DateTime.UtcNow,
                Status = SampleStatus.InTesting
            };
            arrangeDb.Samples.Add(sample);
            await arrangeDb.SaveChangesAsync();

            arrangeDb.RoomTestConfigurations.Add(new RoomTestConfiguration { RoomId = room.Id, TestType = "SettlePlate", TestCode = td.Code });
            arrangeDb.SamplingConfigurations.Add(new SamplingConfiguration { WaterSamplingPointId = point1.Id, TestCode = td.Code });
            arrangeDb.MachinePartConfigurations.Add(new MachinePartConfiguration { MachinePartId = part.Id, TestType = "Swab", TestCode = td.Code });
            arrangeDb.TestOrders.AddRange(
                new TestOrder { SampleId = sample.Id, TestCode = td.Code, SectionId = micro.Id, Status = ApprovalStatus.Pending },
                new TestOrder { SampleId = sample.Id, TestCode = td.Code, SectionId = micro.Id, Status = ApprovalStatus.Approved });

            await arrangeDb.SaveChangesAsync();
            testDefId = td.Id;
        }

        UsageSummary summary;
        await using (var actDb = _fixture.CreateDbContext())
        {
            summary = await BuildService(actDb).GetAsync(MasterRecordType.TestDefinition, testDefId, _fixture.SeededUserId);
        }

        Assert.Equal(MasterRecordType.TestDefinition, summary.Type);
        Assert.Equal(testDefId, summary.Id);
        Assert.Equal(1, summary.OpenWorkCount);
        Assert.Equal(1, LineCount(summary, "specifications"));
        Assert.Equal(1, LineCount(summary, "sample-tests"));
        Assert.Equal(1, LineCount(summary, "room-test-configs"));
        Assert.Equal(1, LineCount(summary, "sampling-configs"));
        Assert.Equal(1, LineCount(summary, "machine-part-configs"));
        Assert.Equal(2, LineCount(summary, "water-sampling-points"));
        Assert.Equal(1, LineCount(summary, "open-test-orders"));
        Assert.Equal(2, LineCount(summary, "all-test-orders"));
        Assert.True(summary.Lines.Single(l => l.Key == "open-test-orders").IsOpenWork);
    }

    [PostgresFact]
    public async Task Specification_CountsEachSource()
    {
        string tag = Tag();
        int specId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var (micro, _) = await EnsureTwoSectionsAsync(arrangeDb);
            var cause = await EnsureCauseAsync(arrangeDb);

            var td = new TestDefinition { Code = "SPEC_TD_" + tag, DisplayName = "Spec Test", SectionId = micro.Id, IsActive = true };
            var item = new Item { Code = "ITM-" + tag, Name = "Item " + tag, Category = SampleCategory.FinishedProduct, IsActive = true };
            arrangeDb.TestDefinitions.Add(td);
            arrangeDb.Items.Add(item);
            await arrangeDb.SaveChangesAsync();

            var spec = new Specification
            {
                ItemId = item.Id,
                TestCode = td.Code,
                ParameterName = "Dissolution",
                LimitType = LimitType.MultiStage
            };
            arrangeDb.Specifications.Add(spec);
            await arrangeDb.SaveChangesAsync();

            arrangeDb.SpecificationStages.AddRange(
                new SpecificationStage { SpecificationId = spec.Id, StageNumber = 1, StageLabel = "S1", AcceptanceCriteriaText = ">= 80%" },
                new SpecificationStage { SpecificationId = spec.Id, StageNumber = 2, StageLabel = "S2", AcceptanceCriteriaText = ">= 75%" });

            var sample = new Sample
            {
                ItemId = item.Id,
                Category = SampleCategory.FinishedProduct,
                ReferenceNumber = "SMP-" + tag,
                ControlNumber = "CTL-" + tag,
                CauseOfTestingId = cause.Id,
                ReceivedByUserId = _fixture.SeededUserId,
                ReceivedAt = DateTime.UtcNow,
                Status = SampleStatus.InTesting
            };
            arrangeDb.Samples.Add(sample);
            await arrangeDb.SaveChangesAsync();

            var openOrder = new TestOrder { SampleId = sample.Id, TestCode = td.Code, SectionId = micro.Id, Status = ApprovalStatus.InProgress };
            var closedOrder = new TestOrder { SampleId = sample.Id, TestCode = td.Code, SectionId = micro.Id, Status = ApprovalStatus.Approved };
            arrangeDb.TestOrders.AddRange(openOrder, closedOrder);
            await arrangeDb.SaveChangesAsync();

            var sig = new ElectronicSignature
            {
                UserId = _fixture.SeededUserId,
                UserFullNameSnapshot = "Admin",
                UsernameSnapshot = "qa_admin",
                RoleSnapshot = "SystemAdministrator",
                MeaningOfSignature = SignatureMeaning.ResultRecorded,
                EntityType = "TestOrder",
                EntityId = closedOrder.Id,
                SignedAt = DateTime.UtcNow
            };
            arrangeDb.ElectronicSignatures.Add(sig);
            await arrangeDb.SaveChangesAsync();

            var analysis = new TestAnalysis
            {
                TestOrderId = closedOrder.Id,
                AnalysisType = WorkflowType.Dissolution,
                AnalysedAt = DateTime.UtcNow,
                EnteredByUserId = _fixture.SeededUserId,
                EnteredAt = DateTime.UtcNow,
                SignatureId = sig.Id
            };
            arrangeDb.TestAnalyses.Add(analysis);
            await arrangeDb.SaveChangesAsync();

            arrangeDb.ParameterResults.Add(new ParameterResult
            {
                TestOrderId = closedOrder.Id,
                TestAnalysisId = analysis.Id,
                SpecificationId = spec.Id,
                ParameterName = "Dissolution",
                ComparisonStatus = ResultStatus.WithinLimits
            });

            await arrangeDb.SaveChangesAsync();
            specId = spec.Id;
        }

        UsageSummary summary;
        await using (var actDb = _fixture.CreateDbContext())
        {
            summary = await BuildService(actDb).GetAsync(MasterRecordType.Specification, specId, _fixture.SeededUserId);
        }

        Assert.Equal(1, summary.OpenWorkCount);
        Assert.Equal(1, LineCount(summary, "open-test-orders"));
        Assert.Equal(1, LineCount(summary, "parameter-results"));
        Assert.Equal(2, LineCount(summary, "specification-stages"));
    }

    [PostgresFact]
    public async Task Item_CountsEachSource()
    {
        string tag = Tag();
        int itemId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var (micro, fp) = await EnsureTwoSectionsAsync(arrangeDb);
            var cause = await EnsureCauseAsync(arrangeDb);

            var td = new TestDefinition { Code = "ITM_TD_" + tag, DisplayName = "Item Test", SectionId = micro.Id, IsActive = true };
            var item = new Item { Code = "ITM-" + tag, Name = "Item " + tag, Category = SampleCategory.FinishedProduct, IsActive = true };
            var diluent = new SolutionMaster
            {
                SectionId = fp.Id,
                Name = "Diluent " + tag,
                Type = SolutionType.Diluent,
                ShelfLifeValue = 1,
                ShelfLifeUnit = ShelfLifeUnit.Days,
                FinalVolumeMl = 100m,
                CreatedByUserId = _fixture.SeededUserId,
                CreatedAt = DateTime.UtcNow,
                LastModifiedByUserId = _fixture.SeededUserId,
                LastModifiedAt = DateTime.UtcNow
            };
            arrangeDb.TestDefinitions.Add(td);
            arrangeDb.Items.Add(item);
            arrangeDb.SolutionMasters.Add(diluent);
            await arrangeDb.SaveChangesAsync();

            arrangeDb.Specifications.Add(new Specification { ItemId = item.Id, TestCode = td.Code, ParameterName = "P1" });
            arrangeDb.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = td.Code, DisplayName = td.DisplayName });
            arrangeDb.ItemDocuments.Add(new ItemDocument
            {
                ItemId = item.Id,
                DocumentType = ItemDocumentType.Sop,
                OriginalFileName = "stm.pdf",
                StorageKey = "k-" + tag,
                FileExtension = ".pdf",
                ContentType = "application/pdf",
                Version = "1",
                UploadedByUserId = _fixture.SeededUserId,
                UploadedAt = DateTime.UtcNow
            });
            arrangeDb.ItemPreparationConfigurations.Add(new ItemPreparationConfiguration
            {
                ItemId = item.Id,
                Amount = 10m,
                Technique = "PourPlate",
                CreatedByUserId = _fixture.SeededUserId,
                CreatedAt = DateTime.UtcNow
            });
            arrangeDb.HplcMethods.Add(new HplcMethod
            {
                SectionId = fp.Id,
                Name = "Method " + tag,
                Abbreviation = "M" + tag[..4],
                EffectiveDate = DateTime.UtcNow,
                DiluentSolutionId = diluent.Id,
                ProductItemId = item.Id,
                CreatedByUserId = _fixture.SeededUserId,
                CreatedAt = DateTime.UtcNow,
                LastModifiedByUserId = _fixture.SeededUserId,
                LastModifiedAt = DateTime.UtcNow
            });

            var openSample = new Sample
            {
                ItemId = item.Id,
                Category = SampleCategory.FinishedProduct,
                ReferenceNumber = "SMP1-" + tag,
                ControlNumber = "CTL1-" + tag,
                CauseOfTestingId = cause.Id,
                ReceivedByUserId = _fixture.SeededUserId,
                ReceivedAt = DateTime.UtcNow,
                Status = SampleStatus.InTesting
            };
            var closedSample = new Sample
            {
                ItemId = item.Id,
                Category = SampleCategory.FinishedProduct,
                ReferenceNumber = "SMP2-" + tag,
                ControlNumber = "CTL2-" + tag,
                CauseOfTestingId = cause.Id,
                ReceivedByUserId = _fixture.SeededUserId,
                ReceivedAt = DateTime.UtcNow,
                Status = SampleStatus.Approved
            };
            arrangeDb.Samples.AddRange(openSample, closedSample);
            await arrangeDb.SaveChangesAsync();

            arrangeDb.TestOrders.AddRange(
                new TestOrder { SampleId = openSample.Id, TestCode = td.Code, SectionId = micro.Id, Status = ApprovalStatus.Pending },
                new TestOrder { SampleId = closedSample.Id, TestCode = td.Code, SectionId = micro.Id, Status = ApprovalStatus.Approved });
            await arrangeDb.SaveChangesAsync();

            itemId = item.Id;
        }

        UsageSummary summary;
        await using (var actDb = _fixture.CreateDbContext())
        {
            summary = await BuildService(actDb).GetAsync(MasterRecordType.Item, itemId, _fixture.SeededUserId);
        }

        Assert.Equal(1, summary.OpenWorkCount);
        Assert.Equal(1, LineCount(summary, "specifications"));
        Assert.Equal(1, LineCount(summary, "sample-tests"));
        Assert.Equal(1, LineCount(summary, "open-samples"));
        Assert.Equal(2, LineCount(summary, "all-samples"));
        Assert.Equal(1, LineCount(summary, "item-documents"));
        Assert.Equal(1, LineCount(summary, "item-preparations"));
        Assert.Equal(1, LineCount(summary, "hplc-methods"));
    }

    [PostgresFact]
    public async Task Equipment_InSectionICannotRead_Refused()
    {
        string tag = Tag();
        int equipmentId;
        int microUserId;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var (micro, fp) = await EnsureTwoSectionsAsync(arrangeDb);
            var equipment = new Equipment { Code = "EQX-" + tag, Name = "Fp only " + tag, Type = EquipmentType.Hplc, SectionId = fp.Id };
            arrangeDb.Equipment.Add(equipment);
            await arrangeDb.SaveChangesAsync();
            equipmentId = equipment.Id;
            microUserId = await CreateScopedUserAsync(arrangeDb, micro.Id, RoleType.Analyst);
        }

        await using var actDb = _fixture.CreateDbContext();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => BuildService(actDb).GetAsync(MasterRecordType.Equipment, equipmentId, microUserId));
    }

    [PostgresFact]
    public async Task Equipment_CountsEachSource()
    {
        string tag = Tag();
        int equipmentId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var (micro, fp) = await EnsureTwoSectionsAsync(arrangeDb);
            var cause = await EnsureCauseAsync(arrangeDb);

            var equipment = new Equipment
            {
                Code = "EQ-" + tag,
                Name = "Multi Eq " + tag,
                Type = EquipmentType.Hplc,
                SectionId = fp.Id
            };
            arrangeDb.Equipment.Add(equipment);

            var item = new Item { Code = "ITM-" + tag, Name = "Item " + tag, Category = SampleCategory.FinishedProduct, IsActive = true };
            arrangeDb.Items.Add(item);

            var diluent = new SolutionMaster
            {
                SectionId = fp.Id,
                Name = "Diluent " + tag,
                Type = SolutionType.Diluent,
                ShelfLifeValue = 1,
                ShelfLifeUnit = ShelfLifeUnit.Days,
                FinalVolumeMl = 100m,
                CreatedByUserId = _fixture.SeededUserId,
                CreatedAt = DateTime.UtcNow,
                LastModifiedByUserId = _fixture.SeededUserId,
                LastModifiedAt = DateTime.UtcNow
            };
            arrangeDb.SolutionMasters.Add(diluent);
            await arrangeDb.SaveChangesAsync();

            var method = new HplcMethod
            {
                SectionId = fp.Id,
                Name = "Method " + tag,
                Abbreviation = "H" + tag[..4],
                EffectiveDate = DateTime.UtcNow,
                DiluentSolutionId = diluent.Id,
                CreatedByUserId = _fixture.SeededUserId,
                CreatedAt = DateTime.UtcNow,
                LastModifiedByUserId = _fixture.SeededUserId,
                LastModifiedAt = DateTime.UtcNow
            };
            var column = new ChromatographyColumn
            {
                Code = "COL-" + tag,
                Name = "Col " + tag,
                UspDesignation = "L1",
                SectionId = fp.Id,
                IsActive = true,
                CreatedByUserId = _fixture.SeededUserId,
                LastModifiedByUserId = _fixture.SeededUserId
            };
            arrangeDb.HplcMethods.Add(method);
            arrangeDb.ChromatographyColumns.Add(column);

            var sample = new Sample
            {
                ItemId = item.Id,
                Category = SampleCategory.FinishedProduct,
                ReferenceNumber = "SMP-" + tag,
                ControlNumber = "CTL-" + tag,
                CauseOfTestingId = cause.Id,
                ReceivedByUserId = _fixture.SeededUserId,
                ReceivedAt = DateTime.UtcNow,
                Status = SampleStatus.InTesting
            };
            arrangeDb.Samples.Add(sample);
            await arrangeDb.SaveChangesAsync();

            var openOrder = new TestOrder { SampleId = sample.Id, TestCode = "T1", SectionId = micro.Id, Status = ApprovalStatus.InProgress };
            var closedOrder = new TestOrder { SampleId = sample.Id, TestCode = "T2", SectionId = micro.Id, Status = ApprovalStatus.Approved };
            arrangeDb.TestOrders.AddRange(openOrder, closedOrder);
            await arrangeDb.SaveChangesAsync();

            var sig = new ElectronicSignature
            {
                UserId = _fixture.SeededUserId,
                UserFullNameSnapshot = "Admin",
                UsernameSnapshot = "qa_admin",
                RoleSnapshot = "SystemAdministrator",
                MeaningOfSignature = SignatureMeaning.ResultRecorded,
                EntityType = "TestOrder",
                EntityId = openOrder.Id,
                SignedAt = DateTime.UtcNow
            };
            arrangeDb.ElectronicSignatures.Add(sig);
            await arrangeDb.SaveChangesAsync();

            arrangeDb.Incubations.AddRange(
                new Incubation { TestOrderId = openOrder.Id, IncubatorEquipmentId = equipment.Id, StepNumber = 1, StepName = "S1", StartedAt = DateTime.UtcNow },
                new Incubation { TestOrderId = closedOrder.Id, IncubatorEquipmentId = equipment.Id, StepNumber = 1, StepName = "S1", StartedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow });

            arrangeDb.TestAnalyses.AddRange(
                new TestAnalysis { TestOrderId = openOrder.Id, EquipmentId = equipment.Id, AnalysisType = WorkflowType.Measurement, AnalysedAt = DateTime.UtcNow, EnteredByUserId = _fixture.SeededUserId, EnteredAt = DateTime.UtcNow, SignatureId = sig.Id, IsActive = true },
                new TestAnalysis { TestOrderId = closedOrder.Id, EquipmentId = equipment.Id, AnalysisType = WorkflowType.Measurement, AnalysedAt = DateTime.UtcNow, EnteredByUserId = _fixture.SeededUserId, EnteredAt = DateTime.UtcNow, SignatureId = sig.Id, IsActive = true });

            arrangeDb.HplcRuns.AddRange(
                new HplcRun { Code = "HR1-" + tag, SectionId = fp.Id, EquipmentId = equipment.Id, HplcMethodId = method.Id, ChromatographyColumnId = column.Id, AnalystUserId = _fixture.SeededUserId, StartedAt = DateTime.UtcNow, Status = HplcRunStatus.Open },
                new HplcRun { Code = "HR2-" + tag, SectionId = fp.Id, EquipmentId = equipment.Id, HplcMethodId = method.Id, ChromatographyColumnId = column.Id, AnalystUserId = _fixture.SeededUserId, StartedAt = DateTime.UtcNow, Status = HplcRunStatus.Completed });

            arrangeDb.IcpRuns.AddRange(
                new IcpRun { Code = "IR1-" + tag, SectionId = fp.Id, EquipmentId = equipment.Id, AnalystUserId = _fixture.SeededUserId, StartedAt = DateTime.UtcNow, Status = IcpRunStatus.Open },
                new IcpRun { Code = "IR2-" + tag, SectionId = fp.Id, EquipmentId = equipment.Id, AnalystUserId = _fixture.SeededUserId, StartedAt = DateTime.UtcNow, Status = IcpRunStatus.Abandoned });

            arrangeDb.AutoclavePrograms.Add(new AutoclaveProgram
            {
                EquipmentId = equipment.Id,
                ProgramCode = "P01",
                ProgramName = "Media",
                LoadType = "Media",
                Temperature = 121m,
                CycleTimeMinutes = 15,
                CreatedByUserId = _fixture.SeededUserId,
                LastModifiedByUserId = _fixture.SeededUserId
            });

            arrangeDb.InstrumentAgents.Add(new InstrumentAgent
            {
                Code = "AGT-" + tag,
                Name = "Agent " + tag,
                EquipmentId = equipment.Id,
                SectionId = fp.Id,
                TimeZoneId = "UTC",
                KeyHash = "hash",
                KeyPrefix = "pfx",
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = _fixture.SeededUserId
            });

            await arrangeDb.SaveChangesAsync();
            equipmentId = equipment.Id;
        }

        UsageSummary summary;
        await using (var actDb = _fixture.CreateDbContext())
        {
            summary = await BuildService(actDb).GetAsync(MasterRecordType.Equipment, equipmentId, _fixture.SeededUserId);
        }

        Assert.Equal(4, summary.OpenWorkCount);
        Assert.Equal(1, LineCount(summary, "open-incubations"));
        Assert.Equal(2, LineCount(summary, "all-incubations"));
        Assert.Equal(1, LineCount(summary, "open-test-analyses"));
        Assert.Equal(2, LineCount(summary, "all-test-analyses"));
        Assert.Equal(1, LineCount(summary, "open-hplc-runs"));
        Assert.Equal(2, LineCount(summary, "all-hplc-runs"));
        Assert.Equal(1, LineCount(summary, "open-icp-runs"));
        Assert.Equal(2, LineCount(summary, "all-icp-runs"));
        Assert.Equal(1, LineCount(summary, "autoclave-programs"));
        Assert.Equal(1, LineCount(summary, "instrument-agents"));
    }

    [PostgresFact]
    public async Task MaterialMaster_CountsEachSource()
    {
        string tag = Tag();
        int entryId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var (micro, fp) = await EnsureTwoSectionsAsync(arrangeDb);
            var entry = new MaterialMasterEntry
            {
                SectionId = fp.Id,
                Code = "MM-" + tag,
                Name = "Reagent " + tag,
                Category = MaterialMasterCategory.Reagent,
                BaseUnit = MaterialUnit.Gram,
                CreatedByUserId = _fixture.SeededUserId,
                CreatedAt = DateTime.UtcNow,
                LastModifiedByUserId = _fixture.SeededUserId,
                LastModifiedAt = DateTime.UtcNow
            };
            arrangeDb.MaterialMasterEntries.Add(entry);

            var item = new Item { Code = "ITM-" + tag, Name = "Item " + tag, Category = SampleCategory.FinishedProduct, IsActive = true };
            arrangeDb.Items.Add(item);

            var solution = new SolutionMaster
            {
                SectionId = fp.Id,
                Name = "Sol " + tag,
                Type = SolutionType.Reagent,
                ShelfLifeValue = 1,
                ShelfLifeUnit = ShelfLifeUnit.Days,
                FinalVolumeMl = 100m,
                CreatedByUserId = _fixture.SeededUserId,
                CreatedAt = DateTime.UtcNow,
                LastModifiedByUserId = _fixture.SeededUserId,
                LastModifiedAt = DateTime.UtcNow
            };
            arrangeDb.SolutionMasters.Add(solution);
            await arrangeDb.SaveChangesAsync();

            arrangeDb.Materials.AddRange(
                new Material
                {
                    SectionId = fp.Id,
                    MaterialType = MaterialType.Chemical,
                    MaterialName = entry.Name,
                    ManufacturerName = "Mfr",
                    BatchNumber = "B1-" + tag,
                    ReceivingDate = DateTime.UtcNow,
                    Location = "Shelf A",
                    QuantityReceived = 100m,
                    QuantityRemaining = 100m,
                    Unit = MaterialUnit.Gram,
                    MaterialMasterEntryId = entry.Id,
                    CreatedByUserId = _fixture.SeededUserId,
                    LastModifiedByUserId = _fixture.SeededUserId
                },
                new Material
                {
                    SectionId = micro.Id,
                    MaterialType = MaterialType.Chemical,
                    MaterialName = entry.Name,
                    ManufacturerName = "Mfr",
                    BatchNumber = "B2-" + tag,
                    ReceivingDate = DateTime.UtcNow,
                    Location = "Shelf B",
                    QuantityReceived = 50m,
                    QuantityRemaining = 50m,
                    Unit = MaterialUnit.Gram,
                    MaterialMasterEntryId = entry.Id,
                    CreatedByUserId = _fixture.SeededUserId,
                    LastModifiedByUserId = _fixture.SeededUserId
                });

            arrangeDb.SolutionComponents.Add(new SolutionComponent
            {
                SolutionMasterId = solution.Id,
                Order = 1,
                Kind = SolutionComponentKind.Material,
                MaterialMasterEntryId = entry.Id,
                Quantity = 5m,
                Unit = SolutionComponentUnit.Gram
            });

            arrangeDb.ItemPreparationConfigurations.Add(new ItemPreparationConfiguration
            {
                ItemId = item.Id,
                Amount = 10m,
                Technique = "PourPlate",
                DiluentSource = PreparationMaterialSource.MaterialEntry,
                DiluentMaterialMasterEntryId = entry.Id,
                CreatedByUserId = _fixture.SeededUserId,
                CreatedAt = DateTime.UtcNow
            });

            await arrangeDb.SaveChangesAsync();
            entryId = entry.Id;
        }

        UsageSummary summary;
        await using (var actDb = _fixture.CreateDbContext())
        {
            summary = await BuildService(actDb).GetAsync(MasterRecordType.MaterialMaster, entryId, _fixture.SeededUserId);
        }

        Assert.Equal(0, summary.OpenWorkCount);
        Assert.Equal(2, LineCount(summary, "material-lots"));
        Assert.Equal(1, LineCount(summary, "solution-components"));
        Assert.Equal(1, LineCount(summary, "item-preparations"));
    }

    [PostgresFact]
    public async Task OpenVersusHistorical_Separated()
    {
        string tag = Tag();
        int tdId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var (micro, _) = await EnsureTwoSectionsAsync(arrangeDb);
            var cause = await EnsureCauseAsync(arrangeDb);
            var td = new TestDefinition { Code = "OH_" + tag, DisplayName = "OpenHist", SectionId = micro.Id, IsActive = true };
            var item = new Item { Code = "ITM-" + tag, Name = "Item " + tag, Category = SampleCategory.FinishedProduct, IsActive = true };
            arrangeDb.TestDefinitions.Add(td);
            arrangeDb.Items.Add(item);
            await arrangeDb.SaveChangesAsync();

            var sample = new Sample
            {
                ItemId = item.Id,
                Category = SampleCategory.FinishedProduct,
                ReferenceNumber = "SMP-" + tag,
                ControlNumber = "CTL-" + tag,
                CauseOfTestingId = cause.Id,
                ReceivedByUserId = _fixture.SeededUserId,
                ReceivedAt = DateTime.UtcNow,
                Status = SampleStatus.InTesting
            };
            arrangeDb.Samples.Add(sample);
            await arrangeDb.SaveChangesAsync();

            arrangeDb.TestOrders.AddRange(
                new TestOrder { SampleId = sample.Id, TestCode = td.Code, SectionId = micro.Id, Status = ApprovalStatus.Pending },
                new TestOrder { SampleId = sample.Id, TestCode = td.Code, SectionId = micro.Id, Status = ApprovalStatus.InProgress },
                new TestOrder { SampleId = sample.Id, TestCode = td.Code, SectionId = micro.Id, Status = ApprovalStatus.ResultEntered },
                new TestOrder { SampleId = sample.Id, TestCode = td.Code, SectionId = micro.Id, Status = ApprovalStatus.Reviewed },
                new TestOrder { SampleId = sample.Id, TestCode = td.Code, SectionId = micro.Id, Status = ApprovalStatus.Approved },
                new TestOrder { SampleId = sample.Id, TestCode = td.Code, SectionId = micro.Id, Status = ApprovalStatus.Rejected });

            await arrangeDb.SaveChangesAsync();
            tdId = td.Id;
        }

        UsageSummary summary;
        await using (var actDb = _fixture.CreateDbContext())
        {
            summary = await BuildService(actDb).GetAsync(MasterRecordType.TestDefinition, tdId, _fixture.SeededUserId);
        }

        Assert.Equal(4, LineCount(summary, "open-test-orders"));
        Assert.Equal(6, LineCount(summary, "all-test-orders"));
        Assert.Equal(4, summary.OpenWorkCount);
        Assert.Equal(6, summary.HistoricalCount);
    }

    [PostgresFact]
    public async Task SupersededAndCancelledOrders_NotOpen()
    {
        string tag = Tag();
        int tdId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var (micro, _) = await EnsureTwoSectionsAsync(arrangeDb);
            var cause = await EnsureCauseAsync(arrangeDb);
            var td = new TestDefinition { Code = "SC_" + tag, DisplayName = "SupCan", SectionId = micro.Id, IsActive = true };
            var item = new Item { Code = "ITM-" + tag, Name = "Item " + tag, Category = SampleCategory.FinishedProduct, IsActive = true };
            arrangeDb.TestDefinitions.Add(td);
            arrangeDb.Items.Add(item);
            await arrangeDb.SaveChangesAsync();

            var sample = new Sample
            {
                ItemId = item.Id,
                Category = SampleCategory.FinishedProduct,
                ReferenceNumber = "SMP-" + tag,
                ControlNumber = "CTL-" + tag,
                CauseOfTestingId = cause.Id,
                ReceivedByUserId = _fixture.SeededUserId,
                ReceivedAt = DateTime.UtcNow,
                Status = SampleStatus.InTesting
            };
            arrangeDb.Samples.Add(sample);
            await arrangeDb.SaveChangesAsync();

            arrangeDb.TestOrders.AddRange(
                new TestOrder { SampleId = sample.Id, TestCode = td.Code, SectionId = micro.Id, Status = ApprovalStatus.Pending, IsSuperseded = true },
                new TestOrder { SampleId = sample.Id, TestCode = td.Code, SectionId = micro.Id, Status = ApprovalStatus.Cancelled },
                new TestOrder { SampleId = sample.Id, TestCode = td.Code, SectionId = micro.Id, Status = ApprovalStatus.Voided },
                new TestOrder { SampleId = sample.Id, TestCode = td.Code, SectionId = micro.Id, Status = ApprovalStatus.InProgress });

            await arrangeDb.SaveChangesAsync();
            tdId = td.Id;
        }

        UsageSummary summary;
        await using (var actDb = _fixture.CreateDbContext())
        {
            summary = await BuildService(actDb).GetAsync(MasterRecordType.TestDefinition, tdId, _fixture.SeededUserId);
        }

        Assert.Equal(1, LineCount(summary, "open-test-orders"));
        Assert.Equal(4, LineCount(summary, "all-test-orders"));
        Assert.Equal(1, summary.OpenWorkCount);
    }

    [PostgresFact]
    public async Task UsageInOtherSection_CountedOnlyInOtherSectionsLine()
    {
        string tag = Tag();
        int itemId;
        int microUserId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var (micro, fp) = await EnsureTwoSectionsAsync(arrangeDb);
            microUserId = await CreateScopedUserAsync(arrangeDb, micro.Id);
            var cause = await EnsureCauseAsync(arrangeDb);

            var microTest = new TestDefinition { Code = "M_" + tag, DisplayName = "Micro", SectionId = micro.Id, IsActive = true };
            var fpTest = new TestDefinition { Code = "F_" + tag, DisplayName = "FP", SectionId = fp.Id, IsActive = true };
            var item = new Item { Code = "ITM-" + tag, Name = "Item " + tag, Category = SampleCategory.FinishedProduct, IsActive = true };
            arrangeDb.TestDefinitions.AddRange(microTest, fpTest);
            arrangeDb.Items.Add(item);
            await arrangeDb.SaveChangesAsync();

            arrangeDb.Specifications.AddRange(
                new Specification { ItemId = item.Id, TestCode = microTest.Code, ParameterName = "MicroP" },
                new Specification { ItemId = item.Id, TestCode = fpTest.Code, ParameterName = "FpP" });

            var fpOnlySample = new Sample
            {
                ItemId = item.Id,
                Category = SampleCategory.FinishedProduct,
                ReferenceNumber = "SMP-FP-" + tag,
                ControlNumber = "CTL-FP-" + tag,
                CauseOfTestingId = cause.Id,
                ReceivedByUserId = _fixture.SeededUserId,
                ReceivedAt = DateTime.UtcNow,
                Status = SampleStatus.InTesting
            };
            arrangeDb.Samples.Add(fpOnlySample);
            await arrangeDb.SaveChangesAsync();

            arrangeDb.TestOrders.Add(new TestOrder
            {
                SampleId = fpOnlySample.Id,
                TestCode = fpTest.Code,
                SectionId = fp.Id,
                Status = ApprovalStatus.Pending
            });
            await arrangeDb.SaveChangesAsync();
            itemId = item.Id;
        }

        UsageSummary summary;
        await using (var actDb = _fixture.CreateDbContext())
        {
            summary = await BuildService(actDb).GetAsync(MasterRecordType.Item, itemId, microUserId);
        }

        Assert.Equal(1, LineCount(summary, "specifications"));
        Assert.Equal(0, LineCount(summary, "open-samples"));
        Assert.Equal(0, LineCount(summary, "all-samples"));
        Assert.Equal(0, summary.OpenWorkCount);

        var otherLine = summary.Lines.Last();
        Assert.Equal("other-sections", otherLine.Key);
        Assert.Equal("In other sections", otherLine.Label);
        Assert.Equal(2, otherLine.Count); // 1 FP specification + 1 FP-only sample
        Assert.Null(otherLine.ListRoute);
    }

    [PostgresFact]
    public async Task UnknownId_ThrowsNotFoundException_AndControllerPoliciesEnforced()
    {
        int itemId;
        int tdId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var (micro, _) = await EnsureTwoSectionsAsync(arrangeDb);
            var item = new Item { Code = "ITM-" + Tag(), Name = "Item", Category = SampleCategory.FinishedProduct, IsActive = true };
            var td = new TestDefinition { Code = "TD-" + Tag(), DisplayName = "Test", SectionId = micro.Id, IsActive = true };
            arrangeDb.Items.Add(item);
            arrangeDb.TestDefinitions.Add(td);
            await arrangeDb.SaveChangesAsync();
            itemId = item.Id;
            tdId = td.Id;
        }

        await using var actDb = _fixture.CreateDbContext();
        var service = BuildService(actDb);

        foreach (var type in Enum.GetValues<MasterRecordType>())
        {
            await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(type, 999_999_999, _fixture.SeededUserId));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, _fixture.SeededUserId.ToString()) },
            "TestAuth"));

        var deniedController = new MasterDataUsageController(service, new FixedAuthorizationService(allowMasterDataManage: false))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } }
        };

        Assert.IsType<ForbidResult>(await deniedController.Get(MasterRecordType.TestDefinition, tdId));
        Assert.IsType<ForbidResult>(await deniedController.Get(MasterRecordType.Specification, 1));
        Assert.IsType<ForbidResult>(await deniedController.Get(MasterRecordType.MaterialMaster, 1));

        var itemOk = Assert.IsType<OkObjectResult>(await deniedController.Get(MasterRecordType.Item, itemId));
        Assert.IsType<ApiResponse<UsageSummary>>(itemOk.Value);

        var allowedController = new MasterDataUsageController(service, new FixedAuthorizationService(allowMasterDataManage: true))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } }
        };

        var tdOk = Assert.IsType<OkObjectResult>(await allowedController.Get(MasterRecordType.TestDefinition, tdId));
        Assert.IsType<ApiResponse<UsageSummary>>(tdOk.Value);
    }

    [PostgresFact]
    public async Task NoQueryPerRow()
    {
        string tag = Tag();
        int td1Id;
        int td50Id;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var (micro, _) = await EnsureTwoSectionsAsync(arrangeDb);
            var cause = await EnsureCauseAsync(arrangeDb);
            var item = new Item { Code = "ITM-" + tag, Name = "Item " + tag, Category = SampleCategory.FinishedProduct, IsActive = true };
            var td1 = new TestDefinition { Code = "NQ1_" + tag, DisplayName = "One", SectionId = micro.Id, IsActive = true };
            var td50 = new TestDefinition { Code = "NQ50_" + tag, DisplayName = "Fifty", SectionId = micro.Id, IsActive = true };
            arrangeDb.Items.Add(item);
            arrangeDb.TestDefinitions.AddRange(td1, td50);
            await arrangeDb.SaveChangesAsync();

            var sample = new Sample
            {
                ItemId = item.Id,
                Category = SampleCategory.FinishedProduct,
                ReferenceNumber = "SMP-" + tag,
                ControlNumber = "CTL-" + tag,
                CauseOfTestingId = cause.Id,
                ReceivedByUserId = _fixture.SeededUserId,
                ReceivedAt = DateTime.UtcNow,
                Status = SampleStatus.InTesting
            };
            arrangeDb.Samples.Add(sample);
            await arrangeDb.SaveChangesAsync();

            arrangeDb.TestOrders.Add(new TestOrder { SampleId = sample.Id, TestCode = td1.Code, SectionId = micro.Id, Status = ApprovalStatus.Pending });
            for (int i = 0; i < 50; i++)
            {
                arrangeDb.TestOrders.Add(new TestOrder { SampleId = sample.Id, TestCode = td50.Code, SectionId = micro.Id, Status = ApprovalStatus.Pending });
            }

            await arrangeDb.SaveChangesAsync();
            td1Id = td1.Id;
            td50Id = td50.Id;
        }

        var counter1 = new CommandCounter();
        UsageSummary s1;
        await using (var actDb1 = _fixture.CreateDbContext(counter1))
        {
            s1 = await BuildService(actDb1).GetAsync(MasterRecordType.TestDefinition, td1Id, _fixture.SeededUserId);
        }

        var counter50 = new CommandCounter();
        UsageSummary s50;
        await using (var actDb50 = _fixture.CreateDbContext(counter50))
        {
            s50 = await BuildService(actDb50).GetAsync(MasterRecordType.TestDefinition, td50Id, _fixture.SeededUserId);
        }

        Assert.Equal(1, LineCount(s1, "open-test-orders"));
        Assert.Equal(50, LineCount(s50, "open-test-orders"));
        Assert.Equal(counter1.Count, counter50.Count);
        Assert.True(counter1.Count > 0);
    }

    private static int LineCount(UsageSummary summary, string key) =>
        summary.Lines.Single(l => l.Key == key).Count;
}
