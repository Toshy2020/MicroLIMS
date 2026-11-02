using Microsoft.EntityFrameworkCore;
using MicroLIMS.Persistence.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Seed;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Segregation of duties (21 CFR Part 11 11.10(d)(g), EU GMP Annex 11 12): the built-in System Administrator
// controls the system - configuration, users, roles - and may read across sections and through the audit
// trail, but never acts on GxP data. These run against the real seeded grants, so they follow the shipped matrix.
public class AdministratorSegregationTests
{
    private sealed record World(
        MicroLimsDbContext Db, User Admin, User Head, User Author, User MicroAnalyst,
        DocumentSection Micro, DocumentSection Fp, DocumentMaster Master, DocumentRevision Draft, RevisionFile SourceFile, RevisionFile Pdf);

    private static World Seed()
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        foreach (var type in Enum.GetValues<RoleType>())
            db.Roles.Add(new Role { Type = type, Name = type.ToString(), IsSystemRole = true, IsActive = true });
        db.SaveChanges();
        DbSeeder.SeedPermissionsAndGrants(db);

        int RoleOf(RoleType kind) => db.Roles.First(r => r.Type == kind).Id;
        User NewUser(string name, RoleType kind) => new() { Username = name, FullName = name, PasswordHash = "x", RoleId = RoleOf(kind), IsActive = true };
        var admin = NewUser("admin", RoleType.SystemAdministrator);
        var head = NewUser("head", RoleType.SectionHead);
        var author = NewUser("author", RoleType.Analyst);
        var analyst = NewUser("micro_analyst", RoleType.Analyst);
        db.Users.AddRange(admin, head, author, analyst);
        db.SaveChanges();

        var micro = TestServiceFactory.EnsureMicroSection(db);
        var fp = new DocumentSection { Name = "Finished Product", Code = "FP", DepartmentId = micro.DepartmentId, IsActive = true };
        db.DocumentSections.Add(fp);
        db.SaveChanges();
        TestServiceFactory.AssignUserToMicroSection(db, head.Id);
        TestServiceFactory.AssignUserToMicroSection(db, analyst.Id);

        var docType = new DocumentType { Code = "SOP", Name = "Standard Operating Procedure", IsActive = true };
        db.DocumentTypes.Add(docType);
        db.SaveChanges();
        var master = new DocumentMaster
        {
            MicroLimsDocumentId = "DOC-0000001", CompanyDocumentCode = "SOP-001", Title = "Segregation SOP",
            DocumentTypeId = docType.Id, DepartmentId = micro.DepartmentId, SectionId = micro.Id,
            DocumentOwnerUserId = author.Id, RecordStatus = DocumentRecordStatus.Active, CreatedByUserId = author.Id
        };
        db.DocumentMasters.Add(master);
        db.SaveChanges();
        var draft = new DocumentRevision
        {
            DocumentMasterId = master.Id, RevisionNumber = "01", RevisionSequence = 1,
            RevisionStatus = DocumentRevisionStatus.Draft, RevisionType = RevisionType.Major, CreatedByUserId = author.Id
        };
        db.DocumentRevisions.Add(draft);
        db.SaveChanges();
        RevisionFile NewFile(FileRole role) => new()
        {
            DocumentRevisionId = draft.Id, FileRole = role, FileName = role + ".bin", ContentType = "application/octet-stream",
            SizeBytes = 1, ContentSha256 = "00", StorageKey = Guid.NewGuid().ToString(), IsActive = true, FileVersion = 1
        };
        var source = NewFile(FileRole.SourceFile);
        var pdf = NewFile(FileRole.ControlledPdf);
        db.RevisionFiles.AddRange(source, pdf);
        db.SaveChanges();

        return new World(db, admin, head, author, analyst, micro, fp, master, draft, source, pdf);
    }

    private static void Withdraw(MicroLimsDbContext db, int roleId, string code)
    {
        db.RolePermissions.Remove(db.RolePermissions.Single(rp => rp.RoleId == roleId && rp.Permission!.Code == code));
        db.SaveChanges();
    }

    [Fact]
    public async Task Administrator_HasNoAuthorityOverADocument_ButReadsTheAuditTrailAndConfigures()
    {
        var w = Seed();
        var auth = new DocumentAuthorizationService(w.Db);

        // Writing: no draft edit, no file upload, no cancel, no void, no registration.
        Assert.False(await auth.CanEditDraftMetadataAsync(w.Master.Id, w.Admin.Id));
        Assert.False(await auth.CanUploadOrReplaceDraftFileAsync(w.Draft.Id, w.Admin.Id));
        Assert.False(await auth.CanCancelDraftRevisionAsync(w.Draft.Id, w.Admin.Id));
        Assert.False(await auth.CanVoidDocumentMasterAsync(w.Master.Id, w.Admin.Id));
        Assert.False(await auth.CanRegisterDocumentMasterAsync(w.Admin.Id));

        // Reading, through the Audit.View the built-in role holds.
        Assert.True(await auth.CanAccessSourceFileAsync(w.SourceFile.Id, w.Admin.Id));
        Assert.True(await auth.CanAccessControlledPdfAsync(w.Pdf.Id, w.Admin.Id));
        Assert.True(await auth.CanQueryGlobalAuditAsync(w.Admin.Id));
        Assert.True(await auth.CanViewRecordAuditAsync(w.Master.Id, w.Admin.Id));

        // Configuration, through Documents.ConfigManage - and nobody else gets it by role.
        Assert.True(await auth.CanManageConfigurationAsync(w.Admin.Id));
        Assert.False(await auth.CanManageConfigurationAsync(w.Head.Id));
        Assert.False(await auth.CanManageConfigurationAsync(w.Author.Id));
    }

    [Fact]
    public async Task SectionHead_DocumentAuthority_IsUnchanged()
    {
        var w = Seed();
        var auth = new DocumentAuthorizationService(w.Db);

        Assert.True(await auth.CanEditDraftMetadataAsync(w.Master.Id, w.Head.Id));
        Assert.True(await auth.CanUploadOrReplaceDraftFileAsync(w.Draft.Id, w.Head.Id));
        Assert.True(await auth.CanCancelDraftRevisionAsync(w.Draft.Id, w.Head.Id));
        Assert.True(await auth.CanVoidDocumentMasterAsync(w.Master.Id, w.Head.Id));
        Assert.True(await auth.CanAccessSourceFileAsync(w.SourceFile.Id, w.Head.Id));
        Assert.True(await auth.CanAccessControlledPdfAsync(w.Pdf.Id, w.Head.Id));
        Assert.True(await auth.CanQueryGlobalAuditAsync(w.Head.Id));
        Assert.True(await auth.CanViewRecordAuditAsync(w.Master.Id, w.Head.Id));

        // An analyst who is neither owner nor assigned author still has none of it.
        Assert.False(await auth.CanEditDraftMetadataAsync(w.Master.Id, w.MicroAnalyst.Id));
        Assert.False(await auth.CanAccessSourceFileAsync(w.SourceFile.Id, w.MicroAnalyst.Id));
        Assert.False(await auth.CanQueryGlobalAuditAsync(w.MicroAnalyst.Id));
    }

    [Fact]
    public async Task TheAdministratorsReadAccessFollowsAuditView_AndConfigurationFollowsConfigManage()
    {
        var w = Seed();
        var auth = new DocumentAuthorizationService(w.Db);
        var roleId = w.Admin.RoleId;

        Withdraw(w.Db, roleId, PermissionConstants.AuditView);
        Assert.False(await auth.CanAccessSourceFileAsync(w.SourceFile.Id, w.Admin.Id));
        Assert.False(await auth.CanAccessControlledPdfAsync(w.Pdf.Id, w.Admin.Id));
        Assert.False(await auth.CanQueryGlobalAuditAsync(w.Admin.Id));
        Assert.False(await auth.CanViewRecordAuditAsync(w.Master.Id, w.Admin.Id));
        Assert.True(await auth.CanManageConfigurationAsync(w.Admin.Id));

        Withdraw(w.Db, roleId, PermissionConstants.DocumentsConfigManage);
        Assert.False(await auth.CanManageConfigurationAsync(w.Admin.Id));
    }

    [Fact]
    public async Task ConfigurationWrites_RequireDocumentsConfigManage()
    {
        var w = Seed();
        var audit = new AuditEventService(w.Db, new MicroLIMS.Persistence.Helpers.DatabaseSequenceHelper(w.Db));
        var config = new DocumentConfigurationService(w.Db, audit, new DocumentAuthorizationService(w.Db));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            config.CreateDocumentTypeAsync(new MicroLIMS.Application.DTOs.DocumentControl.CreateDocumentTypeRequest("WI", "Work Instruction", 24), w.Head.Id));

        var created = await config.CreateDocumentTypeAsync(new MicroLIMS.Application.DTOs.DocumentControl.CreateDocumentTypeRequest("WI", "Work Instruction", 24), w.Admin.Id);
        Assert.Equal("WI", created.Code);
    }

    [Fact]
    public async Task Administrator_ReadsEverySection_ButActsOnNoneItDoesNotBelongTo()
    {
        var w = Seed();
        var fpMaterial = new Material { SectionId = w.Fp.Id, MaterialName = "FP reagent", MaterialType = MaterialType.Chemical, QuantityReceived = 10, QuantityRemaining = 10, BatchNumber = "B1" };
        var microMaterial = new Material { SectionId = w.Micro.Id, MaterialName = "Micro reagent", MaterialType = MaterialType.Chemical, QuantityReceived = 10, QuantityRemaining = 10, BatchNumber = "B2" };
        w.Db.Materials.AddRange(fpMaterial, microMaterial);
        await w.Db.SaveChangesAsync();
        var service = new MaterialService(w.Db, new UserSectionScopeService(w.Db), new DatabaseSequenceHelper(w.Db));

        // Reads: the administrator sees both sections' materials (Sections.ViewAll); the Micro analyst only its own.
        Assert.Equal(2, (await service.GetAllAsync(w.Admin.Id)).Count);
        Assert.Single(await service.GetAllAsync(w.MicroAnalyst.Id));

        // Writes follow membership: the administrator is refused in either section, the member is allowed in its own.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ConsumeAsync(fpMaterial.Id, MaterialType.Chemical, 1m, w.Admin.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ConsumeAsync(microMaterial.Id, MaterialType.Chemical, 1m, w.Admin.Id));
        var consumed = await service.ConsumeAsync(microMaterial.Id, MaterialType.Chemical, 1m, w.MicroAnalyst.Id);
        Assert.Equal(9m, consumed.QuantityRemaining);
        Assert.Equal(10m, fpMaterial.QuantityRemaining);
    }

    [Fact]
    public async Task Notifications_AdministratorSeesTheReviewAndApprovalCounts_OnlyThroughTheDashboardPermissions()
    {
        var w = Seed();
        var micro = TestServiceFactory.EnsureMicroSection(w.Db);
        var review = new Sample { ReferenceNumber = "REV-1", Status = SampleStatus.UnderReview, ReceivedAt = DateTime.UtcNow };
        review.TestOrders.Add(new TestOrder { TestCode = "TAMC", SectionId = micro.Id, Status = ApprovalStatus.ResultEntered, CurrentStep = WorkflowStep.Ready });
        var approval = new Sample { ReferenceNumber = "APP-1", Status = SampleStatus.UnderApproval, ReceivedAt = DateTime.UtcNow };
        approval.TestOrders.Add(new TestOrder { TestCode = "TAMC", SectionId = micro.Id, Status = ApprovalStatus.Reviewed, CurrentStep = WorkflowStep.Reviewed });
        w.Db.Samples.AddRange(review, approval);
        await w.Db.SaveChangesAsync();
        var service = TestServiceFactory.DashboardNotification(w.Db);

        var seen = await service.GetNotificationsAsync(RoleType.SystemAdministrator, w.Admin.Id);
        Assert.Contains(seen, n => n.Type == "ReviewWaiting" && n.Message == "1 sample(s) awaiting review.");
        Assert.Contains(seen, n => n.Type == "ApprovalWaiting" && n.Message == "1 sample(s) awaiting approval.");

        // The role no longer implies them: withdraw the dashboard permissions and they go.
        Withdraw(w.Db, w.Admin.RoleId, PermissionConstants.DashboardsReview);
        Withdraw(w.Db, w.Admin.RoleId, PermissionConstants.DashboardsLabOverview);
        w.Db.NotificationLogs.RemoveRange(w.Db.NotificationLogs);
        await w.Db.SaveChangesAsync();
        var after = await service.GetNotificationsAsync(RoleType.SystemAdministrator, w.Admin.Id);
        Assert.DoesNotContain(after, n => n.Type is "ReviewWaiting" or "ApprovalWaiting");

        // Section Head behaviour is unchanged.
        var head = await service.GetNotificationsAsync(RoleType.SectionHead, w.Head.Id);
        Assert.Contains(head, n => n.Type == "ReviewWaiting");
        Assert.Contains(head, n => n.Type == "ApprovalWaiting");
    }
}
