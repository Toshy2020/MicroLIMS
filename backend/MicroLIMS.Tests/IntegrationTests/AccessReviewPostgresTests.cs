using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// WP4.3 database guarantees. AccessReviews change once only (Draft -> Signed, snapshot columns untouched) and are
// never deleted or truncated; findings change only while their review is Draft and are never deleted or truncated.
// The migration grants Access.Review to the built-in administrator.
[Collection("PostgresDatabaseCollection")]
public class AccessReviewPostgresTests
{
    private readonly PostgresTestFixture _fixture;

    public AccessReviewPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private async Task<int> NewDraftAsync()
    {
        await using var db = _fixture.CreateDbContext();
        var review = new AccessReview
        {
            GeneratedByUserId = _fixture.SeededUserId, PeriodLabel = $"trigger test {Guid.NewGuid():N}",
            SnapshotJson = "{\"users\":[]}", SnapshotSha256 = new string('a', 64)
        };
        db.AccessReviews.Add(review);
        await db.SaveChangesAsync();
        return review.Id;
    }

    private async Task<int> NewSignatureAsync()
    {
        await using var db = _fixture.CreateDbContext();
        var signature = new ElectronicSignature
        {
            UserId = _fixture.SeededUserId, UserFullNameSnapshot = "QA", UsernameSnapshot = "qa_admin", RoleSnapshot = "SystemAdministrator",
            MeaningOfSignature = SignatureMeaning.AccessReviewSigned, EntityType = "AccessReview", EntityId = 1
        };
        db.ElectronicSignatures.Add(signature);
        await db.SaveChangesAsync();
        return signature.Id;
    }

    private async Task<PostgresException> FailAsync(string sql, params object[] args)
    {
        await using var db = _fixture.CreateDbContext();
        return await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql, args));
    }

    private async Task SignViaSqlAsync(int reviewId)
    {
        var signatureId = await NewSignatureAsync();
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE \"AccessReviews\" SET \"Status\" = 1, \"ReviewerUserId\" = {0}, \"SignedAt\" = now(), \"ElectronicSignatureId\" = {1}, \"Conclusion\" = 'ok' WHERE \"Id\" = {2}",
            _fixture.SeededUserId, signatureId, reviewId);
    }

    [PostgresFact]
    public async Task Draft_AllowsOnlyTheSigningUpdate_NeverASnapshotChange()
    {
        var id = await NewDraftAsync();

        // The snapshot columns never change, even while Draft.
        var snapshot = await FailAsync("UPDATE \"AccessReviews\" SET \"SnapshotJson\" = '{}' WHERE \"Id\" = {0}", id);
        Assert.Contains("only change by being signed", snapshot.Message);
        var label = await FailAsync("UPDATE \"AccessReviews\" SET \"PeriodLabel\" = 'x' WHERE \"Id\" = {0}", id);
        Assert.Contains("only change by being signed", label.Message);
        // A Draft -> Draft update is not the signing transition.
        await FailAsync("UPDATE \"AccessReviews\" SET \"Conclusion\" = 'early' WHERE \"Id\" = {0}", id);
        // Signed without a reviewer or signature is not a valid signing either.
        await FailAsync("UPDATE \"AccessReviews\" SET \"Status\" = 1 WHERE \"Id\" = {0}", id);
        // Signing may not smuggle in a snapshot change.
        var signatureId = await NewSignatureAsync();
        var smuggle = await FailAsync(
            "UPDATE \"AccessReviews\" SET \"Status\" = 1, \"ReviewerUserId\" = {0}, \"SignedAt\" = now(), \"ElectronicSignatureId\" = {1}, \"SnapshotSha256\" = {2} WHERE \"Id\" = {3}",
            _fixture.SeededUserId, signatureId, new string('b', 64), id);
        Assert.Contains("snapshot", smuggle.Message, StringComparison.OrdinalIgnoreCase);

        await using var verify = _fixture.CreateDbContext();
        var stored = await verify.AccessReviews.AsNoTracking().SingleAsync(r => r.Id == id);
        Assert.Equal(AccessReviewStatus.Draft, stored.Status);
        Assert.Equal("{\"users\":[]}", stored.SnapshotJson);
    }

    [PostgresFact]
    public async Task SignedReview_RejectsUpdate_Delete_AndTruncate()
    {
        var id = await NewDraftAsync();
        await SignViaSqlAsync(id); // Draft -> Signed is allowed

        var update = await FailAsync("UPDATE \"AccessReviews\" SET \"Conclusion\" = 'changed' WHERE \"Id\" = {0}", id);
        Assert.Contains("signed and immutable", update.Message);
        await FailAsync("UPDATE \"AccessReviews\" SET \"SnapshotJson\" = '{}' WHERE \"Id\" = {0}", id);
        await FailAsync("UPDATE \"AccessReviews\" SET \"Status\" = 0 WHERE \"Id\" = {0}", id);

        var delete = await FailAsync("DELETE FROM \"AccessReviews\" WHERE \"Id\" = {0}", id);
        Assert.Contains("append-only", delete.Message, StringComparison.OrdinalIgnoreCase);

        var truncate = await FailAsync("TRUNCATE TABLE \"AccessReviews\" CASCADE");
        Assert.Contains("TRUNCATE", truncate.Message);

        await using var verify = _fixture.CreateDbContext();
        Assert.Equal("ok", (await verify.AccessReviews.AsNoTracking().SingleAsync(r => r.Id == id)).Conclusion);
    }

    [PostgresFact]
    public async Task DraftReview_CannotBeDeleted()
    {
        var id = await NewDraftAsync();
        var delete = await FailAsync("DELETE FROM \"AccessReviews\" WHERE \"Id\" = {0}", id);
        Assert.Contains("append-only", delete.Message, StringComparison.OrdinalIgnoreCase);
    }

    [PostgresFact]
    public async Task Findings_AreEditableOnlyWhileDraft_AndNeverDeletedOrTruncated()
    {
        var id = await NewDraftAsync();
        await using (var db = _fixture.CreateDbContext())
        {
            db.AccessReviewFindings.Add(new AccessReviewFinding
            {
                ReviewId = id, UserId = _fixture.SeededUserId, Kind = "Other", Detail = "first", RecordedByUserId = _fixture.SeededUserId
            });
            await db.SaveChangesAsync();
        }
        await using (var db = _fixture.CreateDbContext())
        {
            var finding = await db.AccessReviewFindings.SingleAsync(f => f.ReviewId == id);
            finding.Detail = "edited while draft";
            await db.SaveChangesAsync();

            // A finding never moves to another account, and is never deleted, draft or not.
            await FailAsync("UPDATE \"AccessReviewFindings\" SET \"UserId\" = {0} WHERE \"Id\" = {1}", _fixture.SeededControllerUserId, finding.Id);
            var delete = await FailAsync("DELETE FROM \"AccessReviewFindings\" WHERE \"Id\" = {0}", finding.Id);
            Assert.Contains("append-only", delete.Message, StringComparison.OrdinalIgnoreCase);
        }

        await SignViaSqlAsync(id);

        var update = await FailAsync("UPDATE \"AccessReviewFindings\" SET \"Detail\" = 'late edit' WHERE \"ReviewId\" = {0}", id);
        Assert.Contains("signed and immutable", update.Message);
        var insert = await FailAsync(
            "INSERT INTO \"AccessReviewFindings\" (\"ReviewId\", \"UserId\", \"Kind\", \"Detail\", \"Action\", \"RecordedByUserId\", \"RecordedAt\") VALUES ({0}, {1}, 'Other', 'late', 0, {1}, now())",
            id, _fixture.SeededUserId);
        Assert.Contains("signed and immutable", insert.Message);
        await FailAsync("DELETE FROM \"AccessReviewFindings\" WHERE \"ReviewId\" = {0}", id);
        var truncate = await FailAsync("TRUNCATE TABLE \"AccessReviewFindings\"");
        Assert.Contains("TRUNCATE", truncate.Message);

        await using var verify = _fixture.CreateDbContext();
        Assert.Equal("edited while draft", (await verify.AccessReviewFindings.AsNoTracking().SingleAsync(f => f.ReviewId == id)).Detail);
    }

    // The whole path against the real database: the service signs in one transaction and the trigger accepts it.
    [PostgresFact]
    public async Task Service_GeneratesAndSigns_OnPostgres_ThenTheRowIsFrozen()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        int reviewerId;
        await using (var setup = _fixture.CreateDbContext())
        {
            var roleId = await setup.Roles.Select(r => r.Id).FirstAsync();
            var reviewer = new User
            {
                FullName = "Pg Reviewer", Username = $"pg_reviewer_{tag}", RoleId = roleId,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Rev1ewer!Pass", 4)
            };
            setup.Users.Add(reviewer);
            await setup.SaveChangesAsync();
            reviewerId = reviewer.Id;
            // Has signed in before, so the report does not flag the signer.
            setup.LoginHistories.Add(new LoginHistory { UserId = reviewerId, Username = reviewer.Username, Success = true });
            await setup.SaveChangesAsync();
        }

        await using var db = _fixture.CreateDbContext();
        var service = new AccessReviewService(db, new ElectronicSignatureService(db));

        var review = await service.GenerateAsync($"PG {tag}", "integration test", _fixture.SeededUserId);
        Assert.Contains(review.Snapshot.Users, u => u.Username == $"pg_reviewer_{tag}");
        await service.AddFindingAsync(review.Id, new(_fixture.SeededUserId, "Other", "pg finding", "NoChange", null), _fixture.SeededUserId);
        await Assert.ThrowsAsync<SignatureVerificationException>(() => service.SignAsync(review.Id, "wrong", "Fine", reviewerId, null));

        var signed = await service.SignAsync(review.Id, "Rev1ewer!Pass", "Fine", reviewerId, null);

        Assert.Equal("Signed", signed.Status);
        await using var frozen = _fixture.CreateDbContext();
        await Assert.ThrowsAsync<PostgresException>(() =>
            frozen.Database.ExecuteSqlRawAsync("UPDATE \"AccessReviews\" SET \"Conclusion\" = 'x' WHERE \"Id\" = {0}", review.Id));
    }

    [PostgresFact]
    public async Task Migration_GrantsAccessReviewToTheAdministrator_WithAuditRowAndPermissionVersionBump_AndRollsBack()
    {
        await ScratchPostgresDatabase.RunAsync(async connectionString =>
        {
            await using var db = ScratchPostgresDatabase.NewContext(connectionString);
            var ids = db.GetService<IMigrationsAssembly>().Migrations.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            var target = ids.Single(k => k.EndsWith("_AddAccessReviews", StringComparison.Ordinal));
            var before = ids[ids.IndexOf(target) - 1];

            await db.Database.MigrateAsync(before);
            await db.Database.ExecuteSqlRawAsync(@"INSERT INTO ""Roles"" (""Type"", ""Name"", ""IsSystemRole"") VALUES (0, 'System Administrator', TRUE), (1, 'Section Head', FALSE);");
            var versionBefore = await db.Roles.Where(r => r.Type == RoleType.SystemAdministrator).Select(r => r.PermissionVersion).SingleAsync();

            await db.Database.MigrateAsync(target);
            db.ChangeTracker.Clear();

            var role = await db.Roles.AsNoTracking().SingleAsync(r => r.Type == RoleType.SystemAdministrator);
            var held = await db.RolePermissions.Where(rp => rp.RoleId == role.Id).Select(rp => rp.Permission!.Code).ToListAsync();
            Assert.Contains(PermissionConstants.AccessReview, held);
            Assert.True(await db.Permissions.AnyAsync(p => p.Code == PermissionConstants.AccessReview && p.IsEnforced));
            Assert.True(role.PermissionVersion > versionBefore, "PermissionVersion must be bumped so issued tokens are revalidated");

            var audit = await db.AuditLogs.SingleAsync(a => a.Action == "ROLE_PERMISSIONS_UPDATED" && a.NewValue!.Contains(PermissionConstants.AccessReview));
            Assert.Equal(AuditActionCategory.Security, audit.ActionCategory);
            Assert.Equal(role.Id.ToString(), audit.EntityId);
            Assert.Contains("access review", audit.Reason!, StringComparison.OrdinalIgnoreCase);

            // No other role receives it.
            var otherRole = await db.Roles.AsNoTracking().SingleAsync(r => r.Type == RoleType.SectionHead);
            Assert.False(await db.RolePermissions.AnyAsync(rp => rp.RoleId == otherRole.Id && rp.Permission!.Code == PermissionConstants.AccessReview));

            // Rolling back removes the grant, the permission and the trigger functions.
            await db.Database.MigrateAsync(before);
            db.ChangeTracker.Clear();
            Assert.False(await db.Permissions.AnyAsync(p => p.Code == PermissionConstants.AccessReview));
            var functions = await db.Database
                .SqlQueryRaw<string>("SELECT proname AS \"Value\" FROM pg_proc WHERE proname LIKE 'prevent_access_review%'")
                .ToListAsync();
            Assert.Empty(functions);
        });
    }
}
