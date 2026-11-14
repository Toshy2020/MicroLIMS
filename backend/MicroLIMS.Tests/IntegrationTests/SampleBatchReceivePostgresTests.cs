using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Validators;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class SampleBatchReceivePostgresTests
{
    private readonly PostgresTestFixture _fixture;

    public SampleBatchReceivePostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed class FixedClock(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class DeactivatingReceivingDecorator : IReceivingService
    {
        private readonly IReceivingService _inner;
        private readonly Func<Task> _beforeNthCall;
        private readonly int _targetCallIndex;
        private int _calls;

        public DeactivatingReceivingDecorator(IReceivingService inner, int targetCallIndex, Func<Task> beforeNthCall)
        {
            _inner = inner;
            _targetCallIndex = targetCallIndex;
            _beforeNthCall = beforeNthCall;
        }

        public async Task<SampleDto> ReceiveSampleAsync(ItemBasedReceiveRequest request)
        {
            var current = _calls++;
            if (current == _targetCallIndex)
            {
                await _beforeNthCall();
            }

            return await _inner.ReceiveSampleAsync(request);
        }
    }

    private static SampleBatchReceiveService BuildService(
        MicroLimsDbContext db,
        TimeProvider? clock = null,
        IReceivingService? receivingOverride = null)
    {
        var refGen = new ReferenceNumberGenerator(db, clock);
        var scope = new UserSectionScopeService(db);
        var receiving = receivingOverride ?? new ReceivingService(new ProductWorkflowEngine(db, refGen, clock), new ReceiveSampleValidator());
        var water = new WaterService(new WaterWorkflowEngine(db, refGen, scope), clock);
        var em = new EMService(new EMWorkflowEngine(db, refGen, scope), clock);
        var ac = new AfterCleaningService(new AfterCleaningWorkflowEngine(db, refGen, scope), clock);
        return new SampleBatchReceiveService(
            db,
            receiving,
            new ReceiveSampleValidator(),
            new ReceiptLabService(db, scope),
            water,
            em,
            ac,
            scope);
    }

    private async Task<(int UserId, int MicroSectionId, int OtherSectionId, int CauseId, int ItemId, int SecondItemId, int WaterDeptId)> SeedProductAndWaterAsync(
        string suffix)
    {
        await using var db = _fixture.CreateDbContext();

        var micro = await db.DocumentSections.FirstAsync(s => s.Code == LaboratoryCodes.Microbiology);
        var other = await db.DocumentSections.FirstOrDefaultAsync(s => s.Id != micro.Id && s.IsActive);
        if (other is null)
        {
            other = new DocumentSection { Code = $"SEC-{suffix}", Name = $"Other {suffix}", IsActive = true };
            db.DocumentSections.Add(other);
            await db.SaveChangesAsync();
        }

        var role = await db.Roles.FirstAsync(r => r.Name == "Analyst");
        var user = new User
        {
            Username = $"batch_analyst_{suffix}",
            PasswordHash = "x",
            FullName = $"Batch Analyst {suffix}",
            Email = $"batch_{suffix}@test.local",
            RoleId = role.Id,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, DepartmentId = micro.DepartmentId, SectionId = micro.Id });

        var cause = new CauseOfTesting { Name = $"Routine {suffix}", IsActive = true };
        db.CausesOfTesting.Add(cause);

        var stageName = $"Bulk-{suffix}";
        if (!await db.ProductionStages.AnyAsync(p => p.Name == stageName))
        {
            db.ProductionStages.Add(new ProductionStage { Name = stageName, IsActive = true });
        }

        var testCode1 = $"BATCH-T1-{suffix}";
        var testCode2 = $"BATCH-T2-{suffix}";
        db.TestDefinitions.AddRange(
            new TestDefinition { Code = testCode1, DisplayName = $"Test 1 {suffix}", SectionId = micro.Id, IsActive = true },
            new TestDefinition { Code = testCode2, DisplayName = $"Test 2 {suffix}", SectionId = other.Id, IsActive = true });

        var item1 = new Item
        {
            Code = $"ITM1-{suffix}",
            Name = $"Product 1 {suffix}",
            Category = SampleCategory.FinishedProduct,
            IsActive = true
        };
        item1.AssignedTests.Add(new SampleTest { TestCode = testCode1, DisplayName = $"Test 1 {suffix}" });

        var item2 = new Item
        {
            Code = $"ITM2-{suffix}",
            Name = $"Product 2 {suffix}",
            Category = SampleCategory.FinishedProduct,
            IsActive = true
        };
        item2.AssignedTests.Add(new SampleTest { TestCode = testCode1, DisplayName = $"Test 1 {suffix}" });

        db.Items.AddRange(item1, item2);

        var waterDept = new WaterDepartment { Name = $"Purified Water {suffix}", IsActive = true };
        db.WaterDepartments.Add(waterDept);

        await db.SaveChangesAsync();
        return (user.Id, micro.Id, other.Id, cause.Id, item1.Id, item2.Id, waterDept.Id);
    }

    [PostgresFact]
    public async Task AllValid_CreatesAllSamples_ConsecutiveNumbers_InRowOrder_WithAuditRows()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, microId, _, causeId, itemId, secondItemId, _) = await SeedProductAndWaterAsync(suffix);
        var stage = $"Bulk-{suffix}";

        BatchReceiveResult result;
        await using (var actDb = _fixture.CreateDbContext())
        {
            var svc = BuildService(actDb);
            var req = new BatchReceiveRequest(
                "Product",
                new[]
                {
                    new ReceiveItemBasedSampleRequest(itemId, causeId, "10 g", "Sampler A", $"B1-{suffix}", $"C1-{suffix}", null, null, stage, new List<int> { microId }),
                    new ReceiveItemBasedSampleRequest(secondItemId, causeId, "20 g", "Sampler B", $"B2-{suffix}", $"C2-{suffix}", null, null, stage, new List<int> { microId }),
                    new ReceiveItemBasedSampleRequest(itemId, causeId, "30 g", "Sampler C", $"B3-{suffix}", $"C3-{suffix}", null, null, stage, new List<int> { microId })
                },
                null, null, null);

            result = await svc.ReceiveAsync(req, userId);
        }

        Assert.Equal(3, result.Samples.Count);
        Assert.Equal(new[] { 0, 1, 2 }, result.Samples.Select(s => s.Row).ToArray());

        var seq0 = int.Parse(result.Samples[0].ReferenceNumber[^3..]);
        var seq1 = int.Parse(result.Samples[1].ReferenceNumber[^3..]);
        var seq2 = int.Parse(result.Samples[2].ReferenceNumber[^3..]);
        Assert.Equal(seq0 + 1, seq1);
        Assert.Equal(seq1 + 1, seq2);

        await using var assertDb = _fixture.CreateDbContext();
        var ids = result.Samples.Select(s => s.SampleId).ToList();
        var persisted = await assertDb.Samples
            .AsNoTracking()
            .Include(s => s.TestOrders)
            .Where(s => ids.Contains(s.Id))
            .OrderBy(s => s.Id)
            .ToListAsync();

        Assert.Equal(3, persisted.Count);
        Assert.Equal($"B1-{suffix}", persisted[0].BatchNumber);
        Assert.Equal($"B2-{suffix}", persisted[1].BatchNumber);
        Assert.Equal($"B3-{suffix}", persisted[2].BatchNumber);
        Assert.All(persisted, s =>
        {
            Assert.Equal(SampleStatus.Received, s.Status);
            Assert.Equal(userId, s.ReceivedByUserId);
            Assert.Single(s.TestOrders);
        });

        var idStrings = ids.Select(id => id.ToString()).ToList();
        var auditRows = await assertDb.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityName == nameof(Sample) && a.Action == "Create" && idStrings.Contains(a.EntityId))
            .ToListAsync();
        Assert.Equal(3, auditRows.Count);
    }

    [PostgresFact]
    public async Task AllValid_Water_CreatesAllSamples_ConsecutiveNumbers_WithAuditRows()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, microId, _, causeId, _, _, waterDeptId) = await SeedProductAndWaterAsync(suffix);
        var sampledAt = DateTime.UtcNow.AddMinutes(-10);

        BatchReceiveResult result;
        await using (var actDb = _fixture.CreateDbContext())
        {
            var svc = BuildService(actDb);
            var req = new BatchReceiveRequest(
                "Water",
                null,
                new[]
                {
                    new ReceiveWaterRequest(waterDeptId, causeId, "500 mL", "Sampler W1", $"CW1-{suffix}", sampledAt, microId),
                    new ReceiveWaterRequest(waterDeptId, causeId, "500 mL", "Sampler W2", $"CW2-{suffix}", sampledAt.AddMinutes(5), microId)
                },
                null, null);

            result = await svc.ReceiveAsync(req, userId);
        }

        Assert.Equal(2, result.Samples.Count);
        Assert.Equal(0, result.Samples[0].Row);
        Assert.Equal(1, result.Samples[1].Row);

        var seq0 = int.Parse(result.Samples[0].ReferenceNumber[^3..]);
        var seq1 = int.Parse(result.Samples[1].ReferenceNumber[^3..]);
        Assert.Equal(seq0 + 1, seq1);

        await using var assertDb = _fixture.CreateDbContext();
        var ids = result.Samples.Select(s => s.SampleId).ToList();
        var persisted = await assertDb.Samples.AsNoTracking().Where(s => ids.Contains(s.Id)).ToListAsync();
        Assert.Equal(2, persisted.Count);
        Assert.All(persisted, s =>
        {
            Assert.Equal(SampleCategory.Water, s.Category);
            Assert.Equal(microId, s.LabSectionId);
        });

        var idStrings = ids.Select(id => id.ToString()).ToList();
        var auditRows = await assertDb.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityName == nameof(Sample) && a.Action == "Create" && idStrings.Contains(a.EntityId))
            .ToListAsync();
        Assert.Equal(2, auditRows.Count);
    }

    [PostgresFact]
    public async Task OneInvalidRow_NothingCreated_ReturnsRowAndField()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, microId, _, causeId, itemId, _, _) = await SeedProductAndWaterAsync(suffix);
        var stage = $"Bulk-{suffix}";

        await using (var actDb = _fixture.CreateDbContext())
        {
            var svc = BuildService(actDb);
            var req = new BatchReceiveRequest(
                "Product",
                new[]
                {
                    new ReceiveItemBasedSampleRequest(itemId, causeId, "10 g", "Sampler A", $"B1-{suffix}", $"C1-{suffix}", null, null, stage, new List<int> { microId }),
                    new ReceiveItemBasedSampleRequest(itemId, causeId, "10 g", "Sampler B", "", $"C2-{suffix}", null, null, stage, new List<int> { microId })
                },
                null, null, null);

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.ReceiveAsync(req, userId));
            var err = Assert.Single(ex.FieldErrors);
            Assert.Equal(1, err.Row);
            Assert.Equal("batchNumber", err.Field);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.Samples.AsNoTracking().AnyAsync(s => s.ControlNumber == $"C1-{suffix}" || s.ControlNumber == $"C2-{suffix}"));
    }

    [PostgresFact]
    public async Task ThreeInvalidRows_AllThreeReported()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, microId, _, causeId, itemId, _, _) = await SeedProductAndWaterAsync(suffix);
        var stage = $"Bulk-{suffix}";

        await using var actDb = _fixture.CreateDbContext();
        var svc = BuildService(actDb);
        var req = new BatchReceiveRequest(
            "Product",
            new[]
            {
                new ReceiveItemBasedSampleRequest(0, causeId, "10 g", "Sampler A", $"B0-{suffix}", $"C0-{suffix}", null, null, stage, new List<int> { microId }),
                new ReceiveItemBasedSampleRequest(itemId, causeId, "10 g", "Sampler B", $"B1-{suffix}", "", null, null, stage, new List<int> { microId }),
                new ReceiveItemBasedSampleRequest(itemId, causeId, "10 g", "Sampler C", $"B2-{suffix}", $"C2-{suffix}", null, null, stage, new List<int> { microId }),
                new ReceiveItemBasedSampleRequest(itemId, causeId, "10 g", "", $"B3-{suffix}", $"C3-{suffix}", null, null, stage, new List<int> { microId })
            },
            null, null, null);

        var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.ReceiveAsync(req, userId));
        Assert.Equal(3, ex.FieldErrors.Count);
        Assert.Contains(ex.FieldErrors, e => e.Row == 0 && e.Field == "itemId");
        Assert.Contains(ex.FieldErrors, e => e.Row == 1 && e.Field == "controlNumber");
        Assert.Contains(ex.FieldErrors, e => e.Row == 3 && e.Field == "sampledBy");
    }

    [PostgresFact]
    public async Task LastRowFailsInsideTheEngine_EverythingRolledBack_NoSamples_NoAuditRows_NumbersNotConsumed()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, microId, _, causeId, item1Id, item2Id, _) = await SeedProductAndWaterAsync(suffix);
        var stage = $"Bulk-{suffix}";

        string expectedFirstNumber;
        await using (var preDb = _fixture.CreateDbContext())
        {
            await using var tx = await preDb.Database.BeginTransactionAsync();
            expectedFirstNumber = await new ReferenceNumberGenerator(preDb).GenerateAsync(SampleCategory.FinishedProduct);
            await tx.RollbackAsync();
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var realReceiving = new ReceivingService(
                new ProductWorkflowEngine(actDb, new ReferenceNumberGenerator(actDb)),
                new ReceiveSampleValidator());

            var decorator = new DeactivatingReceivingDecorator(realReceiving, targetCallIndex: 1, beforeNthCall: async () =>
            {
                await using var secondDb = _fixture.CreateDbContext();
                var target = await secondDb.Items.SingleAsync(i => i.Id == item2Id);
                target.IsActive = false;
                await secondDb.SaveChangesAsync();
            });

            var svc = BuildService(actDb, null, decorator);
            var req = new BatchReceiveRequest(
                "Product",
                new[]
                {
                    new ReceiveItemBasedSampleRequest(item1Id, causeId, "10 g", "Sampler A", $"B1-{suffix}", $"RB1-{suffix}", null, null, stage, new List<int> { microId }),
                    new ReceiveItemBasedSampleRequest(item2Id, causeId, "20 g", "Sampler B", $"B2-{suffix}", $"RB2-{suffix}", null, null, stage, new List<int> { microId })
                },
                null, null, null);

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.ReceiveAsync(req, userId));
            Assert.Contains(ex.FieldErrors, e => e.Row == 1);

            // Change tracker must not retain rolled-back Sample entities from row 0.
            Assert.Empty(actDb.ChangeTracker.Entries<Sample>());
        }

        await using (var assertDb = _fixture.CreateDbContext())
        {
            Assert.False(await assertDb.Samples.AsNoTracking().AnyAsync(s => s.ControlNumber == $"RB1-{suffix}" || s.ControlNumber == $"RB2-{suffix}"));
            var auditForUser = await assertDb.AuditLogs
                .AsNoTracking()
                .Where(a => a.EntityName == nameof(Sample) && a.Action == "Create" && a.ControlNumber == $"RB1-{suffix}")
                .ToListAsync();
            Assert.Empty(auditForUser);
        }

        // Receiving one more sample in the same month must get the first number (not skipped).
        await using (var nextDb = _fixture.CreateDbContext())
        {
            var svc = BuildService(nextDb);
            var next = await svc.ReceiveAsync(new BatchReceiveRequest(
                "Product",
                new[]
                {
                    new ReceiveItemBasedSampleRequest(item1Id, causeId, "10 g", "Sampler A", $"BN-{suffix}", $"CN-{suffix}", null, null, stage, new List<int> { microId })
                },
                null, null, null), userId);

            Assert.Equal(expectedFirstNumber, next.Samples.Single().ReferenceNumber);
        }
    }

    [PostgresFact]
    public async Task Rows101_Rejected()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, microId, _, causeId, itemId, _, _) = await SeedProductAndWaterAsync(suffix);
        var stage = $"Bulk-{suffix}";
        var rows = Enumerable.Range(0, 101)
            .Select(i => new ReceiveItemBasedSampleRequest(itemId, causeId, "10 g", "Sampler", $"B{i}-{suffix}", $"C{i}-{suffix}", null, null, stage, new List<int> { microId }))
            .ToList();

        await using var actDb = _fixture.CreateDbContext();
        var svc = BuildService(actDb);
        await Assert.ThrowsAsync<FieldValidationException>(() =>
            svc.ReceiveAsync(new BatchReceiveRequest("Product", rows, null, null, null), userId));
    }

    [PostgresFact]
    public async Task EmptyList_Rejected()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, _, _, _, _, _, _) = await SeedProductAndWaterAsync(suffix);

        await using var actDb = _fixture.CreateDbContext();
        var svc = BuildService(actDb);
        await Assert.ThrowsAsync<FieldValidationException>(() =>
            svc.ReceiveAsync(new BatchReceiveRequest("Product", Array.Empty<ReceiveItemBasedSampleRequest>(), null, null, null), userId));
    }

    [PostgresFact]
    public async Task TwoListsSet_Rejected()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, microId, _, causeId, itemId, _, waterDeptId) = await SeedProductAndWaterAsync(suffix);

        await using var actDb = _fixture.CreateDbContext();
        var svc = BuildService(actDb);
        await Assert.ThrowsAsync<FieldValidationException>(() =>
            svc.ReceiveAsync(new BatchReceiveRequest(
                "Product",
                new[] { new ReceiveItemBasedSampleRequest(itemId, causeId, "10 g", "Sampler", "B1", "C1", null, null, $"Bulk-{suffix}", new List<int> { microId }) },
                new[] { new ReceiveWaterRequest(waterDeptId, causeId, "100 mL", "Sampler", "CW1") },
                null,
                null), userId));
    }

    [PostgresFact]
    public async Task CategoryMismatch_Rejected()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, microId, _, causeId, itemId, _, _) = await SeedProductAndWaterAsync(suffix);

        await using var actDb = _fixture.CreateDbContext();
        var svc = BuildService(actDb);
        await Assert.ThrowsAsync<FieldValidationException>(() =>
            svc.ReceiveAsync(new BatchReceiveRequest(
                "Water",
                new[] { new ReceiveItemBasedSampleRequest(itemId, causeId, "10 g", "Sampler", "B1", "C1", null, null, $"Bulk-{suffix}", new List<int> { microId }) },
                null,
                null,
                null), userId));
    }

    [PostgresFact]
    public async Task RowForSectionICannotWriteTo_WholeBatchRejected_WithThatRow()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, microId, otherId, causeId, itemId, _, _) = await SeedProductAndWaterAsync(suffix);
        var stage = $"Bulk-{suffix}";

        await using (var actDb = _fixture.CreateDbContext())
        {
            var svc = BuildService(actDb);
            var req = new BatchReceiveRequest(
                "Product",
                new[]
                {
                    new ReceiveItemBasedSampleRequest(itemId, causeId, "10 g", "Sampler A", $"B0-{suffix}", $"SEC0-{suffix}", null, null, stage, new List<int> { microId }),
                    new ReceiveItemBasedSampleRequest(itemId, causeId, "10 g", "Sampler B", $"B1-{suffix}", $"SEC1-{suffix}", null, null, stage, new List<int> { otherId })
                },
                null, null, null);

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.ReceiveAsync(req, userId));
            var err = Assert.Single(ex.FieldErrors);
            Assert.Equal(1, err.Row);
            Assert.Equal("targetSectionIds", err.Field);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.Samples.AsNoTracking().AnyAsync(s => s.ControlNumber == $"SEC0-{suffix}" || s.ControlNumber == $"SEC1-{suffix}"));
    }

    [PostgresFact]
    public async Task ResultOfBatch_EqualsResultOfSameRowsReceivedSingly()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, microId, _, causeId, item1Id, item2Id, _) = await SeedProductAndWaterAsync(suffix);
        var stage = $"Bulk-{suffix}";

        var rows = new[]
        {
            new ReceiveItemBasedSampleRequest(item1Id, causeId, "15 g", "Sampler A", $"SB1-{suffix}", $"SC1-{suffix}", null, null, stage, new List<int> { microId }),
            new ReceiveItemBasedSampleRequest(item2Id, causeId, "25 g", "Sampler B", $"SB2-{suffix}", $"SC2-{suffix}", null, null, stage, new List<int> { microId })
        };

        var singleIds = new List<int>();
        await using (var singleDb = _fixture.CreateDbContext())
        {
            var receiving = new ReceivingService(new ProductWorkflowEngine(singleDb, new ReferenceNumberGenerator(singleDb)), new ReceiveSampleValidator());
            foreach (var r in rows)
            {
                var dto = await receiving.ReceiveSampleAsync(new ItemBasedReceiveRequest(
                    r.ItemId, r.CauseOfTestingId, r.SampleQuantity, r.SampledBy,
                    r.BatchNumber, r.ControlNumber, r.MfgDate, r.ExpDate,
                    r.ProductionStage, userId, r.TargetSectionIds, r.Grade));
                singleIds.Add(dto.Id);
            }
        }

        BatchReceiveResult batchResult;
        await using (var batchDb = _fixture.CreateDbContext())
        {
            var svc = BuildService(batchDb);
            batchResult = await svc.ReceiveAsync(new BatchReceiveRequest("Product", rows, null, null, null), userId);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var singleSamples = await assertDb.Samples.AsNoTracking().Include(s => s.TestOrders).Where(s => singleIds.Contains(s.Id)).OrderBy(s => s.Id).ToListAsync();
        var batchIds = batchResult.Samples.Select(s => s.SampleId).ToList();
        var batchSamples = await assertDb.Samples.AsNoTracking().Include(s => s.TestOrders).Where(s => batchIds.Contains(s.Id)).OrderBy(s => s.Id).ToListAsync();

        Assert.Equal(singleSamples.Count, batchSamples.Count);
        for (var i = 0; i < singleSamples.Count; i++)
        {
            var s = singleSamples[i];
            var b = batchSamples[i];
            Assert.Equal(s.Category, b.Category);
            Assert.Equal(s.ItemId, b.ItemId);
            Assert.Equal(s.CauseOfTestingId, b.CauseOfTestingId);
            Assert.Equal(s.SampleQuantity, b.SampleQuantity);
            Assert.Equal(s.SampledBy, b.SampledBy);
            Assert.Equal(s.BatchNumber, b.BatchNumber);
            Assert.Equal(s.ControlNumber, b.ControlNumber);
            Assert.Equal(s.ProductionStage, b.ProductionStage);
            Assert.Equal(s.ProductionStageId, b.ProductionStageId);
            Assert.Equal(s.ReceivedByUserId, b.ReceivedByUserId);
            Assert.Equal(s.Status, b.Status);
            Assert.Equal(s.PreparationStatus, b.PreparationStatus);
            Assert.Equal(
                s.TestOrders.OrderBy(o => o.TestCode).Select(o => (o.TestCode, o.SectionId, o.Status, o.CurrentStep)),
                b.TestOrders.OrderBy(o => o.TestCode).Select(o => (o.TestCode, o.SectionId, o.Status, o.CurrentStep)));
        }
    }

    [Fact]
    public void WithoutPermission_403()
    {
        var method = typeof(SampleController).GetMethod(nameof(SampleController.ReceiveBatch));
        Assert.NotNull(method);

        var post = method!.GetCustomAttribute<HttpPostAttribute>();
        Assert.NotNull(post);
        Assert.Equal("receive-batch", post!.Template);

        var auth = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(auth);
        Assert.Equal(PermissionConstants.SamplesReceiveOwnLab, auth!.Policy);
    }
}
