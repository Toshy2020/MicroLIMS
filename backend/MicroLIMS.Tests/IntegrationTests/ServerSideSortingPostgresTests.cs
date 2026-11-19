using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class ServerSideSortingPostgresTests
{
    private readonly PostgresTestFixture _fixture;

    public ServerSideSortingPostgresTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed record SampleSeedData(
        string Prefix,
        int FpSectionId,
        int FpOnlyUserId,
        List<int> FpIdsAscendingControl,
        List<int> FpIdsAscendingDisplayName,
        List<int> FpIdsDefaultOrder,
        List<int> TieIdsAsc,
        int MicroOnlySampleId);

    private sealed record DocumentSeedData(
        string Prefix,
        int SectionId,
        int OtherSectionId,
        List<int> SectionIdsByCodeAsc,
        List<int> SectionIdsByEffectiveDateAsc,
        List<int> SectionIdsDefaultOrder,
        List<int> TieIdsAsc,
        int OtherSectionDocId);

    private async Task<SampleSeedData> ArrangeSamplesAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var prefix = $"SR-{tag}";
        var baseTime = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        await using var db = _fixture.CreateDbContext();
        var fp = await db.DocumentSections.AsNoTracking().FirstAsync(s => s.Code == "FP");
        var micro = await db.DocumentSections.AsNoTracking().FirstAsync(s => s.Code == "MICRO");
        var role = await db.Roles.AsNoTracking().FirstAsync(r => r.Type == RoleType.Analyst);
        var cause = await db.CausesOfTesting.AsNoTracking().FirstOrDefaultAsync();
        if (cause is null)
        {
            cause = new CauseOfTesting { Name = $"Cause-{tag}" };
            db.CausesOfTesting.Add(cause);
            await db.SaveChangesAsync();
        }

        var user = new User
        {
            FullName = $"Sort Scope {tag}",
            Username = $"{prefix}-usr",
            PasswordHash = "hashed_dummy",
            RoleId = role.Id,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = user.Id,
            DepartmentId = fp.DepartmentId,
            SectionId = fp.Id,
            PhyschemArea = PhyschemArea.FinishedProduct
        });

        Item MakeItem(string suffix, string name) => new()
        {
            Name = $"{prefix}-{name}",
            Code = $"{prefix}-I-{suffix}",
            Category = SampleCategory.FinishedProduct,
            IsActive = true
        };

        var itemZ = MakeItem("1", "Zebra");
        var itemA = MakeItem("2", "Alpha");
        var itemM = MakeItem("3", "Mango");
        var itemB = MakeItem("4", "Beta");
        db.Items.AddRange(itemZ, itemA, itemM, itemB);
        await db.SaveChangesAsync();

        Sample NewSample(string suffix, string ctrl, string sampledBy, DateTime receivedAt, Item item, int sectionId, SampleStatus status = SampleStatus.InTesting)
        {
            var s = new Sample
            {
                ReferenceNumber = $"{prefix}-{suffix}",
                ControlNumber = ctrl,
                BatchNumber = ctrl,
                SampledBy = sampledBy,
                Category = SampleCategory.FinishedProduct,
                Status = status,
                PreparationStatus = SamplePreparationStatus.Ready,
                ReceivedByUserId = _fixture.SeededUserId,
                CauseOfTestingId = cause.Id,
                ReceivedAt = receivedAt,
                ItemId = item.Id
            };
            s.TestOrders.Add(new TestOrder
            {
                Sample = s,
                TestCode = "ASSAY",
                Status = ApprovalStatus.InProgress,
                CurrentStep = WorkflowStep.Running,
                SectionId = sectionId
            });
            return s;
        }

        // Default order is ReceivedAt DESC then Id ASC: s2, s4, s1, s3.
        // Control order ASC: C-01 (s1), C-02 (s2), C-03 (s3), C-04 (s4).
        // DisplayName order ASC: Alpha (s2), Beta (s4), Mango (s3), Zebra (s1).
        var s1 = NewSample("1", $"{prefix}-C01", $"{prefix}-Tie", baseTime.AddHours(2), itemZ, fp.Id);
        var s2 = NewSample("2", $"{prefix}-C02", $"{prefix}-Zed", baseTime.AddHours(4), itemA, fp.Id);
        var s3 = NewSample("3", $"{prefix}-C03", $"{prefix}-Tie", baseTime.AddHours(1), itemM, fp.Id, SampleStatus.Approved);
        var s4 = NewSample("4", $"{prefix}-C04", $"{prefix}-Ant", baseTime.AddHours(3), itemB, fp.Id);
        var sMicro = NewSample("M", $"{prefix}-C00", $"{prefix}-Micro", baseTime.AddHours(5), itemA, micro.Id);

        db.Samples.AddRange(s1, s2, s3, s4, sMicro);
        await db.SaveChangesAsync();

        var tieIdsAsc = new List<int> { s1.Id, s3.Id }.OrderBy(x => x).ToList();
        return new SampleSeedData(
            Prefix: prefix,
            FpSectionId: fp.Id,
            FpOnlyUserId: user.Id,
            FpIdsAscendingControl: new List<int> { s1.Id, s2.Id, s3.Id, s4.Id },
            FpIdsAscendingDisplayName: new List<int> { s2.Id, s4.Id, s3.Id, s1.Id },
            FpIdsDefaultOrder: new List<int> { s2.Id, s4.Id, s1.Id, s3.Id },
            TieIdsAsc: tieIdsAsc,
            MicroOnlySampleId: sMicro.Id);
    }

    private async Task<DocumentSeedData> ArrangeDocumentsAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var prefix = $"DL-{tag}";
        var baseTime = new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc);

        await using var db = _fixture.CreateDbContext();
        var otherSection = new DocumentSection
        {
            DepartmentId = _fixture.SeededDepartmentId,
            Name = $"Other {tag}",
            Code = $"OTH-{tag}",
            IsActive = true
        };
        db.DocumentSections.Add(otherSection);
        await db.SaveChangesAsync();

        DocumentMaster NewDoc(string suffix, string title, DateTime createdAt, int sectionId)
        {
            return new DocumentMaster
            {
                MicroLimsDocumentId = $"{prefix}-M-{suffix}",
                CompanyDocumentCode = $"{prefix}-{suffix}",
                Title = title,
                DocumentTypeId = _fixture.SeededDocTypeId,
                DepartmentId = _fixture.SeededDepartmentId,
                SectionId = sectionId,
                DocumentOwnerUserId = _fixture.SeededControllerUserId,
                CreatedByUserId = _fixture.SeededControllerUserId,
                CreatedAt = createdAt,
                RecordStatus = DocumentRecordStatus.Active
            };
        }

        // Default order is CreatedAt ASC then Id ASC: d2, d1, d4, d3.
        // Code order ASC: -A (d1), -B (d2), -C (d3), -D (d4).
        // EffectiveDate order ASC (non-null asc, null last by Id): d3 (May 2), d1 (May 10), d2 (May 20), d4 (null).
        var d1 = NewDoc("A", $"{prefix}-TieTitle", baseTime.AddHours(2), _fixture.SeededSectionId);
        var d2 = NewDoc("B", $"{prefix}-UniqueZ", baseTime.AddHours(1), _fixture.SeededSectionId);
        var d3 = NewDoc("C", $"{prefix}-TieTitle", baseTime.AddHours(4), _fixture.SeededSectionId);
        var d4 = NewDoc("D", $"{prefix}-Alpha", baseTime.AddHours(3), _fixture.SeededSectionId);
        var dOther = NewDoc("E", $"{prefix}-OtherSec", baseTime, otherSection.Id);

        db.DocumentMasters.AddRange(d1, d2, d3, d4, dOther);
        await db.SaveChangesAsync();

        DocumentRevision AddRevision(DocumentMaster master, DateTime? effectiveDate)
        {
            var rev = new DocumentRevision
            {
                DocumentMasterId = master.Id,
                RevisionNumber = "1.0",
                RevisionSequence = 1,
                RevisionStatus = effectiveDate.HasValue ? DocumentRevisionStatus.Effective : DocumentRevisionStatus.Draft,
                EffectiveDate = effectiveDate,
                NextReviewDate = effectiveDate?.AddYears(1),
                CreatedByUserId = _fixture.SeededControllerUserId
            };
            db.DocumentRevisions.Add(rev);
            return rev;
        }

        var r1 = AddRevision(d1, new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc));
        var r2 = AddRevision(d2, new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc));
        var r3 = AddRevision(d3, new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc));
        AddRevision(d4, null);
        await db.SaveChangesAsync();

        d1.CurrentEffectiveRevisionId = r1.Id;
        d2.CurrentEffectiveRevisionId = r2.Id;
        d3.CurrentEffectiveRevisionId = r3.Id;
        await db.SaveChangesAsync();

        var tieIdsAsc = new List<int> { d1.Id, d3.Id }.OrderBy(x => x).ToList();
        return new DocumentSeedData(
            Prefix: prefix,
            SectionId: _fixture.SeededSectionId,
            OtherSectionId: otherSection.Id,
            SectionIdsByCodeAsc: new List<int> { d1.Id, d2.Id, d3.Id, d4.Id },
            SectionIdsByEffectiveDateAsc: new List<int> { d3.Id, d1.Id, d2.Id, d4.Id },
            SectionIdsDefaultOrder: new List<int> { d2.Id, d1.Id, d4.Id, d3.Id },
            TieIdsAsc: tieIdsAsc,
            OtherSectionDocId: dOther.Id);
    }

    private static DocumentMasterService CreateDocumentMasterService(MicroLimsDbContext db)
    {
        var seq = new DatabaseSequenceHelper(db);
        return new DocumentMasterService(db, seq, new AuditEventService(db, seq), new DocumentAuthorizationService(db));
    }

    [PostgresFact]
    public async Task Samples_SortsAscendingAndDescending_AcrossPages_WithNoOverlap()
    {
        var seed = await ArrangeSamplesAsync();

        await using var actDb = _fixture.CreateDbContext();
        var service = TestServiceFactory.TestingWorkspace(actDb);

        async Task<List<int>> AllPagedIds(string sortBy, string sortDir)
        {
            var p1 = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto
            {
                LabSectionId = seed.FpSectionId,
                Area = "fp",
                Search = seed.Prefix,
                SortBy = sortBy,
                SortDir = sortDir,
                Page = 1,
                PageSize = 2
            }, seed.FpOnlyUserId);

            var p2 = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto
            {
                LabSectionId = seed.FpSectionId,
                Area = "fp",
                Search = seed.Prefix,
                SortBy = sortBy,
                SortDir = sortDir,
                Page = 2,
                PageSize = 2
            }, seed.FpOnlyUserId);

            Assert.Equal(4, p1.TotalCount);
            Assert.Equal(2, p1.Items.Count);
            Assert.Equal(2, p2.Items.Count);
            var combined = p1.Items.Concat(p2.Items).Select(i => i.SampleId).ToList();
            Assert.Equal(combined.Count, combined.Distinct().Count());
            return combined;
        }

        Assert.Equal(seed.FpIdsAscendingControl, await AllPagedIds("controlNumber", "asc"));
        Assert.Equal(seed.FpIdsAscendingControl.AsEnumerable().Reverse().ToList(), await AllPagedIds("controlNumber", "desc"));

        Assert.Equal(seed.FpIdsAscendingDisplayName, await AllPagedIds("displayName", "asc"));
        Assert.Equal(seed.FpIdsAscendingDisplayName.AsEnumerable().Reverse().ToList(), await AllPagedIds("displayName", "desc"));
    }

    [PostgresFact]
    public async Task Samples_BreaksTiesById_PreservesDefaultAndScope()
    {
        var seed = await ArrangeSamplesAsync();

        await using var actDb = _fixture.CreateDbContext();
        var service = TestServiceFactory.TestingWorkspace(actDb);

        var defaultRes = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto
        {
            LabSectionId = seed.FpSectionId,
            Area = "fp",
            Search = seed.Prefix,
            Page = 1,
            PageSize = 10
        }, seed.FpOnlyUserId);

        Assert.Equal(seed.FpIdsDefaultOrder, defaultRes.Items.Select(i => i.SampleId).ToList());
        Assert.DoesNotContain(defaultRes.Items, i => i.SampleId == seed.MicroOnlySampleId);

        var tieAsc = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto
        {
            LabSectionId = seed.FpSectionId,
            Area = "fp",
            Search = seed.Prefix,
            SortBy = "sampledBy",
            SortDir = "asc",
            Page = 1,
            PageSize = 10
        }, seed.FpOnlyUserId);

        // Ant < Tie < Zed; ties in Tie keep ascending Id order even when sorting desc.
        Assert.Equal(seed.TieIdsAsc, tieAsc.Items.Where(i => i.SampledBy == $"{seed.Prefix}-Tie").Select(i => i.SampleId).ToList());

        var tieDesc = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto
        {
            LabSectionId = seed.FpSectionId,
            Area = "fp",
            Search = seed.Prefix,
            SortBy = "sampledBy",
            SortDir = "desc",
            Page = 1,
            PageSize = 10
        }, seed.FpOnlyUserId);

        Assert.Equal(seed.TieIdsAsc, tieDesc.Items.Where(i => i.SampledBy == $"{seed.Prefix}-Tie").Select(i => i.SampleId).ToList());
        Assert.DoesNotContain(tieDesc.Items, i => i.SampleId == seed.MicroOnlySampleId);

        // Open samples come before Approved sample in both "due" directions.
        var dueAsc = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto
        {
            LabSectionId = seed.FpSectionId,
            Area = "fp",
            Search = seed.Prefix,
            SortBy = "due",
            SortDir = "asc"
        }, seed.FpOnlyUserId);
        Assert.Equal("Approved", dueAsc.Items.Last().Status);

        var dueDesc = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto
        {
            LabSectionId = seed.FpSectionId,
            Area = "fp",
            Search = seed.Prefix,
            SortBy = "due",
            SortDir = "desc"
        }, seed.FpOnlyUserId);
        Assert.Equal("Approved", dueDesc.Items.Last().Status);
    }

    [PostgresFact]
    public async Task Samples_RejectsUnknownSortBy_AndInvalidSortDir()
    {
        await using var actDb = _fixture.CreateDbContext();
        var service = TestServiceFactory.TestingWorkspace(actDb);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto
        {
            SortBy = "assignedTo",
            SortDir = "asc"
        }, _fixture.SeededUserId));

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto
        {
            SortBy = "unknownField",
            SortDir = "asc"
        }, _fixture.SeededUserId));

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto
        {
            SortBy = "controlNumber",
            SortDir = "sideways"
        }, _fixture.SeededUserId));
    }

    [PostgresFact]
    public async Task DocumentLibrary_SortsAscendingAndDescending_AcrossPages_WithNoOverlap()
    {
        var seed = await ArrangeDocumentsAsync();

        await using var actDb = _fixture.CreateDbContext();
        var service = CreateDocumentMasterService(actDb);

        async Task<List<int>> AllPagedIds(string sortBy, string sortDir)
        {
            var p1 = await service.GetLibraryAsync(new DocumentLibraryFilterRequest
            {
                SearchTerm = seed.Prefix,
                SectionId = seed.SectionId,
                SortBy = sortBy,
                SortDir = sortDir,
                Page = 1,
                PageSize = 2
            }, _fixture.SeededControllerUserId);

            var p2 = await service.GetLibraryAsync(new DocumentLibraryFilterRequest
            {
                SearchTerm = seed.Prefix,
                SectionId = seed.SectionId,
                SortBy = sortBy,
                SortDir = sortDir,
                Page = 2,
                PageSize = 2
            }, _fixture.SeededControllerUserId);

            Assert.Equal(4, p1.TotalCount);
            Assert.Equal(2, p1.Items.Count);
            Assert.Equal(2, p2.Items.Count);
            var combined = p1.Items.Concat(p2.Items).Select(i => i.Id).ToList();
            Assert.Equal(combined.Count, combined.Distinct().Count());
            return combined;
        }

        Assert.Equal(seed.SectionIdsByCodeAsc, await AllPagedIds("companyDocumentCode", "asc"));
        Assert.Equal(seed.SectionIdsByCodeAsc.AsEnumerable().Reverse().ToList(), await AllPagedIds("companyDocumentCode", "desc"));

        // Null EffectiveDate (d4) stays last in both ascending and descending order.
        Assert.Equal(seed.SectionIdsByEffectiveDateAsc, await AllPagedIds("effectiveDate", "asc"));
        var expectedEffectiveDesc = new List<int>
        {
            seed.SectionIdsByEffectiveDateAsc[2],
            seed.SectionIdsByEffectiveDateAsc[1],
            seed.SectionIdsByEffectiveDateAsc[0],
            seed.SectionIdsByEffectiveDateAsc[3]
        };
        Assert.Equal(expectedEffectiveDesc, await AllPagedIds("effectiveDate", "desc"));
    }

    [PostgresFact]
    public async Task DocumentLibrary_BreaksTiesById_PreservesDefaultAndSectionScope()
    {
        var seed = await ArrangeDocumentsAsync();

        await using var actDb = _fixture.CreateDbContext();
        var service = CreateDocumentMasterService(actDb);

        var defaultRes = await service.GetLibraryAsync(new DocumentLibraryFilterRequest
        {
            SearchTerm = seed.Prefix,
            SectionId = seed.SectionId,
            Page = 1,
            PageSize = 10
        }, _fixture.SeededControllerUserId);

        Assert.Equal(seed.SectionIdsDefaultOrder, defaultRes.Items.Select(i => i.Id).ToList());
        Assert.DoesNotContain(defaultRes.Items, i => i.Id == seed.OtherSectionDocId);

        var tieAsc = await service.GetLibraryAsync(new DocumentLibraryFilterRequest
        {
            SearchTerm = seed.Prefix,
            SectionId = seed.SectionId,
            SortBy = "title",
            SortDir = "asc",
            Page = 1,
            PageSize = 10
        }, _fixture.SeededControllerUserId);

        Assert.Equal(seed.TieIdsAsc, tieAsc.Items.Where(i => i.Title == $"{seed.Prefix}-TieTitle").Select(i => i.Id).ToList());

        var tieDesc = await service.GetLibraryAsync(new DocumentLibraryFilterRequest
        {
            SearchTerm = seed.Prefix,
            SectionId = seed.SectionId,
            SortBy = "title",
            SortDir = "desc",
            Page = 1,
            PageSize = 10
        }, _fixture.SeededControllerUserId);

        Assert.Equal(seed.TieIdsAsc, tieDesc.Items.Where(i => i.Title == $"{seed.Prefix}-TieTitle").Select(i => i.Id).ToList());
        Assert.DoesNotContain(tieDesc.Items, i => i.Id == seed.OtherSectionDocId);
    }

    [PostgresFact]
    public async Task DocumentLibrary_RejectsUnknownSortBy_AndInvalidSortDir()
    {
        await using var actDb = _fixture.CreateDbContext();
        var service = CreateDocumentMasterService(actDb);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetLibraryAsync(new DocumentLibraryFilterRequest
        {
            SortBy = "status",
            SortDir = "asc"
        }, _fixture.SeededControllerUserId));

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetLibraryAsync(new DocumentLibraryFilterRequest
        {
            SortBy = "notARealColumn",
            SortDir = "asc"
        }, _fixture.SeededControllerUserId));

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetLibraryAsync(new DocumentLibraryFilterRequest
        {
            SortBy = "title",
            SortDir = "upwards"
        }, _fixture.SeededControllerUserId));
    }
}
