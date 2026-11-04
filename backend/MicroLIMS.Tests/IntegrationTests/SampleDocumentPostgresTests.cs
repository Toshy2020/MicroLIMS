using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Abstractions.Storage;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Postgres-backed: the document row, the sample-scope rule and the audit entry are checked in the real schema.
[Collection("PostgresDatabaseCollection")]
public class SampleDocumentPostgresTests
{
    private const string Password = "Correct-Horse-9";
    private readonly PostgresTestFixture _fixture;

    public SampleDocumentPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed class FailingStorage : IFileStorageService
    {
        public Task<string> SaveAsync(string fileName, byte[] content) => throw new IOException("disk full");
        public Task<byte[]> ReadAsync(string path) => throw new IOException("disk full");
    }

    private sealed record World(MicroLimsDbContext Db, Sample Sample, User MicroHead, User FpHead);

    private static readonly byte[] Pdf = { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };

    private static async Task<User> UserAsync(MicroLimsDbContext db, string uid, int deptId, int sectionId)
    {
        var role = new Role { Type = RoleType.SectionHead, Name = $"SectionHead-{uid}", IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var user = new User { FullName = $"Head {uid}", Username = $"head{uid}".ToLowerInvariant(), RoleId = role.Id, PasswordHash = TestPasswords.Hash(Password), IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, DepartmentId = deptId, SectionId = sectionId });
        await db.SaveChangesAsync();
        return user;
    }

    // A periodic-retest raw material sample for Microbiology: no orders yet, so the access
    // rule has to use PendingTargetSectionIds (the sample-detail rule).
    private async Task<World> ReceiveAsync()
    {
        var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var db = _fixture.CreateDbContext();
        var micro = await db.DocumentSections.FirstAsync(s => s.Code == "MICRO");
        var fp = await db.DocumentSections.FirstAsync(s => s.Code == "FP");
        var code = $"LD{uid}";
        db.TestDefinitions.Add(new TestDefinition { Code = code, DisplayName = $"{code} name", SectionId = micro.Id, IsActive = true });
        var item = new Item { Code = $"RM-{uid}", Name = $"Glycerin {uid}", Category = SampleCategory.RawMaterial, IsActive = true };
        item.AssignedTests.Add(new SampleTest { TestCode = code, DisplayName = $"{code} name" });
        db.Items.Add(item);
        await db.SaveChangesAsync();
        var cause = await db.CausesOfTesting.SingleAsync(c => c.Name == "Periodic retest");
        var microHead = await UserAsync(db, "M" + uid, micro.DepartmentId, micro.Id);
        var fpHead = await UserAsync(db, "F" + uid, fp.DepartmentId, fp.Id);
        var sample = await new ProductWorkflowEngine(db, new ReferenceNumberGenerator(db)).ReceiveAsync(new ItemBasedReceiveRequest(
            item.Id, cause.Id, "10 g", "Analyst", $"LOT-{uid}", $"CTRL-{uid}", DateTime.UtcNow, DateTime.UtcNow.AddYears(1), null,
            _fixture.SeededUserId, new[] { micro.Id }, null));
        return new World(db, sample, microHead, fpHead);
    }

    private static SampleDocumentService Service(MicroLimsDbContext db, IFileStorageService storage) =>
        new(db, storage, new MaterialDocumentFileValidator(26_214_400L),
            new AuditEventService(db, new MicroLIMS.Persistence.Helpers.DatabaseSequenceHelper(db)),
            new UserSectionScopeService(db), NullLogger<SampleDocumentService>.Instance);

    [PostgresFact]
    public async Task Upload_Pdf_StoresRowFileAndAudit()
    {
        var w = await ReceiveAsync();
        await using var db = w.Db;
        var storage = new InMemoryFileStorageService();

        var dto = await Service(db, storage).UploadAsync(w.Sample.Id, "CoA.pdf", "application/pdf", Pdf,
            SampleDocumentType.SupplierCoa, w.MicroHead.Id);

        Assert.Equal(SampleDocumentStatus.Current, dto.Status);
        Assert.Equal(w.MicroHead.FullName, dto.UploadedByName);
        var key = $"sample-documents/{w.Sample.Id}/{dto.Id}.pdf";
        Assert.Equal(Pdf, storage.Files[key]);

        await using var read = _fixture.CreateDbContext();
        var row = await read.SampleDocuments.AsNoTracking().SingleAsync(d => d.Id == dto.Id);
        Assert.Equal(key, row.StorageKey);
        Assert.Equal(SampleDocumentType.SupplierCoa, row.DocumentType);
        Assert.Equal(64, row.ContentSha256.Length);
        var audit = await read.AuditLogs.Include(a => a.Changes).AsNoTracking()
            .SingleAsync(a => a.ActionCode == "SAMPLE_DOCUMENT_UPLOADED" && a.SampleId == w.Sample.Id);
        Assert.Equal(w.MicroHead.Id, audit.UserId);
        Assert.Equal("CoA.pdf", audit.Changes.Single(c => c.FieldName == "Document").NewValue);

        var (meta, content) = await Service(read, storage).ReadContentAsync(dto.Id, w.MicroHead.Id);
        Assert.Equal(Pdf, content);
        Assert.Equal("CoA.pdf", meta.OriginalFileName);
    }

    [PostgresFact]
    public async Task Upload_DisallowedExtension_IsRejected_AndNothingIsStored()
    {
        var w = await ReceiveAsync();
        await using var db = w.Db;
        var storage = new InMemoryFileStorageService();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db, storage).UploadAsync(
            w.Sample.Id, "run.exe", "application/octet-stream", Pdf, SampleDocumentType.SupplierCoa, w.MicroHead.Id));

        Assert.Empty(storage.Files);
        Assert.False(await db.SampleDocuments.AnyAsync(d => d.SampleId == w.Sample.Id));
    }

    [PostgresFact]
    public async Task Upload_StorageFailure_RollsBackTheRow()
    {
        var w = await ReceiveAsync();
        await using var db = w.Db;

        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db, new FailingStorage()).UploadAsync(
            w.Sample.Id, "CoA.pdf", "application/pdf", Pdf, SampleDocumentType.SupplierCoa, w.MicroHead.Id));

        await using var read = _fixture.CreateDbContext();
        Assert.False(await read.SampleDocuments.AnyAsync(d => d.SampleId == w.Sample.Id));
        Assert.False(await read.AuditLogs.AnyAsync(a => a.ActionCode == "SAMPLE_DOCUMENT_UPLOADED" && a.SampleId == w.Sample.Id));
    }

    [PostgresFact]
    public async Task Void_RequiresReason_KeepsTheFile_AndAudits()
    {
        var w = await ReceiveAsync();
        await using var db = w.Db;
        var storage = new InMemoryFileStorageService();
        var service = Service(db, storage);
        var dto = await service.UploadAsync(w.Sample.Id, "CoA.pdf", "application/pdf", Pdf, SampleDocumentType.SupplierCoa, w.MicroHead.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.VoidAsync(dto.Id, "  ", w.MicroHead.Id));

        var voided = await service.VoidAsync(dto.Id, "Wrong supplier", w.MicroHead.Id);
        Assert.Equal(SampleDocumentStatus.Voided, voided.Status);
        Assert.Equal("Wrong supplier", voided.VoidReason);
        Assert.Single(storage.Files);
        // Still readable after the void.
        Assert.Equal(Pdf, (await service.ReadContentAsync(dto.Id, w.MicroHead.Id)).Content);
        Assert.Equal(SampleDocumentStatus.Voided, Assert.Single(await service.ListAsync(w.Sample.Id, w.MicroHead.Id)).Status);

        await using var read = _fixture.CreateDbContext();
        var audit = await read.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.ActionCode == "SAMPLE_DOCUMENT_VOIDED" && a.SampleId == w.Sample.Id);
        Assert.Equal("Wrong supplier", audit.Reason);
        Assert.Equal(w.MicroHead.Id, audit.UserId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.VoidAsync(dto.Id, "again", w.MicroHead.Id));
    }

    [PostgresFact]
    public async Task UserOutsideTheSamplesLabs_CannotListReadUploadOrVoid()
    {
        var w = await ReceiveAsync();
        await using var db = w.Db;
        var storage = new InMemoryFileStorageService();
        var service = Service(db, storage);
        var dto = await service.UploadAsync(w.Sample.Id, "CoA.pdf", "application/pdf", Pdf, SampleDocumentType.SupplierCoa, w.MicroHead.Id);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ListAsync(w.Sample.Id, w.FpHead.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReadContentAsync(dto.Id, w.FpHead.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UploadAsync(
            w.Sample.Id, "x.pdf", "application/pdf", Pdf, SampleDocumentType.Other, w.FpHead.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.VoidAsync(dto.Id, "no", w.FpHead.Id));

        // An unscoped administrator can.
        Assert.Single(await service.ListAsync(w.Sample.Id, _fixture.SeededUserId));
    }

    [PostgresFact]
    public async Task VoidedOrCancelledSample_RefusesUploadAndVoid_ButStillLists()
    {
        var w = await ReceiveAsync();
        await using var db = w.Db;
        var service = Service(db, new InMemoryFileStorageService());
        var dto = await service.UploadAsync(w.Sample.Id, "CoA.pdf", "application/pdf", Pdf, SampleDocumentType.SupplierCoa, w.MicroHead.Id);

        foreach (var status in new[] { SampleStatus.Voided, SampleStatus.Cancelled })
        {
            w.Sample.Status = status;
            await db.SaveChangesAsync();

            var upload = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync(
                w.Sample.Id, "x.pdf", "application/pdf", Pdf, SampleDocumentType.Other, w.MicroHead.Id));
            Assert.Equal("This sample is voided; its documents can no longer be changed.", upload.Message);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.VoidAsync(dto.Id, "late", w.MicroHead.Id));
            Assert.Equal(SampleDocumentStatus.Current, Assert.Single(await service.ListAsync(w.Sample.Id, w.MicroHead.Id)).Status);
        }
    }

    // Voiding a CoA is for Section Head / Supervisor / Administrator, as voiding the sample is (not the Administrator, who never acts on GxP records); uploading stays open to Analysts.
    [Fact]
    public void VoidingADocument_IsClosedToAnalysts_UploadIsNot()
    {
        var voidRoles = MicroLIMS.Tests.ArchitectureTests.AuthorizationMatrixTests.AllowedRoles<MicroLIMS.API.Controllers.SampleDocumentController>(
            nameof(MicroLIMS.API.Controllers.SampleDocumentController.Void))!;
        Assert.DoesNotContain(RoleType.Analyst, voidRoles);
        Assert.Contains(RoleType.Reviewer, voidRoles);
        Assert.Contains(RoleType.SectionHead, voidRoles);
        Assert.DoesNotContain(RoleType.SystemAdministrator, voidRoles); // administrators never act on GxP records

        var uploadRoles = MicroLIMS.Tests.ArchitectureTests.AuthorizationMatrixTests.AllowedRoles<MicroLIMS.API.Controllers.SampleDocumentController>(
            nameof(MicroLIMS.API.Controllers.SampleDocumentController.Upload))!;
        Assert.Contains(RoleType.Analyst, uploadRoles);
    }

    [PostgresFact]
    public async Task Upload_StripsControlCharactersAndQuotesFromTheFileName()
    {
        var w = await ReceiveAsync();
        await using var db = w.Db;

        var dto = await Service(db, new InMemoryFileStorageService()).UploadAsync(w.Sample.Id, "Co\r\nA \"v1\"\t.pdf", "application/pdf", Pdf,
            SampleDocumentType.SupplierCoa, w.MicroHead.Id);

        Assert.Equal("CoA v1.pdf", dto.OriginalFileName);
    }
}
