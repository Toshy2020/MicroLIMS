using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class DocumentMasterSectionsPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public DocumentMasterSectionsPostgresIntegrationTests(PostgresTestFixture fixture) => _fixture = fixture;

    private DocumentMasterService CreateService(MicroLimsDbContext db)
    {
        var seq = new DatabaseSequenceHelper(db);
        return new DocumentMasterService(db, seq, new AuditEventService(db, seq), new DocumentAuthorizationService(db));
    }

    private static async Task<DocumentSection> AddSectionAsync(MicroLimsDbContext db, int departmentId)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var s = new DocumentSection { DepartmentId = departmentId, Name = $"Sec {tag}", Code = tag, IsActive = true };
        db.DocumentSections.Add(s);
        await db.SaveChangesAsync();
        return s;
    }

    private RegisterDocumentMasterRequest Register(string code, List<int>? sectionIds) => new(
        CompanyDocumentCode: code,
        Title: "Multi-section document",
        DocumentTypeId: _fixture.SeededDocTypeId,
        DepartmentId: _fixture.SeededDepartmentId,
        SectionId: _fixture.SeededSectionId,
        DocumentOwnerUserId: _fixture.SeededControllerUserId,
        SectionIds: sectionIds);

    [PostgresFact]
    public async Task Register_WithTwoSections_StoresBothIncludingPrimaryAndAudits()
    {
        await using var db = _fixture.CreateDbContext();
        var svc = CreateService(db);
        var extra = await AddSectionAsync(db, _fixture.SeededDepartmentId);

        var dto = await svc.RegisterDocumentMasterAsync(
            Register($"MS-{Guid.NewGuid():N}"[..15], new List<int> { extra.Id, _fixture.SeededSectionId }), _fixture.SeededControllerUserId);

        var rows = await db.DocumentMasterSections.Where(s => s.DocumentMasterId == dto.Id).Select(s => s.SectionId).ToListAsync();
        Assert.Equal(new[] { _fixture.SeededSectionId, extra.Id }.OrderBy(i => i), rows.OrderBy(i => i));
        Assert.Equal(_fixture.SeededSectionId, dto.SectionIds[0]); // primary first
        Assert.Equal(new[] { _fixture.SeededSectionId, extra.Id }, dto.SectionIds);
        Assert.Equal(2, dto.SectionNames.Count);

        var audit = await db.AuditLogs.Include(a => a.Changes)
            .FirstAsync(a => a.ActionCode == "DocumentMasterRegistered" && a.DocumentMasterId == dto.Id);
        Assert.Contains(audit.Changes, c => c.FieldName == "SectionIds" && c.PreviousValue == null
            && c.NewValue == string.Join(",", new[] { _fixture.SeededSectionId, extra.Id }.OrderBy(i => i)));
    }

    [PostgresFact]
    public async Task Register_WithSectionFromAnotherDepartment_IsRejected()
    {
        await using var db = _fixture.CreateDbContext();
        var svc = CreateService(db);
        var otherDept = new DocumentDepartment { Code = Guid.NewGuid().ToString("N")[..8], Name = $"Other {Guid.NewGuid():N}", IsActive = true };
        db.DocumentDepartments.Add(otherDept);
        await db.SaveChangesAsync();
        var foreign = await AddSectionAsync(db, otherDept.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.RegisterDocumentMasterAsync(Register($"MS-{Guid.NewGuid():N}"[..15], new List<int> { foreign.Id }), _fixture.SeededControllerUserId));
        Assert.Contains("does not belong to department", ex.Message);
    }

    [PostgresFact]
    public async Task UpdateDraftMetadata_ReplacesSectionSetAndAuditsOnlyWhenChanged()
    {
        await using var db = _fixture.CreateDbContext();
        var svc = CreateService(db);
        var a = await AddSectionAsync(db, _fixture.SeededDepartmentId);
        var b = await AddSectionAsync(db, _fixture.SeededDepartmentId);
        var dto = await svc.RegisterDocumentMasterAsync(
            Register($"MS-{Guid.NewGuid():N}"[..15], new List<int> { a.Id }), _fixture.SeededControllerUserId);

        UpdateDocumentMasterDraftRequest Update(List<int>? ids) => new(
            dto.CompanyDocumentCode, dto.Title, dto.DocumentTypeId, dto.DepartmentId, dto.SectionId,
            dto.DocumentOwnerUserId, dto.Confidentiality, SectionIds: ids);

        var updated = await svc.UpdateDraftMetadataAsync(dto.Id, Update(new List<int> { b.Id }), _fixture.SeededControllerUserId);

        Assert.Equal(new[] { _fixture.SeededSectionId, b.Id }.OrderBy(i => i), updated.SectionIds.OrderBy(i => i));
        var rows = await db.DocumentMasterSections.AsNoTracking().Where(s => s.DocumentMasterId == dto.Id).Select(s => s.SectionId).ToListAsync();
        Assert.DoesNotContain(a.Id, rows);

        var audits = await db.AuditLogs.Include(x => x.Changes)
            .Where(x => x.ActionCode == "DocumentMasterMetadataUpdated" && x.DocumentMasterId == dto.Id).ToListAsync();
        Assert.Single(audits);
        Assert.Contains(audits[0].Changes, c => c.FieldName == "SectionIds"
            && c.PreviousValue == string.Join(",", new[] { _fixture.SeededSectionId, a.Id }.OrderBy(i => i))
            && c.NewValue == string.Join(",", new[] { _fixture.SeededSectionId, b.Id }.OrderBy(i => i)));

        // Same set again -> no new audit event.
        await svc.UpdateDraftMetadataAsync(dto.Id, Update(new List<int> { b.Id }), _fixture.SeededControllerUserId);
        var after = await db.AuditLogs.CountAsync(x => x.ActionCode == "DocumentMasterMetadataUpdated" && x.DocumentMasterId == dto.Id);
        Assert.Equal(1, after);
    }

    [PostgresFact]
    public async Task Library_SectionFilter_FindsDocumentByNonPrimarySection()
    {
        await using var db = _fixture.CreateDbContext();
        var svc = CreateService(db);
        var extra = await AddSectionAsync(db, _fixture.SeededDepartmentId);
        var dto = await svc.RegisterDocumentMasterAsync(
            Register($"MS-{Guid.NewGuid():N}"[..15], new List<int> { extra.Id }), _fixture.SeededControllerUserId);

        var res = await svc.GetLibraryAsync(new DocumentLibraryFilterRequest { SectionId = extra.Id }, _fixture.SeededControllerUserId);

        var item = Assert.Single(res.Items);
        Assert.Equal(dto.Id, item.Id);
        Assert.Contains(extra.Id, item.SectionIds);
    }
}
