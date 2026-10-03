using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Working standard qualification Task 6: review, return, reject, approve.
public class WorkingStandardSignOffTests
{
    private const string Password = "ValidPassword123!";
    private static readonly DateTimeOffset SepFirst = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

    private class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options) { CurrentUserId = 1 };

    private static ILabClock NewClock() =>
        new LabClock(new FakeTimeProvider(SepFirst), LabClock.ResolveTimeZone("Africa/Cairo"));

    private record Ctx(MicroLimsDbContext Db, WorkingStandardService Svc, DocumentSection Section,
        int A, int R, int P, MaterialMasterEntry Entry);

    private static async Task<Ctx> NewCtxAsync()
    {
        var db = NewDb();
        var section = TestServiceFactory.EnsureMicroSection(db);
        var role = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var ids = new List<int>();
        foreach (var n in new[] { "A", "R", "P" })
        {
            var u = new User
            {
                Username = n + "_" + Guid.NewGuid().ToString("N")[..6], FullName = "User " + n, RoleId = role.Id,
                IsActive = true, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
            };
            db.Users.Add(u);
            await db.SaveChangesAsync();
            TestServiceFactory.AssignUserToMicroSection(db, u.Id);
            ids.Add(u.Id);
        }
        var entry = new MaterialMasterEntry
        {
            SectionId = section.Id, Code = "RS-PARA", Name = "Paracetamol RS", Category = MaterialMasterCategory.ReferenceStandard,
            BaseUnit = MaterialUnit.Gram, IsActive = true,
            CreatedByUserId = 1, CreatedAt = DateTime.UtcNow, LastModifiedByUserId = 1, LastModifiedAt = DateTime.UtcNow,
        };
        db.MaterialMasterEntries.Add(entry);
        await db.SaveChangesAsync();
        return new Ctx(db, TestServiceFactory.WorkingStandard(db, clock: NewClock()), section, ids[0], ids[1], ids[2], entry);
    }

    private static async Task<WorkingStandardQualification> AddAssayedAsync(
        Ctx c, WorkingStandardQualificationKind kind = WorkingStandardQualificationKind.Initial,
        Material? lot = null, bool passed = true)
    {
        var q = new WorkingStandardQualification
        {
            Code = "WSQ-" + Guid.NewGuid().ToString("N")[..6], Kind = kind, Status = WorkingStandardQualificationStatus.Assayed,
            SectionId = c.Section.Id, MaterialMasterEntryId = c.Entry.Id,
            SourceMaterialName = "Paracetamol API", SourceBatchNumber = "RM-77",
            QuantityGrams = kind == WorkingStandardQualificationKind.Initial ? 10m : null,
            Location = kind == WorkingStandardQualificationKind.Initial ? "Fridge 2" : null,
            WorkingStandardMaterialId = lot?.Id,
            MoisturePercent = 0.5m, MeanAssayPercent = 99.6m, RsdPercent = 0.4m, PotencyPercent = 99.497m,
            Passed = passed, FailureReasons = passed ? null : "RSD too high", ReplicateAssaysJson = "[99.5,99.6]",
            CreatedByUserId = c.A, CreatedAt = DateTime.UtcNow,
            PreparedByUserId = c.A, PreparedAt = DateTime.UtcNow,
        };
        c.Db.WorkingStandardQualifications.Add(q);
        await c.Db.SaveChangesAsync();
        return q;
    }

    private static async Task<WorkingStandardQualification> ReloadAsync(Ctx c, int id) =>
        await c.Db.WorkingStandardQualifications.AsNoTracking().FirstAsync(x => x.Id == id);

    private static WorkingStandardSignRequest Sign() => new(Password, null);

    [Fact]
    public async Task Review_BySubmitter_Throws()
    {
        var c = await NewCtxAsync();
        var q = await AddAssayedAsync(c);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => c.Svc.ReviewAsync(q.Id, Sign(), c.A, null));
        Assert.Equal("The analyst who submitted the qualification cannot review it.", ex.Message);
    }

    [Fact]
    public async Task Review_Failed_Throws()
    {
        var c = await NewCtxAsync();
        var q = await AddAssayedAsync(c, passed: false);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => c.Svc.ReviewAsync(q.Id, Sign(), c.R, null));
        Assert.Equal("A failed qualification can only be rejected.", ex.Message);
    }

    [Fact]
    public async Task Approve_ByReviewer_Throws()
    {
        var c = await NewCtxAsync();
        var q = await AddAssayedAsync(c);
        await c.Svc.ReviewAsync(q.Id, Sign(), c.R, null);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => c.Svc.ApproveAsync(q.Id, Sign(), c.R, null));
        Assert.Equal("The reviewer cannot also approve the qualification.", ex.Message);
    }

    [Fact]
    public async Task Approve_Initial_CreatesWorkingStandardLot()
    {
        var c = await NewCtxAsync();
        var q = await AddAssayedAsync(c);
        await c.Svc.ReviewAsync(q.Id, Sign(), c.R, null);
        var dto = await c.Svc.ApproveAsync(q.Id, Sign(), c.P, null);

        Assert.Equal(WorkingStandardQualificationStatus.Approved, dto.Status);
        var lot = await c.Db.Materials.SingleAsync(m => m.MaterialType == MaterialType.WorkingStandard);
        Assert.Equal("WS-01/09/2026", lot.Code);
        Assert.Equal(99.497m, lot.Purity);
        Assert.Equal(0.5m, lot.MoisturePercent);
        Assert.Equal(new DateTime(2027, 9, 1), lot.ExpiryDate!.Value.Date);
        Assert.Equal(q.QuantityGrams, lot.QuantityRemaining);
        Assert.Equal(c.Entry.Id, lot.MaterialMasterEntryId);
        Assert.Equal(lot.Id, (await ReloadAsync(c, q.Id)).WorkingStandardMaterialId);
    }

    [Fact]
    public async Task Approve_Requalification_ExtendsSameLot()
    {
        var c = await NewCtxAsync();
        var lot = new Material
        {
            SectionId = c.Section.Id, MaterialType = MaterialType.WorkingStandard, MaterialMasterEntryId = c.Entry.Id,
            MaterialName = "Paracetamol RS", ManufacturerName = "In-house", BatchNumber = "RM-77", Code = "WS-01/01/2026",
            ReceivingDate = DateTime.UtcNow.AddDays(-200), ExpiryDate = new DateTime(2026, 8, 1), Location = "Fridge",
            QuantityReceived = 10m, QuantityRemaining = 8m, Unit = MaterialUnit.Gram, Purity = 98.9m, MoisturePercent = 0.4m,
            CreatedByUserId = 1, LastModifiedByUserId = 1,
        };
        c.Db.Materials.Add(lot);
        await c.Db.SaveChangesAsync();
        var q = await AddAssayedAsync(c, WorkingStandardQualificationKind.Requalification, lot);
        await c.Svc.ReviewAsync(q.Id, Sign(), c.R, null);
        await c.Svc.ApproveAsync(q.Id, Sign(), c.P, null);

        var fresh = await c.Db.Materials.AsNoTracking().SingleAsync(m => m.MaterialType == MaterialType.WorkingStandard);
        Assert.Equal(lot.Id, fresh.Id);
        Assert.Equal(99.497m, fresh.Purity);
        Assert.Equal(new DateTime(2027, 9, 1), fresh.ExpiryDate!.Value.Date);
        Assert.Equal(1, await c.Db.Materials.CountAsync(m => m.MaterialType == MaterialType.WorkingStandard));
        Assert.True(LotUsability.Check(fresh, c.Entry.Id, null, new DateOnly(2026, 9, 1)).Usable);
    }

    [Fact]
    public async Task Return_ClearsResultsAndGoesToDraft()
    {
        var c = await NewCtxAsync();
        var q = await AddAssayedAsync(c);
        await c.Svc.ReturnAsync(q.Id, new WorkingStandardReturnRequest("  Re-inject  "), c.R);
        var r = await ReloadAsync(c, q.Id);
        Assert.Equal(WorkingStandardQualificationStatus.Draft, r.Status);
        Assert.Equal("Re-inject", r.ReturnReason);
        Assert.Null(r.PotencyPercent);
        Assert.Null(r.MeanAssayPercent);
        Assert.Null(r.RsdPercent);
        Assert.Null(r.ReplicateAssaysJson);
        Assert.Null(r.PreparedByUserId);
        Assert.False(r.Passed);
    }

    [Fact]
    public async Task RejectAtReview_RecordsReasonAndSignature()
    {
        var c = await NewCtxAsync();
        var q = await AddAssayedAsync(c, passed: false);
        await c.Svc.RejectAtReviewAsync(q.Id, new WorkingStandardReasonRequest(Password, "RSD too high"), c.R, null);
        var r = await ReloadAsync(c, q.Id);
        Assert.Equal(WorkingStandardQualificationStatus.Rejected, r.Status);
        Assert.Equal("RSD too high", r.RejectReason);
        Assert.Equal(c.R, r.RejectedByUserId);
        Assert.NotNull(r.RejectedSignatureId);
    }

    [Fact]
    public async Task WrongPassword_NoStateChange()
    {
        var c = await NewCtxAsync();
        var q = await AddAssayedAsync(c);
        await Assert.ThrowsAsync<SignatureVerificationException>(() => c.Svc.ReviewAsync(q.Id, new WorkingStandardSignRequest("bad", null), c.R, null));
        Assert.Equal(WorkingStandardQualificationStatus.Assayed, (await ReloadAsync(c, q.Id)).Status);
    }

    private static async Task<(HplcRun Run, HplcRunSample Sample)> AddAbandonedRunSampleAsync(Ctx c, WorkingStandardQualification q)
    {
        var run = new HplcRun { SectionId = c.Section.Id, Code = "HR-1", Status = HplcRunStatus.Abandoned, StartedAt = DateTime.UtcNow };
        var rs = new HplcRunSample { HplcRun = run, WorkingStandardQualificationId = q.Id, Status = HplcRunSampleStatus.Assigned, AssignedAt = DateTime.UtcNow };
        c.Db.HplcRunSamples.Add(rs);
        await c.Db.SaveChangesAsync();
        return (run, rs);
    }

    [Fact]
    public async Task RejectAtReview_BySubmitter_ThrowsAndNoSignature()
    {
        var c = await NewCtxAsync();
        var q = await AddAssayedAsync(c, passed: false);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => c.Svc.RejectAtReviewAsync(q.Id, new WorkingStandardReasonRequest(Password, "RSD too high"), c.A, null));
        Assert.Equal("The analyst who submitted the qualification cannot reject it at review.", ex.Message);
        Assert.Equal(WorkingStandardQualificationStatus.Assayed, (await ReloadAsync(c, q.Id)).Status);
    }

    [Fact]
    public async Task RejectAtApproval_ByReviewer_Throws()
    {
        var c = await NewCtxAsync();
        var q = await AddAssayedAsync(c);
        await c.Svc.ReviewAsync(q.Id, Sign(), c.R, null);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => c.Svc.RejectAtApprovalAsync(q.Id, new WorkingStandardReasonRequest(Password, "no"), c.R, null));
        Assert.Equal("The reviewer cannot also reject the qualification at approval.", ex.Message);
    }

    [Fact]
    public async Task Review_And_Approve_OnAbandonedRun_Throw_ButRejectAllowed()
    {
        var c = await NewCtxAsync();
        var q = await AddAssayedAsync(c);
        await AddAbandonedRunSampleAsync(c, q);
        const string msg = "The run that carried this assay was abandoned - reject the qualification.";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => c.Svc.ReviewAsync(q.Id, Sign(), c.R, null));
        Assert.Equal(msg, ex.Message);

        q.Status = WorkingStandardQualificationStatus.Reviewed;
        q.ReviewedByUserId = c.R;
        await c.Db.SaveChangesAsync();
        ex = await Assert.ThrowsAsync<InvalidOperationException>(() => c.Svc.ApproveAsync(q.Id, Sign(), c.P, null));
        Assert.Equal(msg, ex.Message);

        await c.Svc.RejectAtApprovalAsync(q.Id, new WorkingStandardReasonRequest(Password, "Run abandoned"), c.P, null);
        Assert.Equal(WorkingStandardQualificationStatus.Rejected, (await ReloadAsync(c, q.Id)).Status);
    }

    [Fact]
    public async Task Dto_AfterSubmit_KeepsAbandonedRunLinkAndEvidence_DraftDoesNot()
    {
        var c = await NewCtxAsync();
        var q = await AddAssayedAsync(c);
        var (run, rs) = await AddAbandonedRunSampleAsync(c, q);
        c.Db.HplcEvidences.Add(new HplcEvidence { HplcRunId = run.Id, HplcRunSampleId = rs.Id, Context = HplcEvidenceContext.Sample,
            Kind = HplcEvidenceKind.SampleReport, FileName = "s.pdf", ContentType = "application/pdf", FilePath = "x", UploadedAt = DateTime.UtcNow });
        await c.Db.SaveChangesAsync();

        var dto = await c.Svc.GetAsync(q.Id, c.R);
        Assert.Equal(run.Id, dto.Run!.HplcRunId);
        Assert.Single(dto.RunEvidenceIds);

        q.Status = WorkingStandardQualificationStatus.Draft;
        await c.Db.SaveChangesAsync();
        dto = await c.Svc.GetAsync(q.Id, c.R);
        Assert.Null(dto.Run);
        Assert.Empty(dto.RunEvidenceIds);
    }

    [Fact]
    public async Task Return_ReasonOver500_Throws_BlankStillEnterReason()
    {
        var c = await NewCtxAsync();
        var q = await AddAssayedAsync(c);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => c.Svc.ReturnAsync(q.Id, new WorkingStandardReturnRequest(new string('x', 501)), c.R));
        Assert.Equal("The reason must be 500 characters or fewer.", ex.Message);
        ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => c.Svc.ReturnAsync(q.Id, new WorkingStandardReturnRequest("  "), c.R));
        Assert.Equal("Enter a reason.", ex.Message);
    }
}
