using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Storage;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class DocumentControlUnitTests
{
    private readonly MicroLimsDbContext _db;
    private readonly IDatabaseSequenceHelper _seqHelper;
    private readonly IAuditEventService _auditService;
    private readonly IDocumentAuthorizationService _authService;
    private readonly InMemoryFileStorageService _storage;
    private readonly DocumentMasterService _masterService;
    private readonly DocumentFileService _fileService;
    private readonly DocumentConfigurationService _configService;
    private readonly DocumentAuditService _docAuditService;

    private readonly int _adminUserId;
    private readonly int _docControllerUserId;
    private readonly int _authorUserId;
    private readonly int _otherUserId;
    private readonly int _docTypeId;
    private readonly int _deptId;
    private readonly int _sectionId;

    public DocumentControlUnitTests()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MicroLimsDbContext(options);
        _seqHelper = new DatabaseSequenceHelper(_db);
        _auditService = new AuditEventService(_db, _seqHelper);
        _authService = new DocumentAuthorizationService(_db);
        _storage = new InMemoryFileStorageService();

        _masterService = new DocumentMasterService(_db, _seqHelper, _auditService, _authService);
        _fileService = new DocumentFileService(_db, _storage, _auditService, _authService);
        _configService = new DocumentConfigurationService(_db, _auditService, _authService);
        _docAuditService = new DocumentAuditService(_db, _auditService, _authService);

        // Seed Roles
        var adminRole = new Role { Type = RoleType.SystemAdministrator, Name = "System Administrator", IsActive = true };
        var controllerRole = new Role { Type = RoleType.SectionHead, Name = "Document Controller", IsActive = true };
        var analystRole = new Role { Type = RoleType.Analyst, Name = "Analyst", IsActive = true };
        _db.Roles.AddRange(adminRole, controllerRole, analystRole);
        _db.SaveChanges();

        // Seed Users
        var adminUser = new User { FullName = "Admin User", Username = "admin", PasswordHash = "hash", RoleId = adminRole.Id, IsActive = true };
        var controllerUser = new User { FullName = "Controller User", Username = "controller", PasswordHash = "hash", RoleId = controllerRole.Id, IsActive = true };
        var authorUser = new User { FullName = "Author User", Username = "author", PasswordHash = "hash", RoleId = analystRole.Id, IsActive = true };
        var otherUser = new User { FullName = "Other User", Username = "other", PasswordHash = "hash", RoleId = analystRole.Id, IsActive = true };
        _db.Users.AddRange(adminUser, controllerUser, authorUser, otherUser);
        _db.SaveChanges();

        _adminUserId = adminUser.Id;
        _docControllerUserId = controllerUser.Id;
        _authorUserId = authorUser.Id;
        _otherUserId = otherUser.Id;

        // Seed Master Data
        var docType = new DocumentType { Code = "SOP", Name = "Standard Operating Procedure", DefaultReviewCycleMonths = 24, IsActive = true };
        _db.DocumentTypes.Add(docType);

        var dept = new DocumentDepartment { Code = "QC", Name = "Quality Control", IsActive = true };
        _db.DocumentDepartments.Add(dept);
        _db.SaveChanges();

        var section = new DocumentSection { DepartmentId = dept.Id, Name = "Microbiology Lab", IsActive = true };
        _db.DocumentSections.Add(section);
        _db.SaveChanges();

        _docTypeId = docType.Id;
        _deptId = dept.Id;
        _sectionId = section.Id;
    }

    [Fact]
    public async Task RegisterDocumentMaster_GeneratesPermanentId_AndInitialDraft()
    {
        var request = new RegisterDocumentMasterRequest(
            CompanyDocumentCode: "SOP-MIC-001",
            Title: "Microbiology Sample Testing SOP",
            DocumentTypeId: _docTypeId,
            DepartmentId: _deptId,
            SectionId: _sectionId,
            DocumentOwnerUserId: _authorUserId,
            Confidentiality: DocumentConfidentiality.Internal,
            Category: "Testing Procedures",
            Keywords: new List<string> { "QC", "Testing", "Microbiology" },
            InitialRevisionNumber: "01"
        );

        var result = await _masterService.RegisterDocumentMasterAsync(request, _authorUserId);

        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.StartsWith("DOC-", result.MicroLimsDocumentId);
        Assert.Equal("SOP-MIC-001", result.CompanyDocumentCode);
        Assert.Equal(DocumentRecordStatus.Active, result.RecordStatus);
        Assert.Equal(RecordOrigin.Native, result.RecordOrigin);
        Assert.Null(result.CurrentEffectiveRevisionId);

        Assert.Single(result.Revisions);
        var initialRev = result.Revisions[0];
        Assert.Equal("01", initialRev.RevisionNumber);
        Assert.Equal(1, initialRev.RevisionSequence);
        Assert.Equal(DocumentRevisionStatus.Draft, initialRev.RevisionStatus);
        Assert.Equal(24, initialRev.ReviewCycleMonths);

        // Verify audit event
        var audit = await _db.AuditLogs.Include(a => a.Changes)
            .FirstOrDefaultAsync(a => a.ActionCode == "DocumentMasterRegistered" && a.DocumentMasterId == result.Id);
        Assert.NotNull(audit);
        Assert.Equal(ActorType.User, audit.ActorType);
        Assert.Equal(_authorUserId, audit.UserId);
        Assert.Contains(audit.Changes, c => c.FieldName == "CompanyDocumentCode" && c.NewValue == "SOP-MIC-001");
    }

    [Fact]
    public async Task RegisterDocumentMaster_DuplicateActiveCompanyCode_ThrowsInvalidOperationException()
    {
        var request1 = new RegisterDocumentMasterRequest(
            CompanyDocumentCode: "SOP-QC-001",
            Title: "Doc 1",
            DocumentTypeId: _docTypeId,
            DepartmentId: _deptId,
            SectionId: _sectionId,
            DocumentOwnerUserId: _authorUserId
        );
        await _masterService.RegisterDocumentMasterAsync(request1, _authorUserId);

        var request2 = new RegisterDocumentMasterRequest(
            CompanyDocumentCode: "sop-qc-001", // case insensitive match
            Title: "Doc 2",
            DocumentTypeId: _docTypeId,
            DepartmentId: _deptId,
            SectionId: _sectionId,
            DocumentOwnerUserId: _authorUserId
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _masterService.RegisterDocumentMasterAsync(request2, _authorUserId);
        });

        Assert.Contains("already exists and is active", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateDraftMetadata_Succeeds_WhenDraftOnly_AndAudited()
    {
        var registered = await _masterService.RegisterDocumentMasterAsync(new RegisterDocumentMasterRequest(
            CompanyDocumentCode: "SOP-QC-010",
            Title: "Original Title",
            DocumentTypeId: _docTypeId,
            DepartmentId: _deptId,
            SectionId: _sectionId,
            DocumentOwnerUserId: _authorUserId
        ), _authorUserId);

        var updateRequest = new UpdateDocumentMasterDraftRequest(
            CompanyDocumentCode: "SOP-QC-010",
            Title: "Updated Title By Owner",
            DocumentTypeId: _docTypeId,
            DepartmentId: _deptId,
            SectionId: _sectionId,
            DocumentOwnerUserId: _authorUserId,
            Confidentiality: DocumentConfidentiality.Restricted,
            Category: "Sterility",
            Keywords: new List<string> { "Sterility", "QC" }
        );

        var updated = await _masterService.UpdateDraftMetadataAsync(registered.Id, updateRequest, _authorUserId);

        Assert.Equal("Updated Title By Owner", updated.Title);
        Assert.Equal(DocumentConfidentiality.Restricted, updated.Confidentiality);
        Assert.Equal("Sterility", updated.Category);
        Assert.Equal(2, updated.Keywords.Count);

        var audit = await _db.AuditLogs.Include(a => a.Changes)
            .FirstOrDefaultAsync(a => a.ActionCode == "DocumentMasterMetadataUpdated" && a.DocumentMasterId == registered.Id);
        Assert.NotNull(audit);
        Assert.Contains(audit.Changes, c => c.FieldName == "Title" && c.PreviousValue == "Original Title" && c.NewValue == "Updated Title By Owner");
    }

    [Fact]
    public async Task UpdateDraftMetadata_Throws_WhenDocumentHasEffectiveRevision()
    {
        var registered = await _masterService.RegisterDocumentMasterAsync(new RegisterDocumentMasterRequest(
            CompanyDocumentCode: "SOP-QC-015",
            Title: "Effective Document",
            DocumentTypeId: _docTypeId,
            DepartmentId: _deptId,
            SectionId: _sectionId,
            DocumentOwnerUserId: _authorUserId
        ), _authorUserId);

        // Simulate document having become effective
        var master = await _db.DocumentMasters.FindAsync(registered.Id);
        master!.CurrentEffectiveRevisionId = registered.Revisions[0].Id;
        await _db.SaveChangesAsync();

        var updateRequest = new UpdateDocumentMasterDraftRequest(
            CompanyDocumentCode: "SOP-QC-015",
            Title: "Illegal Direct Edit",
            DocumentTypeId: _docTypeId,
            DepartmentId: _deptId,
            SectionId: _sectionId,
            DocumentOwnerUserId: _authorUserId,
            Confidentiality: DocumentConfidentiality.Internal
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _masterService.UpdateDraftMetadataAsync(registered.Id, updateRequest, _authorUserId);
        });

        Assert.Contains("cannot be edited directly", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VoidMaster_Succeeds_WhenNeverEffective_AndAudited()
    {
        var registered = await _masterService.RegisterDocumentMasterAsync(new RegisterDocumentMasterRequest(
            CompanyDocumentCode: "SOP-ERR-001",
            Title: "Registered in Error Document",
            DocumentTypeId: _docTypeId,
            DepartmentId: _deptId,
            SectionId: _sectionId,
            DocumentOwnerUserId: _authorUserId
        ), _authorUserId);

        var voidRequest = new VoidDocumentMasterRequest("Document was registered under wrong code and title in error.");
        var voided = await _masterService.VoidMasterAsync(registered.Id, voidRequest, _docControllerUserId);

        Assert.Equal(DocumentRecordStatus.Void, voided.RecordStatus);
        Assert.NotNull(voided.VoidedAt);
        Assert.Equal(_docControllerUserId, voided.VoidedByUserId);
        Assert.Equal(voidRequest.Reason, voided.VoidReason);

        var audit = await _db.AuditLogs.Include(a => a.Changes)
            .FirstOrDefaultAsync(a => a.ActionCode == "DocumentMasterVoided" && a.DocumentMasterId == registered.Id);
        Assert.NotNull(audit);
        Assert.Equal(voidRequest.Reason, audit.Reason);
    }

    [Fact]
    public async Task VoidMaster_Fails_WhenReasonTooShort()
    {
        var registered = await _masterService.RegisterDocumentMasterAsync(new RegisterDocumentMasterRequest(
            CompanyDocumentCode: "SOP-ERR-002",
            Title: "Doc",
            DocumentTypeId: _docTypeId,
            DepartmentId: _deptId,
            SectionId: _sectionId,
            DocumentOwnerUserId: _authorUserId
        ), _authorUserId);

        var voidRequest = new VoidDocumentMasterRequest("short"); // < 10 chars

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _masterService.VoidMasterAsync(registered.Id, voidRequest, _docControllerUserId);
        });
    }

    [Fact]
    public async Task VoidMaster_Fails_WhenCalledByNonDocumentController()
    {
        var registered = await _masterService.RegisterDocumentMasterAsync(new RegisterDocumentMasterRequest(
            CompanyDocumentCode: "SOP-ERR-003",
            Title: "Doc",
            DocumentTypeId: _docTypeId,
            DepartmentId: _deptId,
            SectionId: _sectionId,
            DocumentOwnerUserId: _authorUserId
        ), _authorUserId);

        var voidRequest = new VoidDocumentMasterRequest("Valid reason of 10+ characters.");

        // Author or Admin is rejected per FS-1a-111 (Quality decision, Document Controller only)
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await _masterService.VoidMasterAsync(registered.Id, voidRequest, _authorUserId);
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await _masterService.VoidMasterAsync(registered.Id, voidRequest, _adminUserId);
        });
    }

    [Fact]
    public async Task CancelDraftRevision_Succeeds_AndDeactivatesActiveFiles()
    {
        var registered = await _masterService.RegisterDocumentMasterAsync(new RegisterDocumentMasterRequest(
            CompanyDocumentCode: "SOP-QC-020",
            Title: "Doc with File",
            DocumentTypeId: _docTypeId,
            DepartmentId: _deptId,
            SectionId: _sectionId,
            DocumentOwnerUserId: _authorUserId
        ), _authorUserId);

        var revId = registered.Revisions[0].Id;
        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.7 Test PDF Content");
        await _fileService.UploadRevisionFileAsync(revId, FileRole.ControlledPdf, "test.pdf", "application/pdf", pdfBytes, _authorUserId);

        var cancelRequest = new CancelDraftRevisionRequest("Draft revision is no longer needed.");
        var cancelled = await _masterService.CancelDraftRevisionAsync(revId, cancelRequest, _authorUserId);

        Assert.Equal(DocumentRevisionStatus.Cancelled, cancelled.RevisionStatus);
        Assert.NotNull(cancelled.CancelledAt);
        Assert.Equal(_authorUserId, cancelled.CancelledByUserId);

        // Verify file is marked inactive
        var file = await _db.RevisionFiles.FirstOrDefaultAsync(f => f.DocumentRevisionId == revId);
        Assert.NotNull(file);
        Assert.False(file.IsActive);

        var audit = await _db.AuditLogs
            .FirstOrDefaultAsync(a => a.ActionCode == "DraftRevisionCancelled" && a.DocumentRevisionId == revId);
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task UploadRevisionFile_CalculatesSha256_AndReplacesActiveFile()
    {
        var registered = await _masterService.RegisterDocumentMasterAsync(new RegisterDocumentMasterRequest(
            CompanyDocumentCode: "SOP-QC-030",
            Title: "Doc For Upload",
            DocumentTypeId: _docTypeId,
            DepartmentId: _deptId,
            SectionId: _sectionId,
            DocumentOwnerUserId: _authorUserId
        ), _authorUserId);

        var revId = registered.Revisions[0].Id;

        // 1. Upload File 1
        var pdf1 = Encoding.UTF8.GetBytes("%PDF-1.7 Content Version 1");
        var hash1 = Convert.ToHexString(SHA256.HashData(pdf1));
        var file1 = await _fileService.UploadRevisionFileAsync(revId, FileRole.ControlledPdf, "v1.pdf", "application/pdf", pdf1, _authorUserId);

        Assert.Equal(hash1, file1.ContentSha256);
        Assert.True(file1.IsActive);
        Assert.Null(file1.SupersededByFileId);

        // 2. Upload File 2 (Replacement on draft)
        var pdf2 = Encoding.UTF8.GetBytes("%PDF-1.7 Content Version 2 Updated");
        var hash2 = Convert.ToHexString(SHA256.HashData(pdf2));
        var file2 = await _fileService.UploadRevisionFileAsync(revId, FileRole.ControlledPdf, "v2.pdf", "application/pdf", pdf2, _authorUserId);

        Assert.Equal(hash2, file2.ContentSha256);
        Assert.True(file2.IsActive);

        // Check file 1 is now inactive and superseded by file 2
        var oldFile = await _db.RevisionFiles.FindAsync(file1.Id);
        Assert.False(oldFile!.IsActive);
        Assert.Equal(file2.Id, oldFile.SupersededByFileId);

        var replaceAudit = await _db.AuditLogs.Include(a => a.Changes)
            .FirstOrDefaultAsync(a => a.ActionCode == "RevisionFileReplaced" && a.DocumentRevisionId == revId);
        Assert.NotNull(replaceAudit);
        Assert.Contains(replaceAudit.Changes, c => c.FieldName == "SupersededFileId" && c.PreviousValue == file1.Id.ToString() && c.NewValue == file2.Id.ToString());
    }

    [Fact]
    public async Task UploadRevisionFile_Throws_WhenControlledPdfNotPdf()
    {
        var registered = await _masterService.RegisterDocumentMasterAsync(new RegisterDocumentMasterRequest(
            CompanyDocumentCode: "SOP-QC-040",
            Title: "Doc",
            DocumentTypeId: _docTypeId,
            DepartmentId: _deptId,
            SectionId: _sectionId,
            DocumentOwnerUserId: _authorUserId
        ), _authorUserId);

        var revId = registered.Revisions[0].Id;

        // Fake PDF with wrong extension
        var bytes = Encoding.UTF8.GetBytes("%PDF-1.7 Content");
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _fileService.UploadRevisionFileAsync(revId, FileRole.ControlledPdf, "document.docx", "application/pdf", bytes, _authorUserId);
        });

        // Fake PDF with .pdf extension but invalid header bytes
        var invalidBytes = Encoding.UTF8.GetBytes("NOT A VALID PDF HEADER");
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _fileService.UploadRevisionFileAsync(revId, FileRole.ControlledPdf, "document.pdf", "application/pdf", invalidBytes, _authorUserId);
        });
    }

    [Fact]
    public async Task GetFileContent_VerifiesSha256_ThrowsOnIntegrityFailure()
    {
        var registered = await _masterService.RegisterDocumentMasterAsync(new RegisterDocumentMasterRequest(
            CompanyDocumentCode: "SOP-QC-050",
            Title: "Doc Integrity",
            DocumentTypeId: _docTypeId,
            DepartmentId: _deptId,
            SectionId: _sectionId,
            DocumentOwnerUserId: _authorUserId
        ), _authorUserId);

        var revId = registered.Revisions[0].Id;
        var validBytes = Encoding.UTF8.GetBytes("%PDF-1.7 Original Unaltered Content");
        var fileDto = await _fileService.UploadRevisionFileAsync(revId, FileRole.ControlledPdf, "original.pdf", "application/pdf", validBytes, _authorUserId);

        // Setup storage to return tampered bytes
        var tamperedBytes = Encoding.UTF8.GetBytes("%PDF-1.7 TAMPERED CORRUPTED CONTENT");
        var savedFile = await _db.RevisionFiles.FindAsync(fileDto.Id);
        _storage.Files[savedFile!.StorageKey] = tamperedBytes;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _fileService.GetFileContentAsync(fileDto.Id, _authorUserId);
        });

        Assert.Contains("integrity check failed", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Verify critical audit event was logged
        var audit = await _db.AuditLogs
            .FirstOrDefaultAsync(a => a.ActionCode == "FileIntegrityVerificationFailed" && a.EntityId == fileDto.Id.ToString());
        Assert.NotNull(audit);
        Assert.Equal(AuditActionCategory.Security, audit.ActionCategory);
    }

    [Fact]
    public async Task DocumentAuthorization_OwnerAndAssignedAuthor_GrantedDraftAccess()
    {
        var registered = await _masterService.RegisterDocumentMasterAsync(new RegisterDocumentMasterRequest(
            CompanyDocumentCode: "SOP-QC-060",
            Title: "Auth Check Doc",
            DocumentTypeId: _docTypeId,
            DepartmentId: _deptId,
            SectionId: _sectionId,
            DocumentOwnerUserId: _authorUserId
        ), _authorUserId);

        // Owner can edit draft
        var ownerCanEdit = await _authService.CanEditDraftMetadataAsync(registered.Id, _authorUserId);
        Assert.True(ownerCanEdit);

        // Other unassigned user cannot edit draft
        var otherCanEdit = await _authService.CanEditDraftMetadataAsync(registered.Id, _otherUserId);
        Assert.False(otherCanEdit);

        // Assign other user as Author
        await _masterService.AddOrUpdateAssignmentAsync(registered.Id, new CreateAssignmentRequest(_otherUserId, AssignmentRole.Author), _adminUserId);

        // Now other user (assigned Author) can edit draft
        var assignedAuthorCanEdit = await _authService.CanEditDraftMetadataAsync(registered.Id, _otherUserId);
        Assert.True(assignedAuthorCanEdit);
    }

    [Fact]
    public async Task DocumentLibrary_SearchAndFilter_ReturnsMatchingMasters()
    {
        await _masterService.RegisterDocumentMasterAsync(new RegisterDocumentMasterRequest(
            CompanyDocumentCode: "SOP-MIC-100",
            Title: "Microbiology Growth Promotion Testing",
            DocumentTypeId: _docTypeId,
            DepartmentId: _deptId,
            SectionId: _sectionId,
            DocumentOwnerUserId: _authorUserId,
            Keywords: new List<string> { "Growth", "Media" }
        ), _authorUserId);

        await _masterService.RegisterDocumentMasterAsync(new RegisterDocumentMasterRequest(
            CompanyDocumentCode: "SOP-ENV-200",
            Title: "Environmental Air Monitoring SOP",
            DocumentTypeId: _docTypeId,
            DepartmentId: _deptId,
            SectionId: _sectionId,
            DocumentOwnerUserId: _authorUserId,
            Keywords: new List<string> { "Cleanroom", "Air" }
        ), _authorUserId);

        // Search by keyword "Media"
        var mediaResults = await _masterService.GetLibraryAsync(new DocumentLibraryFilterRequest { SearchTerm = "Media" }, _authorUserId);
        Assert.Single(mediaResults.Items);
        Assert.Equal("SOP-MIC-100", mediaResults.Items[0].CompanyDocumentCode);

        // Search by code "ENV"
        var envResults = await _masterService.GetLibraryAsync(new DocumentLibraryFilterRequest { SearchTerm = "ENV" }, _authorUserId);
        Assert.Single(envResults.Items);
        Assert.Equal("SOP-ENV-200", envResults.Items[0].CompanyDocumentCode);
    }

    [Fact]
    public async Task ConfigurationService_TypesAndDepartments_AuditedAndEnforcesNoHardDelete()
    {
        // 1. Create Document Type
        var typeDto = await _configService.CreateDocumentTypeAsync(new CreateDocumentTypeRequest("VAL", "Validation Protocol", 36), _adminUserId);
        Assert.Equal("VAL", typeDto.Code);
        Assert.True(typeDto.IsActive);

        // 2. Deactivate Document Type (Soft deactivation instead of delete)
        var updated = await _configService.UpdateDocumentTypeAsync(typeDto.Id, new UpdateDocumentTypeRequest("Validation Protocol", 36, IsActive: false), _adminUserId);
        Assert.False(updated.IsActive);

        // Verify in DB - record still exists!
        var inDb = await _db.DocumentTypes.FindAsync(typeDto.Id);
        Assert.NotNull(inDb);
        Assert.False(inDb.IsActive);

        // Verify audit log captured
        var audit = await _db.AuditLogs.Include(a => a.Changes)
            .FirstOrDefaultAsync(a => a.ActionCode == "DocumentTypeUpdated" && a.EntityId == typeDto.Id.ToString());
        Assert.NotNull(audit);
        Assert.Contains(audit.Changes, c => c.FieldName == "IsActive" && c.PreviousValue == "True" && c.NewValue == "False");
    }
}
