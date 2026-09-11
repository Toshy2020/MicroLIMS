using System.Text;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Storage;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class DocumentControlRevisionPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public DocumentControlRevisionPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<(int AuthorId, int ReviewerId, int ControllerId, int MasterId, int EffectiveRevId)> SeedEffectiveDocumentAsync(MicroLimsDbContext db)
    {
        var analystRole = await db.Roles.FirstOrDefaultAsync(r => r.Type == RoleType.Analyst);
        if (analystRole == null)
        {
            analystRole = new Role { Type = RoleType.Analyst, Name = "Analyst", IsActive = true };
            db.Roles.Add(analystRole);
            await db.SaveChangesAsync();
        }

        var author = new User
        {
            FullName = "Postgres Author",
            Username = $"author_{Guid.NewGuid():N}".Substring(0, 15),
            PasswordHash = "x",
            RoleId = analystRole.Id,
            IsActive = true
        };
        var reviewer = new User
        {
            FullName = "Postgres Reviewer",
            Username = $"rev_{Guid.NewGuid():N}".Substring(0, 15),
            PasswordHash = "x",
            RoleId = analystRole.Id,
            IsActive = true
        };
        db.Users.AddRange(author, reviewer);
        await db.SaveChangesAsync();

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var masterService = new DocumentMasterService(db, seqHelper, audit, auth);
        var storage = new InMemoryFileStorageService();
        var fileService = new DocumentFileService(db, storage, audit, auth);

        var req = new RegisterDocumentMasterRequest(
            CompanyDocumentCode: $"SOP-REV-{Guid.NewGuid():N}".Substring(0, 16),
            Title: "Postgres Effective Document for Revision Testing",
            DocumentTypeId: _fixture.SeededDocTypeId,
            DepartmentId: _fixture.SeededDepartmentId,
            SectionId: _fixture.SeededSectionId,
            DocumentOwnerUserId: author.Id,
            Confidentiality: DocumentConfidentiality.Internal,
            Category: "Testing",
            Keywords: new List<string> { "Postgres", "Revision" },
            InitialRevisionNumber: "01",
            ReviewCycleMonths: 24
        );

        var masterDto = await masterService.RegisterDocumentMasterAsync(req, author.Id);
        var revId = masterDto.Revisions[0].Id;

        // Attach controlled PDF
        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4 Baseline PDF Content");
        await fileService.UploadRevisionFileAsync(
            revId,
            FileRole.ControlledPdf,
            "sop_baseline_v1.pdf",
            "application/pdf",
            pdfBytes,
            author.Id);

        // Advance revision to Effective to simulate an active SOP
        var revInDb = await db.DocumentRevisions.FindAsync(revId);
        revInDb!.RevisionStatus = DocumentRevisionStatus.Effective;
        revInDb.EffectiveDate = DateTime.UtcNow.AddMonths(-1);
        revInDb.NextReviewDate = DateTime.UtcNow.AddMonths(23);

        var masterInDb = await db.DocumentMasters.FindAsync(masterDto.Id);
        masterInDb!.CurrentEffectiveRevisionId = revId;

        await db.SaveChangesAsync();

        return (author.Id, reviewer.Id, _fixture.SeededControllerUserId, masterDto.Id, revId);
    }

    [PostgresFact]
    public async Task Postgres_CreateRevision_FromEffective_EnforcesSequenceAndForeignKeys()
    {
        await using var db = _fixture.CreateDbContext();
        var (authorId, _, controllerId, masterId, effectiveRevId) = await SeedEffectiveDocumentAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var revisionService = new DocumentRevisionService(db, audit, auth);

        // 1. Create new revision from effective
        var createReq = new CreateRevisionRequest(
            RevisionType: RevisionType.Major,
            ReasonForRevision: "Upgrading sample test parameters for ISO compliance.",
            ChangeSummary: "Added test specifications for water sampling points.",
            ChangeReference: "CR-2026-001"
        );

        var newRev = await revisionService.CreateRevisionFromEffectiveAsync(masterId, createReq, authorId);

        Assert.NotNull(newRev);
        Assert.Equal("02", newRev.RevisionNumber);
        Assert.Equal(2, newRev.RevisionSequence);
        Assert.Equal(DocumentRevisionStatus.Draft, newRev.RevisionStatus);

        // 2. Query Postgres database directly
        var masterInDb = await db.DocumentMasters
            .Include(m => m.Revisions)
            .FirstOrDefaultAsync(m => m.Id == masterId);

        Assert.NotNull(masterInDb);
        Assert.Equal(effectiveRevId, masterInDb.CurrentEffectiveRevisionId); // Unchanged!
        Assert.Equal(2, masterInDb.Revisions.Count);

        var effectiveInDb = masterInDb.Revisions.First(r => r.Id == effectiveRevId);
        Assert.Equal(DocumentRevisionStatus.Effective, effectiveInDb.RevisionStatus);

        var draftInDb = masterInDb.Revisions.First(r => r.Id == newRev.Id);
        Assert.Equal(DocumentRevisionStatus.Draft, draftInDb.RevisionStatus);
        Assert.Equal("CR-2026-001", draftInDb.ChangeReference);

        // 3. Verify audit log in Postgres
        var auditLogs = await db.AuditLogs
            .Where(a => a.DocumentRevisionId == newRev.Id)
            .ToListAsync();
        Assert.Contains(auditLogs, a => a.ActionCode == "DocumentRevisionCreated");
    }

    [PostgresFact]
    public async Task Postgres_StructuredChangeItems_AndImpactAssessment_PersistsAndAudits()
    {
        await using var db = _fixture.CreateDbContext();
        var (authorId, reviewerId, _, masterId, _) = await SeedEffectiveDocumentAsync(db);

        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var revisionService = new DocumentRevisionService(db, audit, auth);
        var reviewService = new DocumentReviewService(db, audit, auth);

        var newRev = await revisionService.CreateRevisionFromEffectiveAsync(masterId, new CreateRevisionRequest(
            RevisionType: RevisionType.Minor,
            ReasonForRevision: "Clarifying sample preservation times.",
            ChangeReference: "CR-2026-002"
        ), authorId);

        Assert.Equal("01.1", newRev.RevisionNumber);

        // 1. Add structured change item
        var changeItem = await revisionService.AddChangeItemAsync(newRev.Id, new AddChangeItemRequest(
            SectionNumber: "5.1",
            SectionTitle: "Sample Transport",
            DescriptionOfChange: "Transport window extended from 24h to 48h.",
            ChangeRationale: "Validation study VAL-2026-01.",
            ChangeCategory: "Modification"
        ), authorId);

        Assert.NotNull(changeItem);

        // 2. Save impact assessment
        var impactReq = new SaveImpactAssessmentRequest(
            ProcedureOrMethodImpact: true, ProcedureOrMethodDetails: "Transport instructions modified.",
            TrainingImpact: false, TrainingDetails: null,
            FormsOrTemplatesImpact: false, FormsOrTemplatesDetails: null,
            SpecificationsImpact: false, SpecificationsDetails: null,
            EquipmentImpact: false, EquipmentDetails: null,
            MaterialsOrMediaImpact: false, MaterialsOrMediaDetails: null,
            ValidationImpact: true, ValidationDetails: "Refer to validation protocol VAL-2026-01.",
            RegulatoryCommitmentImpact: false, RegulatoryCommitmentDetails: null,
            RelatedDocumentsImpact: false, RelatedDocumentsDetails: null
        );

        var impact = await revisionService.SaveImpactAssessmentAsync(newRev.Id, impactReq, authorId);
        Assert.NotNull(impact);
        Assert.True(impact.IsComplete);

        // 3. Verify in live database
        var itemInDb = await db.RevisionChangeItems.FindAsync(changeItem.Id);
        Assert.NotNull(itemInDb);
        Assert.Equal("5.1", itemInDb.SectionNumber);

        var impactInDb = await db.RevisionImpactAssessments.FirstOrDefaultAsync(a => a.DocumentRevisionId == newRev.Id);
        Assert.NotNull(impactInDb);
        Assert.True(impactInDb.IsComplete);
        Assert.Equal("Transport instructions modified.", impactInDb.ProcedureOrMethodDetails);

        // 4. Attach controlled PDF and submit for review
        var storage = new InMemoryFileStorageService();
        var fileService = new DocumentFileService(db, storage, audit, auth);
        await fileService.UploadRevisionFileAsync(
            newRev.Id,
            FileRole.ControlledPdf,
            "sop_v01_1.pdf",
            "application/pdf",
            Encoding.UTF8.GetBytes("%PDF-1.4 Revision 01.1"),
            authorId);

        var reviewTask = await reviewService.SubmitForReviewAsync(newRev.Id, new SubmitForReviewRequest
        {
            ReviewerUserId = reviewerId
        }, authorId);

        Assert.NotNull(reviewTask);
        Assert.Equal(ReviewTaskStatus.Pending, reviewTask.Status);
    }
}
