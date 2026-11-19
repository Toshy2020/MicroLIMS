using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Seed;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.ArchitectureTests;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

public class AccessReviewQaSignaturePostgresTests
{
    private const string TestPassword = "QaAccessR3view!Pass";
    private const string RefusedMessage = "Access reviews are decided and signed by Quality Assurance.";
    private static readonly DateTimeOffset BaseNow = new(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);

    private static MicroLimsDbContext NewContext(string connectionString, TimeProvider clock) =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseNpgsql(connectionString).Options, clock);

    private static AccessReviewService CreateService(MicroLimsDbContext db, TimeProvider clock)
    {
        var reader = new SystemSettingsReader(db, new SystemSettingsCache(), null, clock);
        var signatures = new ElectronicSignatureService(db, clock, reader);
        return new AccessReviewService(db, signatures, reader, clock);
    }

    private static async Task<int> CreateUserAsync(
        MicroLimsDbContext db,
        RoleType roleType,
        string username,
        string fullName,
        DateTime now,
        bool isActive = true,
        DateTime? lockedUntil = null,
        bool withLoginHistory = true)
    {
        var roleId = await db.Roles
            .AsNoTracking()
            .Where(r => r.Type == roleType && r.IsSystemRole)
            .Select(r => r.Id)
            .SingleAsync();

        return await CreateUserWithRoleIdAsync(db, roleId, username, fullName, now, isActive, lockedUntil, withLoginHistory);
    }

    private static async Task<int> CreateUserWithRoleIdAsync(
        MicroLimsDbContext db,
        int roleId,
        string username,
        string fullName,
        DateTime now,
        bool isActive = true,
        DateTime? lockedUntil = null,
        bool withLoginHistory = true)
    {
        var user = new User
        {
            FullName = fullName,
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(TestPassword, 4),
            RoleId = roleId,
            IsActive = isActive,
            DeactivatedAt = isActive ? null : now.AddDays(-1),
            LockedUntil = lockedUntil,
            CreatedAt = now.AddDays(-10)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        if (withLoginHistory)
        {
            db.LoginHistories.Add(new LoginHistory
            {
                UserId = user.Id,
                Username = user.Username,
                Success = true,
                Timestamp = now.AddDays(-1)
            });
            await db.SaveChangesAsync();
        }

        return user.Id;
    }

    private static Task<bool> AnyActiveUnlockedQaSignerInDbAsync(MicroLimsDbContext db, DateTime now) =>
        (from u in db.Users.AsNoTracking()
         join r in db.Roles.AsNoTracking() on u.RoleId equals r.Id
         join rp in db.RolePermissions.AsNoTracking() on r.Id equals rp.RoleId
         join p in db.Permissions.AsNoTracking() on rp.PermissionId equals p.Id
         where u.IsActive
               && (u.LockedUntil == null || u.LockedUntil <= now)
               && r.IsActive
               && p.Code == PermissionConstants.AccessReviewSign
         select u.Id).AnyAsync();

    [PostgresFact]
    public async Task Administrator_can_sign_while_no_active_qa_signer_exists()
    {
        await ScratchPostgresDatabase.RunAsync(async cs =>
        {
            var clock = new FixedTimeProvider(BaseNow);
            var now = clock.GetUtcNow().UtcDateTime;
            int adminId;
            int subjectId;
            int reviewId;

            await using (var arrangeDb = NewContext(cs, clock))
            {
                await arrangeDb.Database.MigrateAsync();
                DbSeeder.Seed(arrangeDb);
                adminId = await CreateUserAsync(arrangeDb, RoleType.SystemAdministrator, "sys_admin", "Sys Admin", now);
                subjectId = await CreateUserAsync(arrangeDb, RoleType.Analyst, "lab_analyst", "Lab Analyst", now);

                Assert.False(await AnyActiveUnlockedQaSignerInDbAsync(arrangeDb, now));

                var review = await CreateService(arrangeDb, clock).GenerateAsync("2026 Q4", "Quarterly review", adminId);
                reviewId = review.Id;
            }

            await using (var actDb = NewContext(cs, clock))
            {
                var service = CreateService(actDb, clock);
                await service.AddFindingAsync(
                    reviewId,
                    new SaveAccessReviewFindingRequest(subjectId, "RoleReview", "Verified analyst role", "NoChange", null),
                    adminId);
                await service.SignAsync(reviewId, TestPassword, "Access appropriate", adminId, "10.0.0.1");
            }

            await using (var assertDb = NewContext(cs, clock))
            {
                var review = await assertDb.AccessReviews.AsNoTracking().SingleAsync(r => r.Id == reviewId);
                Assert.Equal(AccessReviewStatus.Signed, review.Status);
                Assert.Equal(adminId, review.ReviewerUserId);
                Assert.Equal(now, review.SignedAt);
                Assert.Equal("Access appropriate", review.Conclusion);
                Assert.NotNull(review.ElectronicSignatureId);

                var signature = await assertDb.ElectronicSignatures.AsNoTracking()
                    .SingleAsync(s => s.Id == review.ElectronicSignatureId);
                Assert.Equal(SignatureMeaning.AccessReviewSigned, signature.MeaningOfSignature);
                Assert.Equal(nameof(AccessReview), signature.EntityType);
                Assert.Equal(reviewId, signature.EntityId);
                Assert.Equal(adminId, signature.UserId);

                var signedAudit = await assertDb.AuditLogs.AsNoTracking()
                    .SingleAsync(a => a.EntityName == nameof(AccessReview)
                                      && a.EntityId == reviewId.ToString()
                                      && a.Action == "ACCESS_REVIEW_SIGNED");
                Assert.Equal(adminId, signedAudit.UserId);
                Assert.Equal(AuditActionCategory.Approval, signedAudit.ActionCategory);
            }
        });
    }

    [PostgresFact]
    public async Task Administrator_refused_once_active_qa_signer_exists()
    {
        await ScratchPostgresDatabase.RunAsync(async cs =>
        {
            var clock = new FixedTimeProvider(BaseNow);
            var now = clock.GetUtcNow().UtcDateTime;
            int adminId;
            int qaId;
            int subjectId;
            int reviewId;
            int findingId;
            int auditCountBefore;

            await using (var arrangeDb = NewContext(cs, clock))
            {
                await arrangeDb.Database.MigrateAsync();
                DbSeeder.Seed(arrangeDb);
                adminId = await CreateUserAsync(arrangeDb, RoleType.SystemAdministrator, "sys_admin", "Sys Admin", now);
                qaId = await CreateUserAsync(arrangeDb, RoleType.Qa, "qa_signer", "QA Signer", now);
                subjectId = await CreateUserAsync(arrangeDb, RoleType.Analyst, "lab_analyst", "Lab Analyst", now);

                Assert.True(await AnyActiveUnlockedQaSignerInDbAsync(arrangeDb, now));

                var service = CreateService(arrangeDb, clock);
                var review = await service.GenerateAsync("2026 Q4", "Quarterly review", adminId);
                reviewId = review.Id;

                var finding = await service.AddFindingAsync(
                    reviewId,
                    new SaveAccessReviewFindingRequest(subjectId, "RoleReview", "Initial QA note", "NoChange", null),
                    qaId);
                findingId = finding.Id;

                auditCountBefore = await arrangeDb.AuditLogs.AsNoTracking().CountAsync();
            }

            await using (var actDb = NewContext(cs, clock))
            {
                var service = CreateService(actDb, clock);

                var addEx = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    service.AddFindingAsync(
                        reviewId,
                        new SaveAccessReviewFindingRequest(subjectId, "Other", "Admin attempt", "NoChange", null),
                        adminId));
                Assert.Equal(RefusedMessage, addEx.Message);

                var updateEx = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    service.UpdateFindingAsync(
                        reviewId,
                        findingId,
                        new SaveAccessReviewFindingRequest(subjectId, "RoleReview", "Admin edit", "NoChange", null),
                        adminId));
                Assert.Equal(RefusedMessage, updateEx.Message);

                var signEx = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    service.SignAsync(reviewId, TestPassword, "Admin sign attempt", adminId, "10.0.0.1"));
                Assert.Equal(RefusedMessage, signEx.Message);
            }

            await using (var assertDb = NewContext(cs, clock))
            {
                var review = await assertDb.AccessReviews.AsNoTracking().SingleAsync(r => r.Id == reviewId);
                Assert.Equal(AccessReviewStatus.Draft, review.Status);
                Assert.Null(review.ReviewerUserId);
                Assert.Null(review.SignedAt);
                Assert.Null(review.ElectronicSignatureId);
                Assert.Null(review.Conclusion);

                Assert.Empty(await assertDb.ElectronicSignatures.AsNoTracking()
                    .Where(s => s.EntityType == nameof(AccessReview) && s.EntityId == reviewId)
                    .ToListAsync());

                var findings = await assertDb.AccessReviewFindings.AsNoTracking()
                    .Where(f => f.ReviewId == reviewId)
                    .ToListAsync();
                var singleFinding = Assert.Single(findings);
                Assert.Equal("Initial QA note", singleFinding.Detail);

                Assert.Equal(auditCountBefore, await assertDb.AuditLogs.AsNoTracking().CountAsync());
            }
        });
    }

    [PostgresFact]
    public async Task Inactive_qa_user_does_not_count()
    {
        await ScratchPostgresDatabase.RunAsync(async cs =>
        {
            var clock = new FixedTimeProvider(BaseNow);
            var now = clock.GetUtcNow().UtcDateTime;
            int adminId;
            int subjectId;
            int reviewId;

            await using (var arrangeDb = NewContext(cs, clock))
            {
                await arrangeDb.Database.MigrateAsync();
                DbSeeder.Seed(arrangeDb);
                adminId = await CreateUserAsync(arrangeDb, RoleType.SystemAdministrator, "sys_admin", "Sys Admin", now);
                await CreateUserAsync(arrangeDb, RoleType.Qa, "inactive_qa", "Inactive QA", now, isActive: false);
                subjectId = await CreateUserAsync(arrangeDb, RoleType.Analyst, "lab_analyst", "Lab Analyst", now);

                Assert.False(await AnyActiveUnlockedQaSignerInDbAsync(arrangeDb, now));

                var review = await CreateService(arrangeDb, clock).GenerateAsync("2026 Q4", "Quarterly review", adminId);
                reviewId = review.Id;
            }

            await using (var actDb = NewContext(cs, clock))
            {
                var service = CreateService(actDb, clock);
                await service.AddFindingAsync(
                    reviewId,
                    new SaveAccessReviewFindingRequest(subjectId, "Other", "Admin finding", "NoChange", null),
                    adminId);
                await service.SignAsync(reviewId, TestPassword, "Signed with inactive QA", adminId, null);
            }

            await using (var assertDb = NewContext(cs, clock))
            {
                var review = await assertDb.AccessReviews.AsNoTracking().SingleAsync(r => r.Id == reviewId);
                Assert.Equal(AccessReviewStatus.Signed, review.Status);
                Assert.Equal(adminId, review.ReviewerUserId);
                Assert.NotNull(review.ElectronicSignatureId);
                Assert.True(await assertDb.AuditLogs.AsNoTracking()
                    .AnyAsync(a => a.EntityId == reviewId.ToString() && a.Action == "ACCESS_REVIEW_SIGNED" && a.UserId == adminId));
            }
        });
    }

    [PostgresFact]
    public async Task Locked_qa_user_does_not_count()
    {
        await ScratchPostgresDatabase.RunAsync(async cs =>
        {
            var clock = new FixedTimeProvider(BaseNow);
            var now = clock.GetUtcNow().UtcDateTime;
            int adminId;
            int firstReviewId;
            int secondReviewId;

            await using (var arrangeDb = NewContext(cs, clock))
            {
                await arrangeDb.Database.MigrateAsync();
                DbSeeder.Seed(arrangeDb);
                adminId = await CreateUserAsync(arrangeDb, RoleType.SystemAdministrator, "sys_admin", "Sys Admin", now);
                await CreateUserAsync(
                    arrangeDb, RoleType.Qa, "locked_qa", "Locked QA", now,
                    isActive: true, lockedUntil: now.AddMinutes(30));

                Assert.False(await AnyActiveUnlockedQaSignerInDbAsync(arrangeDb, now));

                var service = CreateService(arrangeDb, clock);
                firstReviewId = (await service.GenerateAsync("2026 Q4 A", "While locked", adminId)).Id;
                secondReviewId = (await service.GenerateAsync("2026 Q4 B", "After unlock", adminId)).Id;
            }

            await using (var actDb = NewContext(cs, clock))
            {
                var service = CreateService(actDb, clock);
                await service.SignAsync(firstReviewId, TestPassword, "Signed while QA locked", adminId, null);

                // Once the lockout expires at the injected clock, QA counts again and blocks the admin.
                clock.SetUtcNow(BaseNow.AddMinutes(31));
                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    service.SignAsync(secondReviewId, TestPassword, "Admin blocked after unlock", adminId, null));
                Assert.Equal(RefusedMessage, ex.Message);
            }

            await using (var assertDb = NewContext(cs, clock))
            {
                var first = await assertDb.AccessReviews.AsNoTracking().SingleAsync(r => r.Id == firstReviewId);
                Assert.Equal(AccessReviewStatus.Signed, first.Status);
                Assert.Equal(adminId, first.ReviewerUserId);

                var second = await assertDb.AccessReviews.AsNoTracking().SingleAsync(r => r.Id == secondReviewId);
                Assert.Equal(AccessReviewStatus.Draft, second.Status);
            }
        });
    }

    [PostgresFact]
    public async Task Qa_user_of_inactive_role_does_not_count()
    {
        await ScratchPostgresDatabase.RunAsync(async cs =>
        {
            var clock = new FixedTimeProvider(BaseNow);
            var now = clock.GetUtcNow().UtcDateTime;
            int adminId;
            int reviewId;

            await using (var arrangeDb = NewContext(cs, clock))
            {
                await arrangeDb.Database.MigrateAsync();
                DbSeeder.Seed(arrangeDb);
                adminId = await CreateUserAsync(arrangeDb, RoleType.SystemAdministrator, "sys_admin", "Sys Admin", now);

                var inactiveQaRole = new Role
                {
                    Type = RoleType.Qa,
                    Name = "Inactive QA Role",
                    IsSystemRole = false,
                    IsActive = false
                };
                arrangeDb.Roles.Add(inactiveQaRole);
                await arrangeDb.SaveChangesAsync();

                var signPermId = await arrangeDb.Permissions.AsNoTracking()
                    .Where(p => p.Code == PermissionConstants.AccessReviewSign)
                    .Select(p => p.Id)
                    .SingleAsync();
                arrangeDb.RolePermissions.Add(new RolePermission { RoleId = inactiveQaRole.Id, PermissionId = signPermId });
                await arrangeDb.SaveChangesAsync();

                await CreateUserWithRoleIdAsync(arrangeDb, inactiveQaRole.Id, "qa_inactive_role", "QA Inactive Role", now);

                Assert.False(await AnyActiveUnlockedQaSignerInDbAsync(arrangeDb, now));

                reviewId = (await CreateService(arrangeDb, clock).GenerateAsync("2026 Q4", "Quarterly review", adminId)).Id;
            }

            await using (var actDb = NewContext(cs, clock))
            {
                await CreateService(actDb, clock).SignAsync(reviewId, TestPassword, "Signed with inactive QA role", adminId, null);
            }

            await using (var assertDb = NewContext(cs, clock))
            {
                var review = await assertDb.AccessReviews.AsNoTracking().SingleAsync(r => r.Id == reviewId);
                Assert.Equal(AccessReviewStatus.Signed, review.Status);
                Assert.Equal(adminId, review.ReviewerUserId);
            }
        });
    }

    [PostgresFact]
    public async Task Qa_adds_finding_and_signs()
    {
        await ScratchPostgresDatabase.RunAsync(async cs =>
        {
            var clock = new FixedTimeProvider(BaseNow);
            var now = clock.GetUtcNow().UtcDateTime;
            int adminId;
            int qaId;
            int subjectId;
            int reviewId;
            string snapshotSha256;

            await using (var arrangeDb = NewContext(cs, clock))
            {
                await arrangeDb.Database.MigrateAsync();
                DbSeeder.Seed(arrangeDb);
                adminId = await CreateUserAsync(arrangeDb, RoleType.SystemAdministrator, "sys_admin", "Sys Admin", now);
                qaId = await CreateUserAsync(arrangeDb, RoleType.Qa, "qa_signer", "Quinn QA", now);
                subjectId = await CreateUserAsync(arrangeDb, RoleType.Analyst, "lab_analyst", "Lab Analyst", now);

                var review = await CreateService(arrangeDb, clock).GenerateAsync("2026 Q4", "Quarterly review", adminId);
                reviewId = review.Id;
                snapshotSha256 = review.SnapshotSha256;
            }

            int findingId;
            await using (var actDb = NewContext(cs, clock))
            {
                var service = CreateService(actDb, clock);
                var finding = await service.AddFindingAsync(
                    reviewId,
                    new SaveAccessReviewFindingRequest(subjectId, "RoleReview", "Remove unused privilege", "ChangeRequested", "Drop export grant"),
                    qaId);
                findingId = finding.Id;

                await service.SignAsync(reviewId, TestPassword, "Reviewed with one change requested", qaId, "10.0.0.8");
            }

            await using (var assertDb = NewContext(cs, clock))
            {
                var review = await assertDb.AccessReviews.AsNoTracking().SingleAsync(r => r.Id == reviewId);
                Assert.Equal(AccessReviewStatus.Signed, review.Status);
                Assert.Equal(qaId, review.ReviewerUserId);
                Assert.Equal(now, review.SignedAt);
                Assert.Equal("Reviewed with one change requested", review.Conclusion);
                Assert.NotNull(review.ElectronicSignatureId);

                var finding = await assertDb.AccessReviewFindings.AsNoTracking().SingleAsync(f => f.Id == findingId);
                Assert.Equal(qaId, finding.RecordedByUserId);
                Assert.Equal(subjectId, finding.UserId);
                Assert.Equal(AccessReviewFindingAction.ChangeRequested, finding.Action);

                var signature = await assertDb.ElectronicSignatures.AsNoTracking()
                    .SingleAsync(s => s.Id == review.ElectronicSignatureId);
                Assert.Equal(SignatureMeaning.AccessReviewSigned, signature.MeaningOfSignature);
                Assert.Equal(nameof(AccessReview), signature.EntityType);
                Assert.Equal(reviewId, signature.EntityId);
                Assert.Equal(qaId, signature.UserId);

                var findingAudit = await assertDb.AuditLogs.AsNoTracking()
                    .SingleAsync(a => a.EntityName == nameof(AccessReview)
                                      && a.EntityId == reviewId.ToString()
                                      && a.Action == "ACCESS_REVIEW_FINDING_ADDED");
                Assert.Equal(qaId, findingAudit.UserId);
                Assert.Null(findingAudit.PreviousValue);
                Assert.NotNull(findingAudit.NewValue);
                using (var doc = JsonDocument.Parse(findingAudit.NewValue))
                {
                    Assert.Equal(findingId, doc.RootElement.GetProperty("FindingId").GetInt32());
                    Assert.Equal(subjectId, doc.RootElement.GetProperty("UserId").GetInt32());
                    Assert.Equal("lab_analyst", doc.RootElement.GetProperty("Username").GetString());
                    Assert.Equal("RoleReview", doc.RootElement.GetProperty("Kind").GetString());
                    Assert.Equal("ChangeRequested", doc.RootElement.GetProperty("Action").GetString());
                }

                var signedAudit = await assertDb.AuditLogs.AsNoTracking()
                    .SingleAsync(a => a.EntityName == nameof(AccessReview)
                                      && a.EntityId == reviewId.ToString()
                                      && a.Action == "ACCESS_REVIEW_SIGNED");
                Assert.Equal(qaId, signedAudit.UserId);
                Assert.Equal(AuditActionCategory.Approval, signedAudit.ActionCategory);
                Assert.NotNull(signedAudit.PreviousValue);
                Assert.NotNull(signedAudit.NewValue);
                using (var prevDoc = JsonDocument.Parse(signedAudit.PreviousValue))
                {
                    Assert.Equal("Draft", prevDoc.RootElement.GetProperty("Status").GetString());
                }
                using (var newDoc = JsonDocument.Parse(signedAudit.NewValue))
                {
                    Assert.Equal("Signed", newDoc.RootElement.GetProperty("Status").GetString());
                    Assert.Equal(snapshotSha256, newDoc.RootElement.GetProperty("SnapshotSha256").GetString());
                    Assert.Equal(signature.Id, newDoc.RootElement.GetProperty("SignatureId").GetInt32());
                    Assert.Equal(1, newDoc.RootElement.GetProperty("FindingCount").GetInt32());
                    Assert.Equal("Reviewed with one change requested", newDoc.RootElement.GetProperty("Conclusion").GetString());
                }
            }
        });
    }

    [PostgresFact]
    public async Task Qa_named_in_a_finding_cannot_sign()
    {
        await ScratchPostgresDatabase.RunAsync(async cs =>
        {
            var clock = new FixedTimeProvider(BaseNow);
            var now = clock.GetUtcNow().UtcDateTime;
            int adminId;
            int qaId;
            int reviewId;

            await using (var arrangeDb = NewContext(cs, clock))
            {
                await arrangeDb.Database.MigrateAsync();
                DbSeeder.Seed(arrangeDb);
                adminId = await CreateUserAsync(arrangeDb, RoleType.SystemAdministrator, "sys_admin", "Sys Admin", now);
                qaId = await CreateUserAsync(arrangeDb, RoleType.Qa, "qa_signer", "Quinn QA", now);

                var service = CreateService(arrangeDb, clock);
                var review = await service.GenerateAsync("2026 Q4", "Quarterly review", adminId);
                reviewId = review.Id;

                await service.AddFindingAsync(
                    reviewId,
                    new SaveAccessReviewFindingRequest(qaId, "RoleReview", "QA account under review", "NoChange", null),
                    qaId);
            }

            await using (var actDb = NewContext(cs, clock))
            {
                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    CreateService(actDb, clock).SignAsync(reviewId, TestPassword, "Self sign attempt", qaId, null));
                Assert.Contains("your own access", ex.Message);
            }

            await using (var assertDb = NewContext(cs, clock))
            {
                var review = await assertDb.AccessReviews.AsNoTracking().SingleAsync(r => r.Id == reviewId);
                Assert.Equal(AccessReviewStatus.Draft, review.Status);
                Assert.Null(review.ElectronicSignatureId);
                Assert.Empty(await assertDb.ElectronicSignatures.AsNoTracking().ToListAsync());
                Assert.False(await assertDb.AuditLogs.AsNoTracking()
                    .AnyAsync(a => a.EntityId == reviewId.ToString() && a.Action == "ACCESS_REVIEW_SIGNED"));
            }
        });
    }

    [PostgresFact]
    public async Task Qa_sign_with_wrong_password_refused_and_nothing_persisted()
    {
        await ScratchPostgresDatabase.RunAsync(async cs =>
        {
            var clock = new FixedTimeProvider(BaseNow);
            var now = clock.GetUtcNow().UtcDateTime;
            int adminId;
            int qaId;
            int reviewId;

            await using (var arrangeDb = NewContext(cs, clock))
            {
                await arrangeDb.Database.MigrateAsync();
                DbSeeder.Seed(arrangeDb);
                adminId = await CreateUserAsync(arrangeDb, RoleType.SystemAdministrator, "sys_admin", "Sys Admin", now);
                qaId = await CreateUserAsync(arrangeDb, RoleType.Qa, "qa_signer", "Quinn QA", now);

                reviewId = (await CreateService(arrangeDb, clock).GenerateAsync("2026 Q4", "Quarterly review", adminId)).Id;
            }

            await using (var actDb = NewContext(cs, clock))
            {
                await Assert.ThrowsAsync<SignatureVerificationException>(() =>
                    CreateService(actDb, clock).SignAsync(reviewId, "Wrong!Pass123", "Attempt", qaId, null));
            }

            await using (var assertDb = NewContext(cs, clock))
            {
                var review = await assertDb.AccessReviews.AsNoTracking().SingleAsync(r => r.Id == reviewId);
                Assert.Equal(AccessReviewStatus.Draft, review.Status);
                Assert.Null(review.ReviewerUserId);
                Assert.Null(review.SignedAt);
                Assert.Null(review.ElectronicSignatureId);
                Assert.Null(review.Conclusion);
                Assert.Empty(await assertDb.ElectronicSignatures.AsNoTracking().ToListAsync());
                Assert.False(await assertDb.AuditLogs.AsNoTracking()
                    .AnyAsync(a => a.EntityId == reviewId.ToString() && a.Action == "ACCESS_REVIEW_SIGNED"));
            }
        });
    }

    [Fact]
    public void Qa_can_generate_and_export()
    {
        var generateRoles = AuthorizationMatrixTests.AllowedRoles<AdminAccessReviewsController>(
            nameof(AdminAccessReviewsController.Generate));
        var exportRoles = AuthorizationMatrixTests.AllowedRoles<AdminAccessReviewsController>(
            nameof(AdminAccessReviewsController.Export));

        Assert.NotNull(generateRoles);
        Assert.Contains(RoleType.Qa, generateRoles);
        Assert.Contains(RoleType.SystemAdministrator, generateRoles);

        Assert.NotNull(exportRoles);
        Assert.Contains(RoleType.Qa, exportRoles);
        Assert.Contains(RoleType.SystemAdministrator, exportRoles);
    }

    [PostgresFact]
    public async Task CanDecide_is_true_for_administrator_without_qa_and_false_with_qa_and_true_for_qa()
    {
        await ScratchPostgresDatabase.RunAsync(async cs =>
        {
            var clock = new FixedTimeProvider(BaseNow);
            var now = clock.GetUtcNow().UtcDateTime;
            int adminId;
            int reviewId;

            await using (var arrangeDb = NewContext(cs, clock))
            {
                await arrangeDb.Database.MigrateAsync();
                DbSeeder.Seed(arrangeDb);
                adminId = await CreateUserAsync(arrangeDb, RoleType.SystemAdministrator, "sys_admin", "Sys Admin", now);

                Assert.False(await AnyActiveUnlockedQaSignerInDbAsync(arrangeDb, now));

                reviewId = (await CreateService(arrangeDb, clock).GenerateAsync("2026 Q4", "Quarterly review", adminId)).Id;
            }

            await using (var beforeQaDb = NewContext(cs, clock))
            {
                var service = CreateService(beforeQaDb, clock);
                Assert.True(await service.MayDecideAsync(adminId));
                var detail = await service.GetAsync(reviewId, adminId);
                Assert.True(detail.CanDecide);
                var list = await service.ListAsync(adminId);
                Assert.True(Assert.Single(list).CanDecide);
            }

            int qaId;
            await using (var addQaDb = NewContext(cs, clock))
            {
                qaId = await CreateUserAsync(addQaDb, RoleType.Qa, "qa_signer", "Quinn QA", now);
            }

            await using (var afterQaDb = NewContext(cs, clock))
            {
                var service = CreateService(afterQaDb, clock);

                Assert.False(await service.MayDecideAsync(adminId));
                Assert.False((await service.GetAsync(reviewId, adminId)).CanDecide);
                Assert.False(Assert.Single(await service.ListAsync(adminId)).CanDecide);

                Assert.True(await service.MayDecideAsync(qaId));
                Assert.True((await service.GetAsync(reviewId, qaId)).CanDecide);
                Assert.True(Assert.Single(await service.ListAsync(qaId)).CanDecide);
            }
        });
    }
}
