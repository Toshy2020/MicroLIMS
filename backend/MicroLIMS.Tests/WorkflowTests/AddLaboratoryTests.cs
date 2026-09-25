using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// Task 5 (lab separation): a laboratory can be added to a sample already
// received for another one - same sample, same ReferenceNumber, so one
// combined CoA still covers the batch. Seeding mirrors Task 4's
// LabTargetedReceiptTests (sections MICRO/FP, TestDefinitions TAMC/ASSAY).
public class AddLaboratoryTests
{
    private const string Password = "Correct-Horse-1!";

    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static DocumentSection EnsureFpSection(MicroLimsDbContext db)
    {
        var dept = db.DocumentDepartments.FirstOrDefault(d => d.Code == "QC")
            ?? db.DocumentDepartments.Add(new DocumentDepartment { Name = "Quality Control", Code = "QC", IsActive = true }).Entity;
        db.SaveChanges();

        var section = db.DocumentSections.FirstOrDefault(s => s.Code == "FP");
        if (section == null)
        {
            section = new DocumentSection { Name = "Physicochemical Laboratory", Code = "FP", DepartmentId = dept.Id, IsActive = true };
            db.DocumentSections.Add(section);
            db.SaveChanges();
        }
        return section;
    }

    // Seeds sections MICRO/FP, TestDefinition rows TAMC (MICRO) and ASSAY
    // (FP), an FP item with both tests assigned, and a signing user.
    private static async Task<(MicroLimsDbContext db, Item item, DocumentSection micro, DocumentSection fp, int userId)> SeedAsync()
    {
        var db = NewDb();
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var fp = EnsureFpSection(db);

        db.TestDefinitions.Add(new TestDefinition { Code = "TAMC", DisplayName = "Total Aerobic Microbial Count", SectionId = micro.Id });
        db.TestDefinitions.Add(new TestDefinition { Code = "ASSAY", DisplayName = "Assay", SectionId = fp.Id });

        var item = new Item { Name = "Tablet", Code = "TAB-01", Category = SampleCategory.FinishedProduct, IsActive = true };
        item.AssignedTests.Add(new SampleTest { TestCode = "TAMC" });
        item.AssignedTests.Add(new SampleTest { TestCode = "ASSAY" });
        db.Items.Add(item);

        var role = new Role { Type = RoleType.SystemAdministrator, Name = "SystemAdministrator" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new User { FullName = "Receiver", Username = "receiver", RoleId = role.Id, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password), IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return (db, item, micro, fp, user.Id);
    }

    // A sample received for FP only - one FP TestOrder (ASSAY), Ready.
    private static async Task<Sample> SeedFpOnlySampleAsync(MicroLimsDbContext db, Item item, DocumentSection fp)
    {
        var routine = new CauseOfTesting { Name = "Routine", IsActive = true };
        db.CausesOfTesting.Add(routine);
        await db.SaveChangesAsync();

        var sample = new Sample
        {
            ReferenceNumber = "FP0107026001",
            Category = SampleCategory.FinishedProduct,
            ItemId = item.Id,
            ControlNumber = "CTRL-1",
            CauseOfTestingId = routine.Id,
            Status = SampleStatus.InTesting,
            PreparationStatus = SamplePreparationStatus.Ready
        };
        sample.TestOrders.Add(new TestOrder { SectionId = fp.Id, TestCode = "ASSAY", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();
        return sample;
    }

    [Fact]
    public async Task AddLab_CreatesTheLabsOrdersOnTheSameSample()
    {
        var (db, item, micro, fp, userId) = await SeedAsync();
        var sample = await SeedFpOnlySampleAsync(db, item, fp);
        var referenceNumber = sample.ReferenceNumber;

        await TestServiceFactory.AddLaboratory(db).AddAsync(sample.Id, micro.Id, userId, Password, "Micro tests also required", null);

        var reloaded = await db.Samples.Include(s => s.TestOrders).AsNoTracking().FirstAsync(s => s.Id == sample.Id);
        Assert.Equal(referenceNumber, reloaded.ReferenceNumber);
        Assert.Contains(reloaded.TestOrders, t => t.TestCode == "TAMC" && t.SectionId == micro.Id);
        Assert.Contains(reloaded.TestOrders, t => t.TestCode == "ASSAY" && t.SectionId == fp.Id);
    }

    [Fact]
    public async Task AddLab_MicroToReadyFpSample_NeedsPreparationAgain()
    {
        var (db, item, micro, fp, userId) = await SeedAsync();
        var sample = await SeedFpOnlySampleAsync(db, item, fp);
        Assert.Equal(SamplePreparationStatus.Ready, sample.PreparationStatus);
        var fpOrderId = sample.TestOrders.Single().Id;

        await TestServiceFactory.AddLaboratory(db).AddAsync(sample.Id, micro.Id, userId, Password, "Micro tests also required", null);

        var reloaded = await db.Samples.Include(s => s.TestOrders).AsNoTracking().FirstAsync(s => s.Id == sample.Id);
        Assert.Equal(SamplePreparationStatus.NeedsPreparation, reloaded.PreparationStatus);
        var fpOrder = reloaded.TestOrders.Single(t => t.Id == fpOrderId);
        Assert.Equal(ApprovalStatus.Pending, fpOrder.Status);
        Assert.Equal(WorkflowStep.Waiting, fpOrder.CurrentStep);
    }

    [Fact]
    public async Task AddLab_ToApprovedSample_ReturnsItToInProgress()
    {
        var (db, item, micro, fp, userId) = await SeedAsync();
        var sample = await SeedFpOnlySampleAsync(db, item, fp);
        sample.Status = SampleStatus.Approved;
        sample.ApprovalDecision = ApprovalDecision.Approve;
        sample.ApprovedAt = DateTime.UtcNow;
        sample.ApprovedByUserId = userId;
        sample.SectionSignoffs.Add(new SampleSectionSignoff { SampleId = sample.Id, SectionId = fp.Id, Status = SectionSignoffStatus.Approved });
        await db.SaveChangesAsync();

        await TestServiceFactory.AddLaboratory(db).AddAsync(sample.Id, micro.Id, userId, Password, "Micro tests also required", null);

        var reloaded = await db.Samples.Include(s => s.TestOrders).Include(s => s.SectionSignoffs).AsNoTracking().FirstAsync(s => s.Id == sample.Id);
        Assert.Equal(SampleStatus.InTesting, reloaded.Status);
        Assert.Equal(OverallSampleStatus.InProgress, SampleSectionRollup.Overall(reloaded));
        Assert.Null(reloaded.ApprovalDecision);
        Assert.Null(reloaded.ApprovedAt);
        Assert.Null(reloaded.ApprovedByUserId);
        // FP's own sign-off stays Approved - only the sample-level roll-up moves.
        Assert.Equal(SectionSignoffStatus.Approved, reloaded.SectionSignoffs.Single(s => s.SectionId == fp.Id).Status);
    }

    [Fact]
    public async Task AddLab_ToRejectedSample_IsRefused()
    {
        var (db, item, micro, fp, userId) = await SeedAsync();
        var sample = await SeedFpOnlySampleAsync(db, item, fp);
        sample.Status = SampleStatus.Rejected;
        sample.SectionSignoffs.Add(new SampleSectionSignoff { SampleId = sample.Id, SectionId = fp.Id, Status = SectionSignoffStatus.Rejected });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => TestServiceFactory.AddLaboratory(db).AddAsync(sample.Id, micro.Id, userId, Password, "Micro tests also required", null));
        Assert.Contains("cannot be added to a rejected, voided or cancelled sample", ex.Message);
    }

    [Fact]
    public async Task AddLab_LabAlreadyOnSample_IsRefused()
    {
        var (db, item, micro, fp, userId) = await SeedAsync();
        var sample = await SeedFpOnlySampleAsync(db, item, fp);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => TestServiceFactory.AddLaboratory(db).AddAsync(sample.Id, fp.Id, userId, Password, "Already testing", null));
        Assert.Contains("already testing the sample", ex.Message);
    }

    [Fact]
    public async Task AddLab_LabWithNoTestsForItem_IsRefused()
    {
        var db = NewDb();
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var fp = EnsureFpSection(db);
        db.TestDefinitions.Add(new TestDefinition { Code = "ASSAY", DisplayName = "Assay", SectionId = fp.Id });

        var item = new Item { Name = "Tablet", Code = "TAB-02", Category = SampleCategory.FinishedProduct, IsActive = true };
        item.AssignedTests.Add(new SampleTest { TestCode = "ASSAY" });
        db.Items.Add(item);

        var role = new Role { Type = RoleType.SystemAdministrator, Name = "SystemAdministrator" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var user = new User { FullName = "Receiver", Username = "receiver2", RoleId = role.Id, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password), IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var sample = await SeedFpOnlySampleAsync(db, item, fp);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => TestServiceFactory.AddLaboratory(db).AddAsync(sample.Id, micro.Id, user.Id, Password, "Add micro", null));
        Assert.Contains("has no tests for Microbiology Laboratory", ex.Message);
    }

    [Fact]
    public async Task AddLab_RecordsSignatureAndEvent()
    {
        var (db, item, micro, fp, userId) = await SeedAsync();
        var sample = await SeedFpOnlySampleAsync(db, item, fp);

        await TestServiceFactory.AddLaboratory(db).AddAsync(sample.Id, micro.Id, userId, Password, "Micro tests also required", null);

        Assert.True(await db.ElectronicSignatures.AnyAsync(s =>
            s.EntityType == ReviewEntityTypes.Sample && s.EntityId == sample.Id && s.MeaningOfSignature == SignatureMeaning.LaboratoryAdded));
        Assert.True(await db.ReviewWorkflowEvents.AnyAsync(e =>
            e.EntityType == ReviewEntityTypes.Sample && e.EntityId == sample.Id &&
            e.EventType == ReviewWorkflowEventType.LaboratoryAdded && e.SectionId == micro.Id));
    }
}
