using MicroLIMS.Application.Services;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class ItemSopResolverPostgresTests(PostgresTestFixture fixture)
{
    [PostgresFact]
    public async Task ResolveMany_reports_each_status_and_revision_metadata()
    {
        int blankId, missingId, draftId, effectiveId, currentId, pdfId;
        string missingCode, draftCode, effectiveCode;
        await using (var arrange = fixture.CreateDbContext())
        {
            blankId = (await AddItemAsync(arrange, "   ")).Id;
            missingCode = NewCode();
            missingId = (await AddItemAsync(arrange, missingCode)).Id;
            draftCode = NewCode();
            var draft = await AddMasterAsync(arrange, fixture, draftCode);
            await AddRevisionAsync(arrange, fixture, draft, 1, DocumentRevisionStatus.Draft, current: true);
            draftId = (await AddItemAsync(arrange, draftCode)).Id;

            effectiveCode = NewCode();
            var effective = await AddMasterAsync(arrange, fixture, effectiveCode);
            var revision = await AddRevisionAsync(arrange, fixture, effective, 2, DocumentRevisionStatus.Effective, current: true);
            currentId = revision.Id;
            await AddPdfAsync(arrange, fixture, revision, 1, false);
            pdfId = (await AddPdfAsync(arrange, fixture, revision, 2, true)).Id;
            await AddPdfAsync(arrange, fixture, revision, 3, false);
            effectiveId = (await AddItemAsync(arrange, $"  {effectiveCode.ToLowerInvariant()}  ")).Id;
        }

        await using var act = fixture.CreateDbContext();
        var results = await new ItemSopResolver(act)
            .ResolveManyAsync([blankId, missingId, draftId, effectiveId, effectiveId]);

        Assert.Equal(4, results.Count);
        Assert.Equal(SopResolutionStatus.NoSopNumber, results[blankId].Status);
        Assert.Equal(SopResolutionStatus.NotFound, results[missingId].Status);
        Assert.Equal(missingCode, results[missingId].SopNumber);
        Assert.Equal(SopResolutionStatus.NoEffectiveRevision, results[draftId].Status);
        Assert.Equal(draftCode, results[draftId].DocumentCode);
        Assert.Null(results[draftId].RevisionId);
        var resolved = results[effectiveId];
        Assert.Equal(SopResolutionStatus.Effective, resolved.Status);
        Assert.Equal(effectiveCode.ToLowerInvariant(), resolved.SopNumber);
        Assert.Equal(effectiveCode, resolved.DocumentCode);
        Assert.Equal("SOP", resolved.TypeCode);
        Assert.Equal(currentId, resolved.RevisionId);
        Assert.Equal(2, resolved.RevisionSequence);
        Assert.Equal(pdfId, resolved.PdfFileId);
        Assert.Empty(act.ChangeTracker.Entries());
    }

    [PostgresFact]
    public async Task Item_STM_case_insensitive_match_returns_effective_revision()
    {
        int itemId;
        var code = NewCode();
        await using (var arrange = fixture.CreateDbContext())
        {
            var master = await AddMasterAsync(arrange, fixture, code);
            var revision = await AddRevisionAsync(arrange, fixture, master, 1, DocumentRevisionStatus.Effective, current: true);
            await AddPdfAsync(arrange, fixture, revision, 1, true);
            itemId = (await AddItemAsync(arrange, code.ToLowerInvariant())).Id;
        }

        await using var act = fixture.CreateDbContext();
        var actual = await new ItemStmService(act).GetForItemAsync(itemId, fixture.SeededUserId);

        await using var verify = fixture.CreateDbContext();
        var expected = await verify.DocumentMasters.AsNoTracking()
            .Where(m => m.RecordStatus == DocumentRecordStatus.Active && m.CompanyDocumentCode.ToUpper() == code)
            .Select(m => new
            {
                m.Id, m.CompanyDocumentCode, m.Title, TypeCode = m.DocumentType.Code,
                Revision = m.CurrentEffectiveRevision == null
                    || m.CurrentEffectiveRevision.RevisionStatus != DocumentRevisionStatus.Effective
                    ? null
                    : new
                    {
                        m.CurrentEffectiveRevision.RevisionNumber, m.CurrentEffectiveRevision.EffectiveDate,
                        Pdf = m.CurrentEffectiveRevision.Files
                            .Where(f => f.FileRole == FileRole.ControlledPdf && f.IsActive)
                            .OrderByDescending(f => f.FileVersion)
                            .Select(f => new { f.Id, f.FileName }).FirstOrDefault()
                    }
            }).FirstAsync();
        Assert.NotNull(expected.Revision);
        Assert.NotNull(expected.Revision.Pdf);
        Assert.Equal(new ItemStmResponse(
            ItemStmStatus.Effective,
            code.ToLowerInvariant(), expected.Id, expected.CompanyDocumentCode, expected.Title, expected.TypeCode,
            expected.Revision.RevisionNumber, expected.Revision.EffectiveDate,
            expected.Revision.Pdf.Id, expected.Revision.Pdf.FileName, true), actual);
    }

    internal static string NewCode() => $"SOP-{Guid.NewGuid():N}"[..16].ToUpperInvariant();

    internal static async Task<Item> AddItemAsync(MicroLimsDbContext db, string? sop)
    {
        var item = new Item { Code = NewCode(), Name = "SOP-linked item", SopNumber = sop ?? string.Empty };
        db.Items.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    internal static async Task<DocumentMaster> AddMasterAsync(
        MicroLimsDbContext db, PostgresTestFixture fixture, string code)
    {
        var master = new DocumentMaster
        {
            MicroLimsDocumentId = NewCode(), CompanyDocumentCode = code, Title = code,
            DocumentTypeId = fixture.SeededDocTypeId, DepartmentId = fixture.SeededDepartmentId,
            SectionId = fixture.SeededSectionId, DocumentOwnerUserId = fixture.SeededUserId,
            CreatedByUserId = fixture.SeededUserId, RecordStatus = DocumentRecordStatus.Active
        };
        db.DocumentMasters.Add(master);
        await db.SaveChangesAsync();
        return master;
    }

    internal static async Task<DocumentRevision> AddRevisionAsync(
        MicroLimsDbContext db, PostgresTestFixture fixture, DocumentMaster master,
        int sequence, DocumentRevisionStatus status, bool current = false)
    {
        var revision = new DocumentRevision
        {
            DocumentMasterId = master.Id, RevisionSequence = sequence, RevisionNumber = sequence.ToString(),
            RevisionStatus = status, CreatedByUserId = fixture.SeededUserId,
            EffectiveDate = status == DocumentRevisionStatus.Draft ? null : new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        db.DocumentRevisions.Add(revision);
        await db.SaveChangesAsync();
        if (current)
        {
            master.CurrentEffectiveRevisionId = revision.Id;
            await db.SaveChangesAsync();
        }
        return revision;
    }

    private static async Task<RevisionFile> AddPdfAsync(
        MicroLimsDbContext db, PostgresTestFixture fixture, DocumentRevision revision, int version, bool active)
    {
        var file = new RevisionFile
        {
            DocumentRevisionId = revision.Id, FileRole = FileRole.ControlledPdf, FileVersion = version,
            FileName = $"stm-{version}.pdf", ContentType = "application/pdf", SizeBytes = 10, ContentSha256 = "00",
            StorageKey = $"test/{Guid.NewGuid():N}.pdf", IsActive = active, UploadedByUserId = fixture.SeededUserId
        };
        db.RevisionFiles.Add(file);
        await db.SaveChangesAsync();
        return file;
    }
}
