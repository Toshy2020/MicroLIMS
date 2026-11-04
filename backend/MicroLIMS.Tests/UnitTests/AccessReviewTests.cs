using System.Text;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// WP4.3: periodic access review. The report content, the reason on generation,
// findings only while Draft, the signature (none / wrong / correct password),
// the signer-not-about-themselves rule and immutability once signed.
public class AccessReviewTests
{
    private const string QaPassword = "QaP@ssw0rd!";
    private const string Reason = "Quarterly access review";
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    private const int Admin = 1, Qa = 2, Dormant = 3, NeverIn = 4, OldNever = 5, Gone = 6, Conflict = 7;

    private sealed class Rig
    {
        public required MicroLimsDbContext Db { get; init; }
        public required AccessReviewService Service { get; init; }
    }

    private static Rig CreateRig()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new MicroLimsDbContext(options);
        var now = Now.UtcDateTime;

        var perms = new[]
        {
            PermissionConstants.UsersManage, PermissionConstants.RolesManage, PermissionConstants.DiscussionsView,
            PermissionConstants.AccessReview, PermissionConstants.SamplesReview, PermissionConstants.SamplesReceive,
            PermissionConstants.SamplesApprove
        }.Select((code, i) => new Permission { Id = i + 1, Code = code, Description = code, IsEnforced = true }).ToList();
        db.Permissions.AddRange(perms);
        int P(string code) => perms.Single(p => p.Code == code).Id;

        db.Roles.AddRange(
            new Role { Id = 1, Type = RoleType.SystemAdministrator, Name = "System Administrator", IsSystemRole = true },
            new Role { Id = 2, Type = RoleType.Reviewer, Name = "QA Reviewer" },
            new Role { Id = 3, Type = RoleType.Analyst, Name = "Analyst" },
            new Role { Id = 4, Type = RoleType.Analyst, Name = "Mixed duties" });
        db.RolePermissions.AddRange(
            new RolePermission { RoleId = 1, PermissionId = P(PermissionConstants.UsersManage) },
            new RolePermission { RoleId = 1, PermissionId = P(PermissionConstants.RolesManage) },
            new RolePermission { RoleId = 1, PermissionId = P(PermissionConstants.DiscussionsView) },
            new RolePermission { RoleId = 2, PermissionId = P(PermissionConstants.AccessReview) },
            new RolePermission { RoleId = 2, PermissionId = P(PermissionConstants.SamplesReview) },
            new RolePermission { RoleId = 3, PermissionId = P(PermissionConstants.SamplesReceive) },
            new RolePermission { RoleId = 4, PermissionId = P(PermissionConstants.UsersManage) },
            new RolePermission { RoleId = 4, PermissionId = P(PermissionConstants.SamplesApprove) });

        var hash = BCrypt.Net.BCrypt.HashPassword(QaPassword, 4);
        db.Users.AddRange(
            new User { Id = Admin, Username = "admin", FullName = "Ada Admin", RoleId = 1, PasswordHash = hash, CreatedAt = now.AddDays(-900) },
            new User { Id = Qa, Username = "qa", FullName = "Quinn QA", RoleId = 2, PasswordHash = hash, CreatedAt = now.AddDays(-900) },
            new User { Id = Dormant, Username = "dormant", FullName = "Dora Dormant", RoleId = 3, PasswordHash = hash, CreatedAt = now.AddDays(-900) },
            new User { Id = NeverIn, Username = "neverin", FullName = "Nina New", RoleId = 3, PasswordHash = hash, CreatedAt = now.AddDays(-10) },
            new User { Id = OldNever, Username = "oldnever", FullName = "Olga Old", RoleId = 3, PasswordHash = hash, CreatedAt = now.AddDays(-400) },
            new User { Id = Gone, Username = "gone", FullName = "Gus Gone", RoleId = 3, PasswordHash = hash, CreatedAt = now.AddDays(-900), IsActive = false, DeactivatedAt = now.AddDays(-90) },
            new User { Id = Conflict, Username = "conflict", FullName = "Connie Conflict", RoleId = 4, PasswordHash = hash, CreatedAt = now.AddDays(-900) });

        db.LoginHistories.AddRange(
            new LoginHistory { UserId = Admin, Username = "admin", Success = true, Timestamp = now.AddDays(-1) },
            new LoginHistory { UserId = Qa, Username = "qa", Success = true, Timestamp = now.AddDays(-5) },
            new LoginHistory { UserId = Dormant, Username = "dormant", Success = true, Timestamp = now.AddDays(-300) },
            new LoginHistory { UserId = Dormant, Username = "dormant", Success = true, Timestamp = now.AddDays(-200) },
            // A failed sign-in never counts as activity.
            new LoginHistory { UserId = Dormant, Username = "dormant", Success = false, Timestamp = now.AddDays(-1) },
            new LoginHistory { UserId = Gone, Username = "gone", Success = true, Timestamp = now.AddDays(-400) },
            new LoginHistory { UserId = Conflict, Username = "conflict", Success = true, Timestamp = now.AddDays(-2) });

        db.AuditLogs.AddRange(
            new AuditLog { EntityName = nameof(User), EntityId = Gone.ToString(), Action = "USER_DISABLED", Reason = "Left the company", UserId = Admin, Timestamp = now.AddDays(-90) },
            new AuditLog { EntityName = nameof(User), EntityId = Dormant.ToString(), Action = "USER_ENABLED", Reason = "Returned", UserId = Admin, Timestamp = now.AddDays(-60) },
            // Older than 12 months, a different action, and a different entity: all left out.
            new AuditLog { EntityName = nameof(User), EntityId = Dormant.ToString(), Action = "USER_ROLE_CHANGED", UserId = Admin, Timestamp = now.AddMonths(-13) },
            new AuditLog { EntityName = nameof(User), EntityId = Dormant.ToString(), Action = "USER_EMAIL_CHANGED", UserId = Admin, Timestamp = now.AddDays(-5) },
            new AuditLog { EntityName = "Role", EntityId = Dormant.ToString(), Action = "USER_DISABLED", UserId = Admin, Timestamp = now.AddDays(-5) });

        var dept = new DocumentDepartment { Id = 1, Code = "MB", Name = "Microbiology" };
        var section = new DocumentSection { Id = 1, Code = "EM", Name = "Environmental Monitoring", DepartmentId = 1, Department = dept };
        db.DocumentDepartments.Add(dept);
        db.DocumentSections.Add(section);
        db.UserOrgMemberships.AddRange(
            new UserOrgMembership { UserId = Dormant, DepartmentId = 1, Department = dept, SectionId = 1, Section = section },
            new UserOrgMembership { UserId = Qa, DepartmentId = 1, Department = dept });
        db.SaveChanges();

        var clock = new FixedTimeProvider(Now);
        var reader = new SystemSettingsReader(db, new SystemSettingsCache(), null, clock);
        var signatures = new ElectronicSignatureService(db, clock, reader);
        return new Rig { Db = db, Service = new AccessReviewService(db, signatures, reader, clock) };
    }

    private static AccessReviewUserEntry Entry(AccessReviewDetailDto d, int userId) => d.Snapshot.Users.Single(u => u.UserId == userId);

    // ---- report content -----------------------------------------------------

    [Fact]
    public async Task Report_ListsEveryAccount_ActiveOrNot_WithRoleAndEffectivePermissions()
    {
        var rig = CreateRig();
        var review = await rig.Service.GenerateAsync("2026 Q4", Reason, Admin);

        Assert.Equal(7, review.Snapshot.Users.Count);
        var gone = Entry(review, Gone);
        Assert.False(gone.IsActive);
        Assert.Equal(Now.UtcDateTime.AddDays(-90), gone.DeactivatedAt);
        Assert.Equal("Analyst", gone.RoleName);
        Assert.Equal("Analyst", gone.RoleType);

        var admin = Entry(review, Admin);
        Assert.Equal("Ada Admin", admin.FullName);
        Assert.Equal("System Administrator", admin.RoleName);
        Assert.Equal("SystemAdministrator", admin.RoleType);
        Assert.Equal(new[] { "Discussions.View", "Roles.Manage", "Users.Manage" }, admin.Permissions.Select(p => p.Code));
        // Hidden codes are included, and marked.
        Assert.True(admin.Permissions.Single(p => p.Code == "Discussions.View").Hidden);
        Assert.False(admin.Permissions.Single(p => p.Code == "Users.Manage").Hidden);

        Assert.Equal(1, review.Snapshot.Summary.InactiveUsers);
        Assert.Equal(6, review.Snapshot.Summary.ActiveUsers);
        Assert.Equal("2026 Q4", review.Snapshot.PeriodLabel);
        Assert.Equal(90, review.Snapshot.DormantDays);
    }

    [Fact]
    public async Task Report_ShowsSectionMemberships()
    {
        var review = await CreateRig().Service.GenerateAsync(null, Reason, Admin);

        var section = Assert.Single(Entry(review, Dormant).Sections);
        Assert.Equal("Microbiology", section.DepartmentName);
        Assert.Equal("Environmental Monitoring", section.SectionName);
        var wholeDepartment = Assert.Single(Entry(review, Qa).Sections);
        Assert.Null(wholeDepartment.SectionId);
        Assert.Empty(Entry(review, Admin).Sections);
    }

    [Fact]
    public async Task Report_LastLoginIsTheLatestSuccessfulOne()
    {
        var review = await CreateRig().Service.GenerateAsync(null, Reason, Admin);

        Assert.Equal(Now.UtcDateTime.AddDays(-200), Entry(review, Dormant).LastSuccessfulLogin);
        Assert.Equal(Now.UtcDateTime.AddDays(-5), Entry(review, Qa).LastSuccessfulLogin);
        Assert.Null(Entry(review, NeverIn).LastSuccessfulLogin);
    }

    [Fact]
    public async Task Report_StatusHistoryHoldsEnableDisableAndRoleChangesOfTheLastTwelveMonths()
    {
        var review = await CreateRig().Service.GenerateAsync(null, Reason, Admin);

        var gone = Assert.Single(Entry(review, Gone).StatusHistory);
        Assert.Equal("USER_DISABLED", gone.Action);
        Assert.Equal("Left the company", gone.Reason);
        Assert.Equal(Admin, gone.ByUserId);
        // The role change 13 months ago, the e-mail change and the other entity's row are not history.
        var dormant = Assert.Single(Entry(review, Dormant).StatusHistory);
        Assert.Equal("USER_ENABLED", dormant.Action);
        Assert.Empty(Entry(review, Admin).StatusHistory);
    }

    [Fact]
    public async Task Report_FlagsDormantNeverLoggedInAndSodConflicts()
    {
        var review = await CreateRig().Service.GenerateAsync(null, Reason, Admin);

        var dormant = Entry(review, Dormant);
        Assert.True(dormant.Dormant);
        Assert.False(dormant.NeverLoggedIn);
        Assert.Equal(200, dormant.DaysSinceLastActivity);

        // Never signed in but only 10 days old: flagged never-logged-in, not yet dormant.
        var fresh = Entry(review, NeverIn);
        Assert.True(fresh.NeverLoggedIn);
        Assert.False(fresh.Dormant);
        // Never signed in and older than the threshold: both.
        var old = Entry(review, OldNever);
        Assert.True(old.NeverLoggedIn);
        Assert.True(old.Dormant);

        // An inactive account is not "still active": not dormant, however long ago it signed in.
        Assert.False(Entry(review, Gone).Dormant);

        Assert.False(Entry(review, Qa).Dormant);
        Assert.Empty(Entry(review, Admin).SodConflicts);
        Assert.Equal(new[] { "Users.Manage + Samples.Approve" }, Entry(review, Conflict).SodConflicts);

        Assert.Equal(2, review.Snapshot.Summary.DormantUsers);
        Assert.Equal(2, review.Snapshot.Summary.NeverLoggedInUsers);
        Assert.Equal(1, review.Snapshot.Summary.SodConflictUsers);
    }

    [Fact]
    public async Task DormantDays_ComesFromTheRegistrySetting_AndIsKeptInTheSnapshot()
    {
        var rig = CreateRig();
        rig.Db.SystemSettings.Add(new SystemSetting
        {
            Key = SystemSettingKeys.AccessReviewDormantDays, Value = "250", ValueType = SystemSettingValueType.Integer,
            Category = "Access Review", Description = "d", CurrentVersion = 1
        });
        await rig.Db.SaveChangesAsync();

        var review = await rig.Service.GenerateAsync(null, Reason, Admin);

        Assert.Equal(250, review.Snapshot.DormantDays);
        Assert.False(Entry(review, Dormant).Dormant); // 200 days < 250
        Assert.True(Entry(review, OldNever).Dormant); // 400 days > 250
    }

    // ---- generate -----------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Generate_RequiresAReason_AndStoresNothingWithout(string? reason)
    {
        var rig = CreateRig();
        await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Service.GenerateAsync("2026 Q4", reason, Admin));
        Assert.Empty(await rig.Db.AccessReviews.ToListAsync());
    }

    [Fact]
    public async Task Generate_StoresADraftWithTheSnapshotChecksum_AndAnAuditRow()
    {
        var rig = CreateRig();
        var review = await rig.Service.GenerateAsync("2026 Q4", Reason, Admin);

        Assert.Equal("Draft", review.Status);
        Assert.True(review.SnapshotIntact);
        var stored = await rig.Db.AccessReviews.SingleAsync();
        Assert.Equal(64, stored.SnapshotSha256.Length);
        Assert.Equal(Admin, stored.GeneratedByUserId);
        var audit = await rig.Db.AuditLogs.SingleAsync(a => a.Action == "ACCESS_REVIEW_GENERATED");
        Assert.Equal(stored.Id.ToString(), audit.EntityId);
        Assert.Equal(Reason, audit.Reason);
        Assert.Contains(stored.SnapshotSha256, audit.NewValue);
        // The audit row carries the checksum, never the report itself.
        Assert.DoesNotContain("Ada Admin", audit.NewValue);
    }

    [Fact]
    public async Task Generate_WithoutALabel_UsesTheDate()
    {
        var review = await CreateRig().Service.GenerateAsync(" ", Reason, Admin);
        Assert.Equal("Access review 2026-10-28", review.PeriodLabel);
    }

    // ---- findings -----------------------------------------------------------

    private static SaveAccessReviewFindingRequest Finding(int userId, string kind = "Dormant", string action = "NoChange", string? note = null) =>
        new(userId, kind, "Last sign-in 200 days ago", action, note);

    [Fact]
    public async Task Findings_AreRecordedWhileDraft_AndShownWithTheReview()
    {
        var rig = CreateRig();
        var review = await rig.Service.GenerateAsync(null, Reason, Admin);

        var finding = await rig.Service.AddFindingAsync(review.Id, Finding(Dormant, action: "ChangeRequested", note: "Disable the account"), Qa);

        Assert.Equal("dormant", finding.Username);
        Assert.Equal("ChangeRequested", finding.Action);
        var loaded = await rig.Service.GetAsync(review.Id);
        Assert.Single(loaded.Findings);
        Assert.Single(await rig.Db.AuditLogs.Where(a => a.Action == "ACCESS_REVIEW_FINDING_ADDED").ToListAsync());

        var updated = await rig.Service.UpdateFindingAsync(review.Id, finding.Id, Finding(Dormant, action: "NoChange", note: "Back from leave"), Qa);
        Assert.Equal("NoChange", updated.Action);
        Assert.Equal("Back from leave", updated.Note);
        Assert.Single(await rig.Db.AuditLogs.Where(a => a.Action == "ACCESS_REVIEW_FINDING_UPDATED").ToListAsync());
    }

    [Fact]
    public async Task Findings_AreValidated()
    {
        var rig = CreateRig();
        var review = await rig.Service.GenerateAsync(null, Reason, Admin);

        await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Service.AddFindingAsync(review.Id, Finding(999), Qa));
        await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Service.AddFindingAsync(review.Id, Finding(Dormant, kind: "Nonsense"), Qa));
        await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Service.AddFindingAsync(review.Id, Finding(Dormant, action: "Delete"), Qa));
        await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Service.AddFindingAsync(review.Id, Finding(Dormant, action: "ChangeRequested"), Qa)); // note required
        await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Service.AddFindingAsync(review.Id, Finding(Dormant) with { Detail = " " }, Qa));
        await Assert.ThrowsAsync<NotFoundException>(() => rig.Service.AddFindingAsync(999, Finding(Dormant), Qa));
        Assert.Empty(await rig.Db.AccessReviewFindings.ToListAsync());
    }

    [Fact]
    public async Task Findings_CannotBeAddedOrChangedOnceSigned()
    {
        var rig = CreateRig();
        var review = await rig.Service.GenerateAsync(null, Reason, Admin);
        var finding = await rig.Service.AddFindingAsync(review.Id, Finding(Dormant), Qa);
        await rig.Service.SignAsync(review.Id, QaPassword, "Reviewed; no further action", Qa, null);

        var add = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Service.AddFindingAsync(review.Id, Finding(NeverIn, "NeverLoggedIn"), Qa));
        Assert.Contains("signed", add.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Service.UpdateFindingAsync(review.Id, finding.Id, Finding(Dormant, note: "edit"), Qa));
        Assert.Single(await rig.Db.AccessReviewFindings.ToListAsync());
    }

    // ---- signing ------------------------------------------------------------

    [Fact]
    public async Task Sign_WithNoPassword_IsRefused_AndTheReviewStaysDraft()
    {
        var rig = CreateRig();
        var review = await rig.Service.GenerateAsync(null, Reason, Admin);

        await Assert.ThrowsAsync<SignatureVerificationException>(() => rig.Service.SignAsync(review.Id, null, "Fine", Qa, null));
        await AssertStillDraft(rig, review.Id);
    }

    [Fact]
    public async Task Sign_WithAWrongPassword_IsRefused_AndTheReviewStaysDraft()
    {
        var rig = CreateRig();
        var review = await rig.Service.GenerateAsync(null, Reason, Admin);

        await Assert.ThrowsAsync<SignatureVerificationException>(() => rig.Service.SignAsync(review.Id, "wrong", "Fine", Qa, null));
        await AssertStillDraft(rig, review.Id);
        Assert.Contains(await rig.Db.AuditLogs.ToListAsync(), a => a.EntityName == "ElectronicSignature" && a.Action == "SignatureFailed");
    }

    private static async Task AssertStillDraft(Rig rig, int id)
    {
        var stored = await rig.Db.AccessReviews.AsNoTracking().SingleAsync(r => r.Id == id);
        Assert.Equal(AccessReviewStatus.Draft, stored.Status);
        Assert.Null(stored.ReviewerUserId);
        Assert.Null(stored.ElectronicSignatureId);
        Assert.Empty(await rig.Db.ElectronicSignatures.ToListAsync());
    }

    [Fact]
    public async Task Sign_WithTheCorrectPassword_SignsTheReview()
    {
        var rig = CreateRig();
        var review = await rig.Service.GenerateAsync("2026 Q4", Reason, Admin);

        var signed = await rig.Service.SignAsync(review.Id, QaPassword, "Access is appropriate", Qa, "10.0.0.5");

        Assert.Equal("Signed", signed.Status);
        Assert.Equal(Qa, signed.ReviewerUserId);
        Assert.Equal("Quinn QA", signed.ReviewerName);
        Assert.Equal(Now.UtcDateTime, signed.SignedAt);
        Assert.Equal("Access is appropriate", signed.Conclusion);
        Assert.True(signed.SnapshotIntact);
        var signature = await rig.Db.ElectronicSignatures.SingleAsync();
        Assert.Equal(signature.Id, signed.ElectronicSignatureId);
        Assert.Equal(SignatureMeaning.AccessReviewSigned, signature.MeaningOfSignature);
        Assert.Equal("AccessReview", signature.EntityType);
        Assert.Equal(review.Id, signature.EntityId);
        Assert.Equal(Qa, signature.UserId);
        Assert.Single(await rig.Db.AuditLogs.Where(a => a.Action == "ACCESS_REVIEW_SIGNED").ToListAsync());
    }

    [Fact]
    public async Task Sign_NeedsAConclusion()
    {
        var rig = CreateRig();
        var review = await rig.Service.GenerateAsync(null, Reason, Admin);

        await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Service.SignAsync(review.Id, QaPassword, "  ", Qa, null));
        await AssertStillDraft(rig, review.Id);
    }

    [Fact]
    public async Task SignedReview_CannotBeSignedAgainOrChanged()
    {
        var rig = CreateRig();
        var review = await rig.Service.GenerateAsync(null, Reason, Admin);
        await rig.Service.SignAsync(review.Id, QaPassword, "Fine", Qa, null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Service.SignAsync(review.Id, QaPassword, "Again", Qa, null));
        var stored = await rig.Db.AccessReviews.AsNoTracking().SingleAsync();
        Assert.Equal("Fine", stored.Conclusion);
        Assert.Single(await rig.Db.ElectronicSignatures.ToListAsync());
    }

    [Fact]
    public async Task Sign_IsRefusedForAReviewAboutTheSignersOwnAccess()
    {
        var rig = CreateRig();

        // The report flags the signer's own account (a segregation-of-duties conflict).
        var flagged = await rig.Service.GenerateAsync(null, Reason, Admin);
        var self = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Service.SignAsync(flagged.Id, QaPassword, "Fine", Conflict, null));
        Assert.Contains("your own access", self.Message);

        // A finding is recorded about the signer's account.
        var review = await rig.Service.GenerateAsync(null, Reason, Admin);
        await rig.Service.AddFindingAsync(review.Id, Finding(Qa, "RoleReview"), Admin);
        await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Service.SignAsync(review.Id, QaPassword, "Fine", Qa, null));
        Assert.Empty(await rig.Db.ElectronicSignatures.ToListAsync());
    }

    [Fact]
    public async Task Sign_ByTheGenerator_IsAllowed_WhenTheReviewIsNotAboutThem()
    {
        var rig = CreateRig();
        var review = await rig.Service.GenerateAsync(null, Reason, Qa);

        var signed = await rig.Service.SignAsync(review.Id, QaPassword, "Fine", Qa, null);
        Assert.Equal("Signed", signed.Status);
    }

    [Fact]
    public async Task Sign_IsRefused_WhenTheStoredReportNoLongerMatchesItsChecksum()
    {
        var rig = CreateRig();
        var review = await rig.Service.GenerateAsync(null, Reason, Admin);
        var stored = await rig.Db.AccessReviews.SingleAsync();
        stored.SnapshotJson = stored.SnapshotJson.Replace("Ada Admin", "Mallory");
        await rig.Db.SaveChangesAsync();

        Assert.False((await rig.Service.GetAsync(review.Id)).SnapshotIntact);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Service.SignAsync(review.Id, QaPassword, "Fine", Qa, null));
        Assert.Contains("checksum", ex.Message);
    }

    // ---- list / export ------------------------------------------------------

    [Fact]
    public async Task List_ShowsNewestFirst_WithStatusAndFindingCount()
    {
        var rig = CreateRig();
        var first = await rig.Service.GenerateAsync("A", Reason, Admin);
        await rig.Service.AddFindingAsync(first.Id, Finding(Dormant), Qa);
        await rig.Service.SignAsync(first.Id, QaPassword, "Fine", Qa, null);
        var second = await rig.Service.GenerateAsync("B", Reason, Admin);

        var list = await rig.Service.ListAsync();

        Assert.Equal(new[] { second.Id, first.Id }, list.Select(r => r.Id));
        Assert.Equal("Draft", list[0].Status);
        Assert.Equal("Signed", list[1].Status);
        Assert.Equal(1, list[1].FindingCount);
        Assert.Equal("Quinn QA", list[1].ReviewerName);
    }

    [Fact]
    public async Task ExportCsv_HasOneRowPerAccount_NeutralisesFormulas_AndIsAudited()
    {
        var rig = CreateRig();
        var admin = await rig.Db.Users.SingleAsync(u => u.Id == Dormant);
        admin.FullName = "=HYPERLINK(\"http://evil\")";
        await rig.Db.SaveChangesAsync();
        var review = await rig.Service.GenerateAsync("2026 Q4", Reason, Admin);
        await rig.Service.AddFindingAsync(review.Id, Finding(Dormant), Qa);

        var file = await rig.Service.ExportCsvAsync(review.Id, Qa);

        Assert.StartsWith("text/csv", file.ContentType);
        Assert.EndsWith(".csv", file.FileName);
        var text = Encoding.UTF8.GetString(file.Content).TrimStart('﻿');
        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(1 + 7, lines.Length);
        Assert.Contains("\"Permissions\"", lines[0]);
        Assert.Contains("Discussions.View (hidden)", text);
        Assert.Contains("Users.Manage + Samples.Approve", text);
        Assert.Contains("\"'=HYPERLINK(", text);
        Assert.Contains("Dormant: Last sign-in 200 days ago [NoChange]", text);

        var audit = await rig.Db.AuditLogs.SingleAsync(a => a.Action == "ACCESS_REVIEW_EXPORTED");
        Assert.Equal(review.Id.ToString(), audit.EntityId);
        Assert.Equal(Qa, audit.UserId);
        Assert.Equal(AuditActionCategory.ImportExport, audit.ActionCategory);
    }

    [Fact]
    public void Permission_IsEnforced_GrantedToTheAdministrator_AndInNoSegregationSet()
    {
        Assert.Contains(PermissionConstants.AccessReview, PermissionConstants.All);
        Assert.Contains(PermissionConstants.AccessReview, PermissionConstants.Enforced);
        Assert.Contains(PermissionConstants.AccessReview, PermissionConstants.SystemAdministratorGrants);
        Assert.DoesNotContain(PermissionConstants.AccessReview, SegregationOfDutiesRules.AdministrationCodes);
        Assert.DoesNotContain(PermissionConstants.AccessReview, SegregationOfDutiesRules.GxpExecutionAndApprovalCodes);
    }
}
