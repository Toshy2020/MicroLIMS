using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class NotApplicableTestServiceTests
{
    private const string Password = "Password123!";

    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static User CreateUser(MicroLimsDbContext db, RoleType roleType, string fullName, string username)
    {
        var role = db.Roles.Local.FirstOrDefault(r => r.Type == roleType)
            ?? db.Roles.FirstOrDefault(r => r.Type == roleType);
        if (role is null)
        {
            role = new Role { Type = roleType, Name = roleType.ToString(), IsSystemRole = true, IsActive = true };
            db.Roles.Add(role);
            db.SaveChanges();
        }

        var user = new User
        {
            FullName = fullName,
            Username = username,
            RoleId = role.Id,
            Role = role,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
            IsActive = true
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private static (Item Item, DocumentSection Section, CauseOfTesting RoutineCause, CauseOfTesting RetestCause) SeedItemWithTests(
        MicroLimsDbContext db, params string[] testCodes)
    {
        var section = db.DocumentSections.FirstOrDefault(s => s.Code == "MIC")
            ?? new DocumentSection { Name = "Microbiology", Code = "MIC" };
        if (section.Id == 0)
        {
            db.DocumentSections.Add(section);
            db.SaveChanges();
        }

        var item = new Item
        {
            Name = "Item " + Guid.NewGuid().ToString("N")[..6],
            Code = "ITM-" + Guid.NewGuid().ToString("N")[..6],
            Category = SampleCategory.RawMaterial,
            IsActive = true
        };

        foreach (var code in testCodes)
        {
            if (!db.TestDefinitions.Any(t => t.Code == code))
                db.TestDefinitions.Add(new TestDefinition { Code = code, DisplayName = code + " Name", SectionId = section.Id });

            item.AssignedTests.Add(new SampleTest { TestCode = code, DisplayName = code + " Name" });
        }

        db.Items.Add(item);

        var routine = db.CausesOfTesting.FirstOrDefault(c => c.Name == "Routine")
            ?? new CauseOfTesting { Name = "Routine", RequiresTestSelection = false };
        if (routine.Id == 0) db.CausesOfTesting.Add(routine);

        var retest = db.CausesOfTesting.FirstOrDefault(c => c.Name == "Periodic retest")
            ?? new CauseOfTesting { Name = "Periodic retest", RequiresTestSelection = true };
        if (retest.Id == 0) db.CausesOfTesting.Add(retest);

        db.SaveChanges();
        return (item, section, routine, retest);
    }

    private static NotApplicableTestService CreateService(MicroLimsDbContext db) =>
        new(db, new ElectronicSignatureService(db), new AuditEventService(db, new DatabaseSequenceHelper(db)));

    private static CreateNotApplicableTestRequest MakeCreateRequest(int itemId, string testCode, string? grade = null) =>
        new(
            ItemId: itemId,
            Grade: grade,
            TestCode: testCode,
            Justification: "Test justification",
            Criticality: "Non-critical",
            ImmediateAction: "Skip testing",
            LongTermAction: "Update dossier",
            Reference: "QC-NA-01");

    [Fact]
    public async Task Create_RefusesATestNotAssignedToTheItem()
    {
        using var db = NewDb();
        var user = CreateUser(db, RoleType.SectionHead, "Section Head", "shead");
        var (item, _, _, _) = SeedItemWithTests(db, "TAMC");
        var service = CreateService(db);

        var request = MakeCreateRequest(item.Id, "UNASSIGNED_TEST");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(user.Id, request));
        Assert.Contains("UNASSIGNED_TEST", ex.Message);
    }

    [Fact]
    public async Task Create_RefusesAGradeTheItemDoesNotHave()
    {
        using var db = NewDb();
        var user = CreateUser(db, RoleType.SectionHead, "Section Head", "shead");
        var (item, _, _, _) = SeedItemWithTests(db, "TAMC");

        db.Specifications.Add(new Specification
        {
            ItemId = item.Id,
            TestCode = "TAMC",
            ParameterName = "Count",
            Grade = "BP"
        });
        db.SaveChanges();

        var service = CreateService(db);
        var request = MakeCreateRequest(item.Id, "TAMC", grade: "USP");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(user.Id, request));
        Assert.Contains("USP", ex.Message);
    }

    [Fact]
    public async Task FinalApprove_BeforeHeadApprove_IsRefused()
    {
        using var db = NewDb();
        var head = CreateUser(db, RoleType.SectionHead, "Section Head", "shead");
        var qc = CreateUser(db, RoleType.QcManager, "QC Manager", "qcman");
        var (item, _, _, _) = SeedItemWithTests(db, "TAMC");
        var service = CreateService(db);

        var created = await service.CreateAsync(head.Id, MakeCreateRequest(item.Id, "TAMC"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.FinalApproveAsync(qc.Id, created.Id, Password, "Approval comment"));
        Assert.Contains("Head", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FinalApprove_BySameUserAsHead_IsRefused()
    {
        using var db = NewDb();
        var dualUser = CreateUser(db, RoleType.SectionHead, "Dual User", "dualuser");
        var (item, _, _, _) = SeedItemWithTests(db, "TAMC");
        var service = CreateService(db);

        var created = await service.CreateAsync(dualUser.Id, MakeCreateRequest(item.Id, "TAMC"));
        await service.HeadApproveAsync(dualUser.Id, created.Id, Password, "Head approval");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.FinalApproveAsync(dualUser.Id, created.Id, Password, "QC approval"));
        Assert.Equal("The QC Manager approval must come from a different person than the Section Head approval.", ex.Message);
    }

    [Fact]
    public async Task ApprovedRecord_SkipsTheTestAtReceipt()
    {
        using var db = NewDb();
        var head = CreateUser(db, RoleType.SectionHead, "Section Head", "shead");
        var qc = CreateUser(db, RoleType.QcManager, "QC Manager", "qcman");
        var (item, _, routine, _) = SeedItemWithTests(db, "TAMC", "TYMC");
        var service = CreateService(db);

        var created = await service.CreateAsync(head.Id, MakeCreateRequest(item.Id, "TAMC"));
        await service.HeadApproveAsync(head.Id, created.Id, Password, "Head signed");
        await service.FinalApproveAsync(qc.Id, created.Id, Password, "QC signed");

        var engine = new ProductWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var sample = await engine.ReceiveAsync(new ItemBasedReceiveRequest(
            item.Id, routine.Id, "100 g", "Sampler", "LOT-1", "CTRL-1",
            DateTime.UtcNow, DateTime.UtcNow.AddYears(1), null, head.Id));

        Assert.DoesNotContain(sample.TestOrders, o => o.TestCode == "TAMC");
        Assert.Contains(sample.TestOrders, o => o.TestCode == "TYMC");
    }

    [Fact]
    public async Task DraftOrHeadApprovedOrWithdrawn_DoesNotSkip()
    {
        using var db = NewDb();
        var head = CreateUser(db, RoleType.SectionHead, "Section Head", "shead");
        var qc = CreateUser(db, RoleType.QcManager, "QC Manager", "qcman");
        var (item, _, routine, _) = SeedItemWithTests(db, "TAMC");
        var service = CreateService(db);
        var engine = new ProductWorkflowEngine(db, new ReferenceNumberGenerator(db));

        // 1. Draft
        var record = await service.CreateAsync(head.Id, MakeCreateRequest(item.Id, "TAMC"));
        var sample1 = await engine.ReceiveAsync(new ItemBasedReceiveRequest(
            item.Id, routine.Id, "100 g", "Sampler", "LOT-1", "CTRL-1",
            DateTime.UtcNow, DateTime.UtcNow.AddYears(1), null, head.Id));
        Assert.Contains(sample1.TestOrders, o => o.TestCode == "TAMC");

        // 2. HeadApproved
        await service.HeadApproveAsync(head.Id, record.Id, Password, "Head ok");
        var sample2 = await engine.ReceiveAsync(new ItemBasedReceiveRequest(
            item.Id, routine.Id, "100 g", "Sampler", "LOT-2", "CTRL-2",
            DateTime.UtcNow, DateTime.UtcNow.AddYears(1), null, head.Id));
        Assert.Contains(sample2.TestOrders, o => o.TestCode == "TAMC");

        // 3. Approved then Withdrawn
        await service.FinalApproveAsync(qc.Id, record.Id, Password, "QC ok");
        await service.WithdrawAsync(head.Id, record.Id, Password, "Withdrawing rule");
        var sample3 = await engine.ReceiveAsync(new ItemBasedReceiveRequest(
            item.Id, routine.Id, "100 g", "Sampler", "LOT-3", "CTRL-3",
            DateTime.UtcNow, DateTime.UtcNow.AddYears(1), null, head.Id));
        Assert.Contains(sample3.TestOrders, o => o.TestCode == "TAMC");
    }

    [Fact]
    public async Task GradedRecord_SkipsOnlyThatGrade()
    {
        using var db = NewDb();
        var head = CreateUser(db, RoleType.SectionHead, "Section Head", "shead");
        var qc = CreateUser(db, RoleType.QcManager, "QC Manager", "qcman");
        var (item, _, routine, _) = SeedItemWithTests(db, "TAMC", "ASSAY", "LOD");

        db.Specifications.AddRange(
            new Specification { ItemId = item.Id, TestCode = "TAMC", ParameterName = "Count", Grade = "BP" },
            new Specification { ItemId = item.Id, TestCode = "TAMC", ParameterName = "Count", Grade = "USP" },
            new Specification { ItemId = item.Id, TestCode = "ASSAY", ParameterName = "Assay", Grade = "BP" },
            new Specification { ItemId = item.Id, TestCode = "ASSAY", ParameterName = "Assay", Grade = "USP" },
            new Specification { ItemId = item.Id, TestCode = "LOD", ParameterName = "Loss", Grade = "BP" },
            new Specification { ItemId = item.Id, TestCode = "LOD", ParameterName = "Loss", Grade = "USP" }
        );
        db.SaveChanges();

        var service = CreateService(db);
        var engine = new ProductWorkflowEngine(db, new ReferenceNumberGenerator(db));

        // Graded record on TAMC for BP
        var bpRecord = await service.CreateAsync(head.Id, MakeCreateRequest(item.Id, "TAMC", grade: "BP"));
        await service.HeadApproveAsync(head.Id, bpRecord.Id, Password, "Head signed");
        await service.FinalApproveAsync(qc.Id, bpRecord.Id, Password, "QC signed");

        // Receipt with USP: TAMC still ordered
        var uspSample = await engine.ReceiveAsync(new ItemBasedReceiveRequest(
            item.Id, routine.Id, "100 g", "Sampler", "LOT-USP", "CTRL-USP",
            DateTime.UtcNow, DateTime.UtcNow.AddYears(1), null, head.Id, Grade: "USP"));
        Assert.Contains(uspSample.TestOrders, o => o.TestCode == "TAMC");

        // Receipt with BP: TAMC skipped
        var bpSample = await engine.ReceiveAsync(new ItemBasedReceiveRequest(
            item.Id, routine.Id, "100 g", "Sampler", "LOT-BP", "CTRL-BP",
            DateTime.UtcNow, DateTime.UtcNow.AddYears(1), null, head.Id, Grade: "BP"));
        Assert.DoesNotContain(bpSample.TestOrders, o => o.TestCode == "TAMC");

        // Now an ungraded (null-grade) record on ASSAY
        var nullRecord = await service.CreateAsync(head.Id, MakeCreateRequest(item.Id, "ASSAY", grade: null));
        await service.HeadApproveAsync(head.Id, nullRecord.Id, Password, "Head signed");
        await service.FinalApproveAsync(qc.Id, nullRecord.Id, Password, "QC signed");

        var uspSample2 = await engine.ReceiveAsync(new ItemBasedReceiveRequest(
            item.Id, routine.Id, "100 g", "Sampler", "LOT-USP2", "CTRL-USP2",
            DateTime.UtcNow, DateTime.UtcNow.AddYears(1), null, head.Id, Grade: "USP"));
        Assert.DoesNotContain(uspSample2.TestOrders, o => o.TestCode == "ASSAY");

        var bpSample2 = await engine.ReceiveAsync(new ItemBasedReceiveRequest(
            item.Id, routine.Id, "100 g", "Sampler", "LOT-BP2", "CTRL-BP2",
            DateTime.UtcNow, DateTime.UtcNow.AddYears(1), null, head.Id, Grade: "BP"));
        Assert.DoesNotContain(bpSample2.TestOrders, o => o.TestCode == "ASSAY");
    }

    [Fact]
    public async Task ApprovingAfterReceipt_LeavesExistingOrders()
    {
        using var db = NewDb();
        var head = CreateUser(db, RoleType.SectionHead, "Section Head", "shead");
        var qc = CreateUser(db, RoleType.QcManager, "QC Manager", "qcman");
        var (item, _, routine, _) = SeedItemWithTests(db, "TAMC");
        var service = CreateService(db);
        var engine = new ProductWorkflowEngine(db, new ReferenceNumberGenerator(db));

        // Create in draft
        var record = await service.CreateAsync(head.Id, MakeCreateRequest(item.Id, "TAMC"));

        // Receive sample while Draft: order created
        var sample = await engine.ReceiveAsync(new ItemBasedReceiveRequest(
            item.Id, routine.Id, "100 g", "Sampler", "LOT-1", "CTRL-1",
            DateTime.UtcNow, DateTime.UtcNow.AddYears(1), null, head.Id));
        Assert.Contains(sample.TestOrders, o => o.TestCode == "TAMC");

        // Now approve the record
        await service.HeadApproveAsync(head.Id, record.Id, Password, "Head signed");
        await service.FinalApproveAsync(qc.Id, record.Id, Password, "QC signed");

        // Reload sample orders
        var reloadedOrders = await db.TestOrders.Where(o => o.SampleId == sample.Id).ToListAsync();
        Assert.Contains(reloadedOrders, o => o.TestCode == "TAMC");
    }

    [Fact]
    public async Task PeriodicRetestSelection_DoesNotOfferANotApplicableTest()
    {
        using var db = NewDb();
        var head = CreateUser(db, RoleType.SectionHead, "Section Head", "shead");
        var qc = CreateUser(db, RoleType.QcManager, "QC Manager", "qcman");
        var (item, _, _, retest) = SeedItemWithTests(db, "TAMC", "TYMC");
        var service = CreateService(db);
        var engine = new ProductWorkflowEngine(db, new ReferenceNumberGenerator(db));

        // Approve not-applicable for TAMC
        var record = await service.CreateAsync(head.Id, MakeCreateRequest(item.Id, "TAMC"));
        await service.HeadApproveAsync(head.Id, record.Id, Password, "Head signed");
        await service.FinalApproveAsync(qc.Id, record.Id, Password, "QC signed");

        // Receive sample for periodic retest
        var sample = await engine.ReceiveAsync(new ItemBasedReceiveRequest(
            item.Id, retest.Id, "100 g", "Sampler", "LOT-RETEST", "CTRL-RETEST",
            DateTime.UtcNow, DateTime.UtcNow.AddYears(1), null, head.Id));
        Assert.True(sample.AwaitingTestSelection);

        var selectionService = TestServiceFactory.SampleTestSelection(db);
        var eligible = await selectionService.GetEligibleAsync(sample.Id);

        Assert.DoesNotContain(eligible, e => e.TestCode == "TAMC");
        Assert.Contains(eligible, e => e.TestCode == "TYMC");
    }

    [Fact]
    public async Task EachStep_WritesASignatureAndAuditEntry()
    {
        using var db = NewDb();
        var head = CreateUser(db, RoleType.SectionHead, "Section Head", "shead");
        var qc = CreateUser(db, RoleType.QcManager, "QC Manager", "qcman");
        var (item, _, _, _) = SeedItemWithTests(db, "TAMC");
        var service = CreateService(db);

        // Step 1: Create
        var created = await service.CreateAsync(head.Id, MakeCreateRequest(item.Id, "TAMC"));
        Assert.Equal(NotApplicableStatus.Draft, created.Status);
        var createAudit = await db.AuditLogs.FirstOrDefaultAsync(a => a.ActionCode == NotApplicableTestService.CreatedActionCode && a.EntityId == created.Id.ToString());
        Assert.NotNull(createAudit);
        Assert.Equal(AuditActionCategory.Configuration, createAudit.ActionCategory);
        Assert.Equal("Test justification", createAudit.Reason);

        // Step 2: Head Approve
        var headApproved = await service.HeadApproveAsync(head.Id, created.Id, Password, "Head approval comment");
        Assert.Equal(NotApplicableStatus.HeadApproved, headApproved.Status);
        Assert.NotNull(headApproved.HeadSignatureId);
        var headSig = await db.ElectronicSignatures.FirstOrDefaultAsync(s => s.Id == headApproved.HeadSignatureId);
        Assert.NotNull(headSig);
        Assert.Equal(SignatureMeaning.NotApplicableHeadApproved, headSig.MeaningOfSignature);
        Assert.Equal("NotApplicableTest", headSig.EntityType);
        Assert.Equal(created.Id, headSig.EntityId);
        Assert.Equal(head.Id, headSig.UserId);

        var headAudit = await db.AuditLogs.FirstOrDefaultAsync(a => a.ActionCode == NotApplicableTestService.HeadApprovedActionCode && a.EntityId == created.Id.ToString());
        Assert.NotNull(headAudit);
        Assert.Equal("Head approval comment", headAudit.Reason);

        // Step 3: Final Approve
        var finalApproved = await service.FinalApproveAsync(qc.Id, created.Id, Password, "QC approval comment");
        Assert.Equal(NotApplicableStatus.Approved, finalApproved.Status);
        Assert.NotNull(finalApproved.FinalSignatureId);
        var finalSig = await db.ElectronicSignatures.FirstOrDefaultAsync(s => s.Id == finalApproved.FinalSignatureId);
        Assert.NotNull(finalSig);
        Assert.Equal(SignatureMeaning.NotApplicableFinalApproved, finalSig.MeaningOfSignature);
        Assert.Equal("NotApplicableTest", finalSig.EntityType);
        Assert.Equal(created.Id, finalSig.EntityId);
        Assert.Equal(qc.Id, finalSig.UserId);

        var finalAudit = await db.AuditLogs.FirstOrDefaultAsync(a => a.ActionCode == NotApplicableTestService.FinalApprovedActionCode && a.EntityId == created.Id.ToString());
        Assert.NotNull(finalAudit);
        Assert.Equal("QC approval comment", finalAudit.Reason);

        // Step 4: Withdraw
        var withdrawn = await service.WithdrawAsync(head.Id, created.Id, Password, "Reason for withdrawal");
        Assert.Equal(NotApplicableStatus.Withdrawn, withdrawn.Status);

        var withdrawSig = await db.ElectronicSignatures.FirstOrDefaultAsync(s =>
            s.EntityType == "NotApplicableTest" && s.EntityId == created.Id && s.MeaningOfSignature == SignatureMeaning.NotApplicableWithdrawn);
        Assert.NotNull(withdrawSig);
        Assert.Equal("Reason for withdrawal", withdrawSig.Comment);

        var withdrawAudit = await db.AuditLogs.FirstOrDefaultAsync(a => a.ActionCode == NotApplicableTestService.WithdrawnActionCode && a.EntityId == created.Id.ToString());
        Assert.NotNull(withdrawAudit);
        Assert.Equal("Reason for withdrawal", withdrawAudit.Reason);
    }

    [Fact]
    public async Task Withdraw_WritesSignatureRowWithNotApplicableWithdrawn()
    {
        using var db = NewDb();
        var head = CreateUser(db, RoleType.SectionHead, "Section Head", "shead");
        var (item, _, _, _) = SeedItemWithTests(db, "TAMC");
        var service = CreateService(db);

        var created = await service.CreateAsync(head.Id, MakeCreateRequest(item.Id, "TAMC"));
        await service.WithdrawAsync(head.Id, created.Id, Password, "Withdraw reason", "10.0.0.1");

        var sig = await db.ElectronicSignatures.FirstOrDefaultAsync(s =>
            s.EntityType == "NotApplicableTest" && s.EntityId == created.Id);
        Assert.NotNull(sig);
        Assert.Equal(SignatureMeaning.NotApplicableWithdrawn, sig.MeaningOfSignature);
        Assert.Equal("Withdraw reason", sig.Comment);
        Assert.Equal("10.0.0.1", sig.IpAddress);
    }

    [Fact]
    public async Task Withdraw_WrongPassword_RefusedAndGoesThroughFailedAttemptHandling()
    {
        using var db = NewDb();
        var head = CreateUser(db, RoleType.SectionHead, "Section Head", "shead");
        var (item, _, _, _) = SeedItemWithTests(db, "TAMC");
        var service = CreateService(db);

        var created = await service.CreateAsync(head.Id, MakeCreateRequest(item.Id, "TAMC"));

        var ex = await Assert.ThrowsAsync<SignatureVerificationException>(
            () => service.WithdrawAsync(head.Id, created.Id, "WrongPassword", "Withdraw reason"));

        Assert.Contains("Password verification failed. The signature was not applied.", ex.Message);

        var failedAudit = await db.AuditLogs.FirstOrDefaultAsync(a =>
            a.EntityName == "ElectronicSignature" &&
            a.Action == "SignatureFailed" &&
            a.UserId == head.Id);
        Assert.NotNull(failedAudit);
        Assert.Contains("NotApplicableTest", failedAudit.NewValue);

        var record = await db.NotApplicableTests.FindAsync(created.Id);
        Assert.Equal(NotApplicableStatus.Draft, record!.Status);
    }

    [Theory]
    [InlineData(NotApplicableStatus.Draft)]
    [InlineData(NotApplicableStatus.HeadApproved)]
    [InlineData(NotApplicableStatus.Approved)]
    public async Task Withdraw_WorksFromDraft_HeadApproved_AndApproved(NotApplicableStatus initialStatus)
    {
        using var db = NewDb();
        var head = CreateUser(db, RoleType.SectionHead, "Section Head", "shead");
        var qc = CreateUser(db, RoleType.QcManager, "QC Manager", "qcman");
        var (item, _, _, _) = SeedItemWithTests(db, "TAMC");
        var service = CreateService(db);

        var created = await service.CreateAsync(head.Id, MakeCreateRequest(item.Id, "TAMC"));
        if (initialStatus is NotApplicableStatus.HeadApproved or NotApplicableStatus.Approved)
        {
            await service.HeadApproveAsync(head.Id, created.Id, Password, "Head approve");
        }
        if (initialStatus == NotApplicableStatus.Approved)
        {
            await service.FinalApproveAsync(qc.Id, created.Id, Password, "Final approve");
        }

        var withdrawn = await service.WithdrawAsync(head.Id, created.Id, Password, "Withdrawing now");
        Assert.Equal(NotApplicableStatus.Withdrawn, withdrawn.Status);

        var loaded = await db.NotApplicableTests.FindAsync(created.Id);
        Assert.Equal(NotApplicableStatus.Withdrawn, loaded!.Status);
    }

    [Fact]
    public async Task FinalApprove_WhenHeadSignerCannotBeFound_Refuses()
    {
        using var db = NewDb();
        var head = CreateUser(db, RoleType.SectionHead, "Section Head", "shead");
        var qc = CreateUser(db, RoleType.QcManager, "QC Manager", "qcman");
        var (item, _, _, _) = SeedItemWithTests(db, "TAMC");
        var service = CreateService(db);

        var created = await service.CreateAsync(head.Id, MakeCreateRequest(item.Id, "TAMC"));
        var entity = await db.NotApplicableTests.FindAsync(created.Id);
        entity!.Status = NotApplicableStatus.HeadApproved;
        entity.HeadSignatureId = 99999;
        entity.HeadSignature = null;
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.FinalApproveAsync(qc.Id, created.Id, Password, "QC approval"));

        Assert.Equal("The Section Head approval could not be verified.", ex.Message);
    }

    [Fact]
    public async Task ReceiveAsync_UngradedReceipt_WhenAllTestsNotApplicable_Refuses()
    {
        using var db = NewDb();
        var head = CreateUser(db, RoleType.SectionHead, "Section Head", "shead");
        var qc = CreateUser(db, RoleType.QcManager, "QC Manager", "qcman");
        var (item, _, routine, _) = SeedItemWithTests(db, "TAMC");
        var service = CreateService(db);
        var engine = new ProductWorkflowEngine(db, new ReferenceNumberGenerator(db));

        var record = await service.CreateAsync(head.Id, MakeCreateRequest(item.Id, "TAMC", grade: null));
        await service.HeadApproveAsync(head.Id, record.Id, Password, "Head signed");
        await service.FinalApproveAsync(qc.Id, record.Id, Password, "QC signed");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ReceiveAsync(new ItemBasedReceiveRequest(
            item.Id, routine.Id, "100 g", "Sampler", "LOT-1", "CTRL-1",
            DateTime.UtcNow, DateTime.UtcNow.AddYears(1), null, head.Id, Grade: null)));

        Assert.Equal($"Every test assigned to '{item.Name}' is declared not applicable; there is nothing to test.", ex.Message);
    }

    [Fact]
    public async Task ReceiveAsync_UngradedReceipt_WhenItemHasNoAssignedTests_KeepsExistingBehaviour()
    {
        using var db = NewDb();
        var head = CreateUser(db, RoleType.SectionHead, "Section Head", "shead");
        var (item, _, routine, _) = SeedItemWithTests(db);
        var engine = new ProductWorkflowEngine(db, new ReferenceNumberGenerator(db));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ReceiveAsync(new ItemBasedReceiveRequest(
            item.Id, routine.Id, "100 g", "Sampler", "LOT-EMPTY", "CTRL-EMPTY",
            DateTime.UtcNow, DateTime.UtcNow.AddYears(1), null, head.Id, Grade: null)));

        Assert.Equal($"Item '{item.Name}' has no assigned tests. Configuration must be completed by the Section Head before samples can be received.", ex.Message);
    }

    [Fact]
    public async Task HeadApprove_WhenNotDraft_Throws()
    {
        using var db = NewDb();
        var head = CreateUser(db, RoleType.SectionHead, "Section Head", "shead");
        var (item, _, _, _) = SeedItemWithTests(db, "TAMC");
        var service = CreateService(db);

        var created = await service.CreateAsync(head.Id, MakeCreateRequest(item.Id, "TAMC"));
        await service.HeadApproveAsync(head.Id, created.Id, Password, "First approval");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.HeadApproveAsync(head.Id, created.Id, Password, "Second approval"));

        Assert.Equal("Only draft records can be approved by the Section Head.", ex.Message);
    }

    [Fact]
    public async Task Create_DuplicateCreate_RefusedWithFriendlyMessage()
    {
        using var db = NewDb();
        var head = CreateUser(db, RoleType.SectionHead, "Section Head", "shead");
        var (item, _, _, _) = SeedItemWithTests(db, "TAMC");
        var service = CreateService(db);

        await service.CreateAsync(head.Id, MakeCreateRequest(item.Id, "TAMC"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(head.Id, MakeCreateRequest(item.Id, "TAMC")));

        Assert.Equal($"A non-withdrawn not-applicable record already exists for item {item.Id}, test 'TAMC'.", ex.Message);
    }

    [Fact]
    public async Task Create_WhenPreviousRecordWithdrawn_FreesSlotForNewRecord()
    {
        using var db = NewDb();
        var head = CreateUser(db, RoleType.SectionHead, "Section Head", "shead");
        var (item, _, _, _) = SeedItemWithTests(db, "TAMC");
        var service = CreateService(db);

        var first = await service.CreateAsync(head.Id, MakeCreateRequest(item.Id, "TAMC"));
        await service.WithdrawAsync(head.Id, first.Id, Password, "Withdrawing to replace");

        var second = await service.CreateAsync(head.Id, MakeCreateRequest(item.Id, "TAMC"));
        Assert.NotNull(second);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(NotApplicableStatus.Draft, second.Status);
    }
}
