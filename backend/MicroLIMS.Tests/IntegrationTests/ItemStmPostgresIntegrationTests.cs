using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class ItemStmPostgresIntegrationTests
{
    private const string DownloadKey = "DocumentControl.ControlledCopy.DownloadPermitted";
    private readonly PostgresTestFixture _fixture;

    public ItemStmPostgresIntegrationTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static string NewCode() => $"STM-{Guid.NewGuid():N}"[..14].ToUpperInvariant();

    private static async Task<Item> AddItemAsync(MicroLimsDbContext db, string sop)
    {
        var item = new Item { Code = $"IT-{Guid.NewGuid():N}"[..16], Name = "STM item", SopNumber = sop };
        db.Items.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    private async Task<DocumentMaster> AddMasterAsync(
        MicroLimsDbContext db, string code, DocumentRecordStatus status = DocumentRecordStatus.Active)
    {
        var m = new DocumentMaster
        {
            MicroLimsDocumentId = $"DOC-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
            CompanyDocumentCode = code,
            Title = "Specification of a material",
            DocumentTypeId = _fixture.SeededDocTypeId,
            DepartmentId = _fixture.SeededDepartmentId,
            SectionId = _fixture.SeededSectionId,
            DocumentOwnerUserId = _fixture.SeededUserId,
            CreatedByUserId = _fixture.SeededUserId,
            RecordStatus = status
        };
        db.DocumentMasters.Add(m);
        await db.SaveChangesAsync();
        return m;
    }

    private async Task<DocumentRevision> AddRevisionAsync(
        MicroLimsDbContext db, DocumentMaster m, string number, int sequence, DocumentRevisionStatus status)
    {
        var r = new DocumentRevision
        {
            DocumentMasterId = m.Id,
            RevisionNumber = number,
            RevisionSequence = sequence,
            RevisionStatus = status,
            EffectiveDate = status == DocumentRevisionStatus.Effective ? new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) : null,
            CreatedByUserId = _fixture.SeededUserId
        };
        db.DocumentRevisions.Add(r);
        await db.SaveChangesAsync();
        return r;
    }

    private async Task<RevisionFile> AddPdfAsync(MicroLimsDbContext db, DocumentRevision r, bool isActive = true)
    {
        var f = new RevisionFile
        {
            DocumentRevisionId = r.Id,
            FileRole = FileRole.ControlledPdf,
            FileName = "stm.pdf",
            ContentType = "application/pdf",
            SizeBytes = 10,
            ContentSha256 = "00",
            StorageKey = $"test/{Guid.NewGuid():N}.pdf",
            IsActive = isActive,
            UploadedByUserId = _fixture.SeededUserId
        };
        db.RevisionFiles.Add(f);
        await db.SaveChangesAsync();
        return f;
    }

    // Master with an Effective revision "01" and its controlled PDF.
    private async Task<(DocumentMaster Master, DocumentRevision Rev, RevisionFile Pdf)> AddEffectiveAsync(
        MicroLimsDbContext db, string code, bool pdfActive = true)
    {
        var m = await AddMasterAsync(db, code);
        var r = await AddRevisionAsync(db, m, "01", 1, DocumentRevisionStatus.Effective);
        var pdf = await AddPdfAsync(db, r, pdfActive);
        m.CurrentEffectiveRevisionId = r.Id;
        await db.SaveChangesAsync();
        return (m, r, pdf);
    }

    private static ItemStmService Service(MicroLimsDbContext db) => new(db);

    private async Task<int> AddUserAsync(MicroLimsDbContext db, RoleType type)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Type == type);
        if (role == null)
        {
            role = new Role { Type = type, Name = type.ToString(), IsActive = true };
            db.Roles.Add(role);
            await db.SaveChangesAsync();
        }
        var tag = Guid.NewGuid().ToString("N")[..10];
        var u = new User { FullName = $"U {tag}", Username = $"u_{tag}", PasswordHash = "x", RoleId = role.Id, IsActive = true };
        db.Users.Add(u);
        await db.SaveChangesAsync();
        return u.Id;
    }

    [PostgresFact]
    public async Task BlankSopNumber_IsNotLinked()
    {
        await using var db = _fixture.CreateDbContext();
        var item = await AddItemAsync(db, "  ");

        var r = await Service(db).GetForItemAsync(item.Id, _fixture.SeededUserId);

        Assert.Equal(ItemStmStatus.NotLinked, r.Status);
        Assert.Null(r.DocumentMasterId);
    }

    [PostgresFact]
    public async Task NoMatchingDocument_IsNotFound()
    {
        await using var db = _fixture.CreateDbContext();
        var code = NewCode();
        var item = await AddItemAsync(db, code);

        var r = await Service(db).GetForItemAsync(item.Id, _fixture.SeededUserId);

        Assert.Equal(ItemStmStatus.NotFound, r.Status);
        Assert.Equal(code, r.SopNumber);
        Assert.Null(r.DocumentMasterId);
    }

    [PostgresFact]
    public async Task DraftOnlyDocument_IsNoEffectiveRevision()
    {
        await using var db = _fixture.CreateDbContext();
        var code = NewCode();
        var m = await AddMasterAsync(db, code);
        await AddRevisionAsync(db, m, "01", 1, DocumentRevisionStatus.Draft);
        var item = await AddItemAsync(db, code);

        var r = await Service(db).GetForItemAsync(item.Id, _fixture.SeededUserId);

        Assert.Equal(ItemStmStatus.NoEffectiveRevision, r.Status);
        Assert.Equal(m.Id, r.DocumentMasterId);
        Assert.Equal(code, r.CompanyDocumentCode);
        Assert.Equal("Specification of a material", r.Title);
        Assert.Null(r.RevisionNumber);
        Assert.Null(r.ControlledPdfFileId);
    }

    [PostgresFact]
    public async Task EffectiveRevisionWithPdf_IsEffective()
    {
        await using var db = _fixture.CreateDbContext();
        var code = NewCode();
        var (m, _, pdf) = await AddEffectiveAsync(db, code);
        var item = await AddItemAsync(db, code);

        var r = await Service(db).GetForItemAsync(item.Id, _fixture.SeededUserId);

        Assert.Equal(ItemStmStatus.Effective, r.Status);
        Assert.Equal(m.Id, r.DocumentMasterId);
        Assert.Equal("01", r.RevisionNumber);
        Assert.NotNull(r.EffectiveDate);
        Assert.Equal(pdf.Id, r.ControlledPdfFileId);
        Assert.Equal("stm.pdf", r.ControlledPdfFileName);
        Assert.Equal("SOP", r.DocumentTypeCode);
        Assert.True(r.CanDownload);
    }

    // Review Focus 1
    [PostgresFact]
    public async Task MatchIgnoresCaseAndSpaces()
    {
        await using var db = _fixture.CreateDbContext();
        var code = NewCode();
        await AddEffectiveAsync(db, code);
        var item = await AddItemAsync(db, "  " + code.ToLowerInvariant() + " ");

        var r = await Service(db).GetForItemAsync(item.Id, _fixture.SeededUserId);

        Assert.Equal(ItemStmStatus.Effective, r.Status);
    }

    // Review Focus 2
    [PostgresFact]
    public async Task VoidedDocumentWithSameCode_IsIgnored()
    {
        await using var db = _fixture.CreateDbContext();
        var code = NewCode();
        var (voided, _, _) = await AddEffectiveAsync(db, code);
        voided.RecordStatus = DocumentRecordStatus.Void;
        await db.SaveChangesAsync();
        var active = await AddMasterAsync(db, code);
        await AddRevisionAsync(db, active, "01", 1, DocumentRevisionStatus.Draft);
        var item = await AddItemAsync(db, code);

        var r = await Service(db).GetForItemAsync(item.Id, _fixture.SeededUserId);

        Assert.Equal(ItemStmStatus.NoEffectiveRevision, r.Status);
        Assert.Equal(active.Id, r.DocumentMasterId);
    }

    // Review Focus 3
    [PostgresFact]
    public async Task InactiveControlledPdf_IsNoEffectiveRevision()
    {
        await using var db = _fixture.CreateDbContext();
        var code = NewCode();
        await AddEffectiveAsync(db, code, pdfActive: false);
        var item = await AddItemAsync(db, code);

        var r = await Service(db).GetForItemAsync(item.Id, _fixture.SeededUserId);

        Assert.Equal(ItemStmStatus.NoEffectiveRevision, r.Status);
        Assert.Null(r.ControlledPdfFileId);
    }

    // Review Focus 4
    [PostgresFact]
    public async Task NewerDraftRevision_StillShowsEffective()
    {
        await using var db = _fixture.CreateDbContext();
        var code = NewCode();
        var (m, _, pdf) = await AddEffectiveAsync(db, code);
        var draft = await AddRevisionAsync(db, m, "02", 2, DocumentRevisionStatus.Draft);
        await AddPdfAsync(db, draft);
        var item = await AddItemAsync(db, code);

        var r = await Service(db).GetForItemAsync(item.Id, _fixture.SeededUserId);

        Assert.Equal(ItemStmStatus.Effective, r.Status);
        Assert.Equal("01", r.RevisionNumber);
        Assert.Equal(pdf.Id, r.ControlledPdfFileId);
    }

    private async Task WithDownloadDisabledAsync(MicroLimsDbContext db, Func<Task> body)
    {
        var existing = await db.ConfigurationSettings.FirstOrDefaultAsync(s => s.SettingKey == DownloadKey);
        var previous = existing?.SettingValue;
        if (existing == null)
        {
            existing = new ConfigurationSetting { SettingKey = DownloadKey, SettingValue = "false", DataType = "bool", SettingGroup = "DocumentControl" };
            db.ConfigurationSettings.Add(existing);
        }
        else existing.SettingValue = "false";
        await db.SaveChangesAsync();
        try { await body(); }
        finally
        {
            await using var cleanup = _fixture.CreateDbContext();
            var row = await cleanup.ConfigurationSettings.SingleAsync(s => s.SettingKey == DownloadKey);
            if (previous == null) cleanup.ConfigurationSettings.Remove(row); else row.SettingValue = previous;
            await cleanup.SaveChangesAsync();
        }
    }

    // Review Focus 5
    [PostgresFact]
    public async Task DownloadDisabledForAnalyst_CanDownloadFalse()
    {
        await using var db = _fixture.CreateDbContext();
        var code = NewCode();
        await AddEffectiveAsync(db, code);
        var item = await AddItemAsync(db, code);
        var analystId = await AddUserAsync(db, RoleType.Analyst);

        await WithDownloadDisabledAsync(db, async () =>
        {
            var r = await Service(db).GetForItemAsync(item.Id, analystId);
            Assert.Equal(ItemStmStatus.Effective, r.Status);
            Assert.False(r.CanDownload);
        });
    }

    [PostgresFact]
    public async Task DownloadDisabled_AdminCanDownload()
    {
        await using var db = _fixture.CreateDbContext();
        var code = NewCode();
        await AddEffectiveAsync(db, code);
        var item = await AddItemAsync(db, code);

        await WithDownloadDisabledAsync(db, async () =>
        {
            var r = await Service(db).GetForItemAsync(item.Id, _fixture.SeededUserId);
            Assert.True(r.CanDownload);
        });
    }

    [PostgresFact]
    public async Task UnknownItem_ThrowsNotFound()
    {
        await using var db = _fixture.CreateDbContext();
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).GetForItemAsync(int.MaxValue, _fixture.SeededUserId));
    }
}
