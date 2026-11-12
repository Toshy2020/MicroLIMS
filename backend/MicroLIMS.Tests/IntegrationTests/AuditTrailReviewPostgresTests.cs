using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class AuditTrailReviewPostgresTests
{
    private const string QaPassword = "QaRev1ew!Pass";
    private readonly PostgresTestFixture _fixture;

    public AuditTrailReviewPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static AuditTrailReviewService CreateService(MicroLimsDbContext db, TimeProvider? clock = null)
    {
        var t = clock ?? TimeProvider.System;
        return new AuditTrailReviewService(
            db,
            new ElectronicSignatureService(db, t),
            new AuditSearchService(db),
            new PermissionService(db),
            t);
    }

    private static async Task<int> CreateUserWithPermissionsAsync(
        MicroLimsDbContext db,
        RoleType roleType,
        string usernamePrefix,
        string fullName,
        params string[] permissionCodes)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var role = new Role
        {
            Name = $"{usernamePrefix}_role_{tag}",
            Type = roleType,
            IsSystemRole = false,
            IsActive = true
        };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        if (permissionCodes.Length > 0)
        {
            var permIds = await db.Permissions
                .AsNoTracking()
                .Where(p => permissionCodes.Contains(p.Code))
                .Select(p => p.Id)
                .ToListAsync();
            foreach (var permId in permIds)
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permId });
            await db.SaveChangesAsync();
        }

        var user = new User
        {
            FullName = fullName,
            Username = $"{usernamePrefix}_{tag}",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(QaPassword, 4),
            RoleId = role.Id,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<(int AuditLogId, string EntityName, string RecordId)> SeedUniqueAuditEntryAsync(
        MicroLimsDbContext db,
        int userId,
        DateTime timestampUtc,
        string entityName,
        string recordId)
    {
        var entry = new AuditLog
        {
            EntityName = entityName,
            EntityId = recordId,
            Action = "Create",
            NewValue = JsonSerializer.Serialize(new { Tag = recordId }),
            UserId = userId,
            Timestamp = timestampUtc
        };
        db.AuditLogs.Add(entry);
        await db.SaveChangesAsync();
        return (entry.Id, entityName, recordId);
    }

    [PostgresFact]
    public async Task Start_persists_review_with_cutoff_and_count_and_audit_row()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var entityName = $"ATR_Start_{tag}";
        var t0 = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(t0);

        int qaUserId;
        int entry1Id;
        int entry2Id;
        await using (var arrangeDb = _fixture.CreateDbContext(clock))
        {
            qaUserId = await CreateUserWithPermissionsAsync(
                arrangeDb, RoleType.Qa, "qa_start", "QA Starter", PermissionConstants.AuditReview);
            (entry1Id, _, _) = await SeedUniqueAuditEntryAsync(arrangeDb, qaUserId, t0.UtcDateTime.AddHours(-2), entityName, "1");
            (entry2Id, _, _) = await SeedUniqueAuditEntryAsync(arrangeDb, qaUserId, t0.UtcDateTime.AddHours(-1), entityName, "2");
        }

        AuditTrailReviewDto started;
        await using (var actDb = _fixture.CreateDbContext(clock))
        {
            var svc = CreateService(actDb, clock);
            started = await svc.StartAsync(
                new StartAuditTrailReviewRequest(
                    $"Review {tag}",
                    t0.UtcDateTime.AddHours(-3),
                    t0.UtcDateTime,
                    AuditReviewScopeKind.EntityName,
                    entityName),
                qaUserId);
        }

        Assert.Equal(2, started.EntryCount);
        Assert.Equal(0, started.NewerEntriesCount);
        Assert.True(started.CutoffAuditLogId >= entry2Id);
        Assert.Equal(AuditTrailReviewStatus.InProgress, started.Status);

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.AuditTrailReviews.AsNoTracking().SingleAsync(r => r.Id == started.Id);
        Assert.Equal($"Review {tag}", persisted.Title);
        Assert.Equal(AuditReviewScopeKind.EntityName, persisted.ScopeKind);
        Assert.Equal(entityName, persisted.ScopeEntityName);
        Assert.Equal(2, persisted.EntryCount);
        Assert.Equal(qaUserId, persisted.StartedByUserId);
        Assert.Equal(t0.UtcDateTime, persisted.StartedAtUtc);
        Assert.True(persisted.CutoffAuditLogId >= entry2Id && entry2Id > entry1Id);

        var audit = await assertDb.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.EntityName == nameof(AuditTrailReview)
                              && a.EntityId == started.Id.ToString()
                              && a.Action == "Create");
        Assert.Equal(qaUserId, audit.UserId);
        Assert.Contains(entityName, audit.NewValue!);
    }

    [PostgresFact]
    public async Task Scope_is_fixed_at_start_and_later_entries_are_excluded()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var entityName = $"ATR_Fixed_{tag}";
        var tStart = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(tStart);

        int qaUserId;
        int initialEntryId;
        await using (var arrangeDb = _fixture.CreateDbContext(clock))
        {
            qaUserId = await CreateUserWithPermissionsAsync(
                arrangeDb, RoleType.Qa, "qa_fixed", "QA Fixed", PermissionConstants.AuditReview);
            (initialEntryId, _, _) = await SeedUniqueAuditEntryAsync(
                arrangeDb, qaUserId, tStart.UtcDateTime.AddMinutes(-30), entityName, "10");
        }

        AuditTrailReviewDto started;
        await using (var actDb = _fixture.CreateDbContext(clock))
        {
            started = await CreateService(actDb, clock).StartAsync(
                new StartAuditTrailReviewRequest(
                    $"Fixed Scope {tag}",
                    tStart.UtcDateTime.AddHours(-1),
                    tStart.UtcDateTime,
                    AuditReviewScopeKind.EntityName,
                    entityName),
                qaUserId);
        }

        // Write a later audit entry with a timestamp inside [PeriodFromUtc, PeriodToUtc]
        // but an AuditLog.Id strictly greater than CutoffAuditLogId.
        int laterEntryId;
        await using (var laterDb = _fixture.CreateDbContext(clock))
        {
            (laterEntryId, _, _) = await SeedUniqueAuditEntryAsync(
                laterDb, qaUserId, tStart.UtcDateTime.AddMinutes(-10), entityName, "11");
        }
        Assert.True(laterEntryId > started.CutoffAuditLogId);

        await using var assertDb = _fixture.CreateDbContext(clock);
        var svc = CreateService(assertDb, clock);
        var detail = await svc.GetAsync(started.Id);
        var entries = await svc.GetEntriesAsync(started.Id, 1, 25);

        Assert.Equal(1, detail.EntryCount);
        Assert.Equal(1, detail.NewerEntriesCount);
        Assert.Equal(1, entries.TotalCount);
        Assert.Single(entries.Items);
        Assert.Equal(initialEntryId, entries.Items[0].Id);
        Assert.DoesNotContain(entries.Items, e => e.Id == laterEntryId);
    }

    [PostgresFact]
    public async Task Finding_outside_scope_refused()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var scopedEntity = $"ATR_InScope_{tag}";
        var otherEntity = $"ATR_Other_{tag}";
        var t0 = new DateTimeOffset(2026, 6, 2, 10, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(t0);

        int qaUserId;
        int otherEntityEntryId;
        int beforePeriodEntryId;
        await using (var arrangeDb = _fixture.CreateDbContext(clock))
        {
            qaUserId = await CreateUserWithPermissionsAsync(
                arrangeDb, RoleType.Qa, "qa_out", "QA Out", PermissionConstants.AuditReview);
            await SeedUniqueAuditEntryAsync(arrangeDb, qaUserId, t0.UtcDateTime.AddMinutes(-30), scopedEntity, "1");
            (otherEntityEntryId, _, _) = await SeedUniqueAuditEntryAsync(arrangeDb, qaUserId, t0.UtcDateTime.AddMinutes(-30), otherEntity, "1");
            (beforePeriodEntryId, _, _) = await SeedUniqueAuditEntryAsync(arrangeDb, qaUserId, t0.UtcDateTime.AddDays(-5), scopedEntity, "2");
        }

        int reviewId;
        await using (var startDb = _fixture.CreateDbContext(clock))
        {
            var review = await CreateService(startDb, clock).StartAsync(
                new StartAuditTrailReviewRequest(
                    $"Out of scope {tag}",
                    t0.UtcDateTime.AddHours(-2),
                    t0.UtcDateTime,
                    AuditReviewScopeKind.EntityName,
                    scopedEntity),
                qaUserId);
            reviewId = review.Id;
        }

        int afterCutoffEntryId;
        await using (var laterDb = _fixture.CreateDbContext(clock))
        {
            (afterCutoffEntryId, _, _) = await SeedUniqueAuditEntryAsync(
                laterDb, qaUserId, t0.UtcDateTime.AddMinutes(-15), scopedEntity, "3");
        }

        await using (var actDb = _fixture.CreateDbContext(clock))
        {
            var svc = CreateService(actDb, clock);
            await Assert.ThrowsAsync<BusinessRuleException>(() => svc.AddFindingAsync(reviewId, otherEntityEntryId, "wrong entity", qaUserId));
            await Assert.ThrowsAsync<BusinessRuleException>(() => svc.AddFindingAsync(reviewId, beforePeriodEntryId, "before period", qaUserId));
            await Assert.ThrowsAsync<BusinessRuleException>(() => svc.AddFindingAsync(reviewId, afterCutoffEntryId, "after cutoff", qaUserId));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.AuditTrailReviewFindings.AsNoTracking().AnyAsync(f => f.ReviewId == reviewId));
    }

    [PostgresFact]
    public async Task Duplicate_finding_for_same_entry_refused()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var entityName = $"ATR_Dup_{tag}";
        var t0 = new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(t0);

        int qaUserId;
        int entryId;
        await using (var arrangeDb = _fixture.CreateDbContext(clock))
        {
            qaUserId = await CreateUserWithPermissionsAsync(
                arrangeDb, RoleType.Qa, "qa_dup", "QA Dup", PermissionConstants.AuditReview);
            (entryId, _, _) = await SeedUniqueAuditEntryAsync(arrangeDb, qaUserId, t0.UtcDateTime.AddMinutes(-10), entityName, "1");
        }

        int reviewId;
        await using (var startDb = _fixture.CreateDbContext(clock))
        {
            var review = await CreateService(startDb, clock).StartAsync(
                new StartAuditTrailReviewRequest($"Dup {tag}", t0.UtcDateTime.AddHours(-1), t0.UtcDateTime, AuditReviewScopeKind.EntityName, entityName),
                qaUserId);
            reviewId = review.Id;
        }

        await using (var firstDb = _fixture.CreateDbContext(clock))
        {
            await CreateService(firstDb, clock).AddFindingAsync(reviewId, entryId, "first note", qaUserId);
        }

        await using (var secondDb = _fixture.CreateDbContext(clock))
        {
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                CreateService(secondDb, clock).AddFindingAsync(reviewId, entryId, "second note", qaUserId));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.Equal(1, await assertDb.AuditTrailReviewFindings.AsNoTracking().CountAsync(f => f.ReviewId == reviewId && f.AuditLogId == entryId));
    }

    [PostgresFact]
    public async Task Sign_requires_correct_password()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var entityName = $"ATR_WrongPw_{tag}";
        var t0 = new DateTimeOffset(2026, 6, 4, 10, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(t0);

        int qaUserId;
        await using (var arrangeDb = _fixture.CreateDbContext(clock))
        {
            qaUserId = await CreateUserWithPermissionsAsync(
                arrangeDb, RoleType.Qa, "qa_pw", "QA Password", PermissionConstants.AuditReview);
        }

        AuditTrailReviewDto started;
        await using (var startDb = _fixture.CreateDbContext(clock))
        {
            started = await CreateService(startDb, clock).StartAsync(
                new StartAuditTrailReviewRequest($"Wrong pw {tag}", t0.UtcDateTime.AddHours(-1), t0.UtcDateTime, AuditReviewScopeKind.EntityName, entityName),
                qaUserId);
        }

        await using (var actDb = _fixture.CreateDbContext(clock))
        {
            await Assert.ThrowsAsync<SignatureVerificationException>(() =>
                CreateService(actDb, clock).SignAsync(started.Id, "WrongPassword!", "Looks fine", started.Version, qaUserId, "127.0.0.1"));
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.AuditTrailReviews.AsNoTracking().SingleAsync(r => r.Id == started.Id);
        Assert.Equal(AuditTrailReviewStatus.InProgress, persisted.Status);
        Assert.Null(persisted.ElectronicSignatureId);
        Assert.False(await assertDb.ElectronicSignatures.AsNoTracking()
            .AnyAsync(s => s.EntityType == "AuditTrailReview" && s.EntityId == started.Id));
        Assert.True(await assertDb.AuditLogs.AsNoTracking()
            .AnyAsync(a => a.EntityName == "ElectronicSignature" && a.Action == "SignatureFailed" && a.EntityId == $"AuditTrailReview:{started.Id}"));
    }

    [PostgresFact]
    public async Task Sign_binds_signature_to_review_and_sets_outcome()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var entityName = $"ATR_Sign_{tag}";
        var t0 = new DateTimeOffset(2026, 6, 5, 10, 0, 0, TimeSpan.Zero);
        var tSign = t0.AddMinutes(15);

        int qaUserId;
        int entryId;
        await using (var arrangeDb = _fixture.CreateDbContext(new FixedTimeProvider(t0)))
        {
            qaUserId = await CreateUserWithPermissionsAsync(
                arrangeDb, RoleType.Qa, "qa_sign", "QA Signer Full", PermissionConstants.AuditReview);
            (entryId, _, _) = await SeedUniqueAuditEntryAsync(arrangeDb, qaUserId, t0.UtcDateTime.AddMinutes(-10), entityName, "1");
        }

        AuditTrailReviewDto startedNoFindings;
        AuditTrailReviewDto startedWithFindings;
        await using (var startDb = _fixture.CreateDbContext(new FixedTimeProvider(t0)))
        {
            var svc = CreateService(startDb, new FixedTimeProvider(t0));
            startedNoFindings = await svc.StartAsync(
                new StartAuditTrailReviewRequest($"Sign clean {tag}", t0.UtcDateTime.AddHours(-1), t0.UtcDateTime, AuditReviewScopeKind.EntityName, entityName),
                qaUserId);
            startedWithFindings = await svc.StartAsync(
                new StartAuditTrailReviewRequest($"Sign finding {tag}", t0.UtcDateTime.AddHours(-1), t0.UtcDateTime, AuditReviewScopeKind.EntityName, entityName),
                qaUserId);
            await svc.AddFindingAsync(startedWithFindings.Id, entryId, "Investigate manual override", qaUserId);
        }

        AuditTrailReviewDto signedClean;
        AuditTrailReviewDto signedFinding;
        await using (var actDb = _fixture.CreateDbContext(new FixedTimeProvider(tSign)))
        {
            var svc = CreateService(actDb, new FixedTimeProvider(tSign));
            signedClean = await svc.SignAsync(startedNoFindings.Id, QaPassword, "No anomalies found", startedNoFindings.Version, qaUserId, "10.0.0.1");
            signedFinding = await svc.SignAsync(startedWithFindings.Id, QaPassword, "One finding recorded", startedWithFindings.Version, qaUserId, "10.0.0.1");
        }

        Assert.Equal(AuditTrailReviewStatus.Signed, signedClean.Status);
        Assert.Equal(AuditTrailReviewOutcome.NoFindings, signedClean.Outcome);
        Assert.Equal("QA Signer Full", signedClean.SignerFullName);
        Assert.Equal("Audit trail review", signedClean.SignatureMeaningText);

        Assert.Equal(AuditTrailReviewStatus.Signed, signedFinding.Status);
        Assert.Equal(AuditTrailReviewOutcome.FindingsRecorded, signedFinding.Outcome);

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.AuditTrailReviews.AsNoTracking().SingleAsync(r => r.Id == startedWithFindings.Id);
        Assert.Equal(AuditTrailReviewStatus.Signed, persisted.Status);
        Assert.Equal(AuditTrailReviewOutcome.FindingsRecorded, persisted.Outcome);
        Assert.Equal("One finding recorded", persisted.Conclusion);
        Assert.Equal(tSign.UtcDateTime, persisted.SignedAtUtc);
        Assert.NotNull(persisted.ElectronicSignatureId);

        var sig = await assertDb.ElectronicSignatures.AsNoTracking().SingleAsync(s => s.Id == persisted.ElectronicSignatureId);
        Assert.Equal(qaUserId, sig.UserId);
        Assert.Equal(SignatureMeaning.AuditTrailReviewed, sig.MeaningOfSignature);
        Assert.Equal("AuditTrailReview", sig.EntityType);
        Assert.Equal(startedWithFindings.Id, sig.EntityId);
        Assert.Equal("One finding recorded", sig.Comment);

        var audit = await assertDb.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.EntityName == nameof(AuditTrailReview)
                              && a.EntityId == startedWithFindings.Id.ToString()
                              && a.Action == "Update");
        Assert.Equal(qaUserId, audit.UserId);
        Assert.NotNull(audit.PreviousValue);
        Assert.NotNull(audit.NewValue);
        Assert.Contains("\"Status\":0", audit.PreviousValue);
        Assert.Contains("\"Status\":1", audit.NewValue);
    }

    [PostgresFact]
    public async Task Sign_by_other_user_refused()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var entityName = $"ATR_OtherSign_{tag}";
        var t0 = new DateTimeOffset(2026, 6, 6, 10, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(t0);

        int starterId;
        int otherQaId;
        await using (var arrangeDb = _fixture.CreateDbContext(clock))
        {
            starterId = await CreateUserWithPermissionsAsync(
                arrangeDb, RoleType.Qa, "qa_starter", "QA Starter", PermissionConstants.AuditReview);
            otherQaId = await CreateUserWithPermissionsAsync(
                arrangeDb, RoleType.Qa, "qa_other", "QA Other", PermissionConstants.AuditReview);
        }

        AuditTrailReviewDto started;
        await using (var startDb = _fixture.CreateDbContext(clock))
        {
            started = await CreateService(startDb, clock).StartAsync(
                new StartAuditTrailReviewRequest($"Other sign {tag}", t0.UtcDateTime.AddHours(-1), t0.UtcDateTime, AuditReviewScopeKind.EntityName, entityName),
                starterId);
        }

        await using (var actDb = _fixture.CreateDbContext(clock))
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                CreateService(actDb, clock).SignAsync(started.Id, QaPassword, "Trying to sign", started.Version, otherQaId, null));
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.AuditTrailReviews.AsNoTracking().SingleAsync(r => r.Id == started.Id);
        Assert.Equal(AuditTrailReviewStatus.InProgress, persisted.Status);
        Assert.Null(persisted.ElectronicSignatureId);
    }

    [PostgresFact]
    public async Task Sign_with_stale_version_refused()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var entityName = $"ATR_Stale_{tag}";
        var t0 = new DateTimeOffset(2026, 6, 7, 10, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(t0);

        int qaUserId;
        await using (var arrangeDb = _fixture.CreateDbContext(clock))
        {
            qaUserId = await CreateUserWithPermissionsAsync(
                arrangeDb, RoleType.Qa, "qa_stale", "QA Stale", PermissionConstants.AuditReview);
        }

        AuditTrailReviewDto started;
        await using (var startDb = _fixture.CreateDbContext(clock))
        {
            started = await CreateService(startDb, clock).StartAsync(
                new StartAuditTrailReviewRequest($"Stale {tag}", t0.UtcDateTime.AddHours(-1), t0.UtcDateTime, AuditReviewScopeKind.EntityName, entityName),
                qaUserId);
        }

        var staleVersion = started.Version + 1;
        await using (var actDb = _fixture.CreateDbContext(clock))
        {
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
                CreateService(actDb, clock).SignAsync(started.Id, QaPassword, "Stale sign", staleVersion, qaUserId, null));
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.AuditTrailReviews.AsNoTracking().SingleAsync(r => r.Id == started.Id);
        Assert.Equal(AuditTrailReviewStatus.InProgress, persisted.Status);
        Assert.Null(persisted.ElectronicSignatureId);
    }

    [PostgresFact]
    public async Task Signed_review_rejects_new_findings()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var entityName = $"ATR_NoNewFinding_{tag}";
        var t0 = new DateTimeOffset(2026, 6, 8, 10, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(t0);

        int qaUserId;
        int entryId;
        await using (var arrangeDb = _fixture.CreateDbContext(clock))
        {
            qaUserId = await CreateUserWithPermissionsAsync(
                arrangeDb, RoleType.Qa, "qa_nofind", "QA NoFind", PermissionConstants.AuditReview);
            (entryId, _, _) = await SeedUniqueAuditEntryAsync(arrangeDb, qaUserId, t0.UtcDateTime.AddMinutes(-10), entityName, "1");
        }

        AuditTrailReviewDto signed;
        await using (var startDb = _fixture.CreateDbContext(clock))
        {
            var svc = CreateService(startDb, clock);
            var started = await svc.StartAsync(
                new StartAuditTrailReviewRequest($"Signed {tag}", t0.UtcDateTime.AddHours(-1), t0.UtcDateTime, AuditReviewScopeKind.EntityName, entityName),
                qaUserId);
            signed = await svc.SignAsync(started.Id, QaPassword, "Clean", started.Version, qaUserId, null);
        }

        await using (var actDb = _fixture.CreateDbContext(clock))
        {
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                CreateService(actDb, clock).AddFindingAsync(signed.Id, entryId, "Too late", qaUserId));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.AuditTrailReviewFindings.AsNoTracking().AnyAsync(f => f.ReviewId == signed.Id));
    }

    [PostgresFact]
    public async Task Signed_review_update_and_delete_rejected_by_trigger()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var entityName = $"ATR_TrigRev_{tag}";
        var t0 = new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(t0);

        int qaUserId;
        await using (var arrangeDb = _fixture.CreateDbContext(clock))
        {
            qaUserId = await CreateUserWithPermissionsAsync(
                arrangeDb, RoleType.Qa, "qa_trig", "QA Trig", PermissionConstants.AuditReview);
        }

        int inProgressId;
        int signedId;
        await using (var startDb = _fixture.CreateDbContext(clock))
        {
            var svc = CreateService(startDb, clock);
            var r1 = await svc.StartAsync(
                new StartAuditTrailReviewRequest($"Draft {tag}", t0.UtcDateTime.AddHours(-1), t0.UtcDateTime, AuditReviewScopeKind.EntityName, entityName),
                qaUserId);
            inProgressId = r1.Id;

            var r2 = await svc.StartAsync(
                new StartAuditTrailReviewRequest($"Signed {tag}", t0.UtcDateTime.AddHours(-1), t0.UtcDateTime, AuditReviewScopeKind.EntityName, entityName),
                qaUserId);
            await svc.SignAsync(r2.Id, QaPassword, "Original conclusion", r2.Version, qaUserId, null);
            signedId = r2.Id;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            await Assert.ThrowsAsync<PostgresException>(() =>
                actDb.Database.ExecuteSqlRawAsync("UPDATE \"AuditTrailReviews\" SET \"Conclusion\" = 'Tampered' WHERE \"Id\" = {0}", signedId));
            await Assert.ThrowsAsync<PostgresException>(() =>
                actDb.Database.ExecuteSqlRawAsync("DELETE FROM \"AuditTrailReviews\" WHERE \"Id\" = {0}", signedId));
            await Assert.ThrowsAsync<PostgresException>(() =>
                actDb.Database.ExecuteSqlRawAsync("DELETE FROM \"AuditTrailReviews\" WHERE \"Id\" = {0}", inProgressId));
            // The scope is fixed at start: not editable even while the review is still in progress.
            await Assert.ThrowsAsync<PostgresException>(() =>
                actDb.Database.ExecuteSqlRawAsync("UPDATE \"AuditTrailReviews\" SET \"CutoffAuditLogId\" = \"CutoffAuditLogId\" + 1000 WHERE \"Id\" = {0}", inProgressId));
            await Assert.ThrowsAsync<PostgresException>(() =>
                actDb.Database.ExecuteSqlRawAsync("UPDATE \"AuditTrailReviews\" SET \"PeriodFromUtc\" = \"PeriodFromUtc\" - interval '1 day' WHERE \"Id\" = {0}", inProgressId));
            await Assert.ThrowsAsync<PostgresException>(() =>
                actDb.Database.ExecuteSqlRawAsync("UPDATE \"AuditTrailReviews\" SET \"ScopeEntityName\" = 'Other' WHERE \"Id\" = {0}", inProgressId));
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.AuditTrailReviews.AsNoTracking().SingleAsync(r => r.Id == signedId);
        Assert.Equal("Original conclusion", persisted.Conclusion);
        Assert.True(await assertDb.AuditTrailReviews.AsNoTracking().AnyAsync(r => r.Id == inProgressId));
    }

    [PostgresFact]
    public async Task Finding_delete_rejected_by_trigger()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var entityName = $"ATR_TrigFDel_{tag}";
        var t0 = new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(t0);

        int qaUserId;
        int entryId;
        await using (var arrangeDb = _fixture.CreateDbContext(clock))
        {
            qaUserId = await CreateUserWithPermissionsAsync(
                arrangeDb, RoleType.Qa, "qa_fdel", "QA FDel", PermissionConstants.AuditReview);
            (entryId, _, _) = await SeedUniqueAuditEntryAsync(arrangeDb, qaUserId, t0.UtcDateTime.AddMinutes(-10), entityName, "1");
        }

        int findingId;
        await using (var startDb = _fixture.CreateDbContext(clock))
        {
            var svc = CreateService(startDb, clock);
            var review = await svc.StartAsync(
                new StartAuditTrailReviewRequest($"FDel {tag}", t0.UtcDateTime.AddHours(-1), t0.UtcDateTime, AuditReviewScopeKind.EntityName, entityName),
                qaUserId);
            var finding = await svc.AddFindingAsync(review.Id, entryId, "Cannot be deleted", qaUserId);
            findingId = finding.Id;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            await Assert.ThrowsAsync<PostgresException>(() =>
                actDb.Database.ExecuteSqlRawAsync("DELETE FROM \"AuditTrailReviewFindings\" WHERE \"Id\" = {0}", findingId));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.True(await assertDb.AuditTrailReviewFindings.AsNoTracking().AnyAsync(f => f.Id == findingId));
    }

    [PostgresFact]
    public async Task Close_finding_once_only()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var entityName = $"ATR_CloseOnce_{tag}";
        var t0 = new DateTimeOffset(2026, 6, 11, 10, 0, 0, TimeSpan.Zero);
        var tClose = t0.AddMinutes(30);

        int qaUserId;
        int entryId;
        await using (var arrangeDb = _fixture.CreateDbContext(new FixedTimeProvider(t0)))
        {
            qaUserId = await CreateUserWithPermissionsAsync(
                arrangeDb, RoleType.Qa, "qa_close", "QA Closer", PermissionConstants.AuditReview);
            (entryId, _, _) = await SeedUniqueAuditEntryAsync(arrangeDb, qaUserId, t0.UtcDateTime.AddMinutes(-10), entityName, "1");
        }

        int findingId;
        await using (var startDb = _fixture.CreateDbContext(new FixedTimeProvider(t0)))
        {
            var svc = CreateService(startDb, new FixedTimeProvider(t0));
            var review = await svc.StartAsync(
                new StartAuditTrailReviewRequest($"Close {tag}", t0.UtcDateTime.AddHours(-1), t0.UtcDateTime, AuditReviewScopeKind.EntityName, entityName),
                qaUserId);
            var finding = await svc.AddFindingAsync(review.Id, entryId, "Needs follow-up", qaUserId);
            findingId = finding.Id;
        }

        AuditTrailReviewFindingDto closed;
        await using (var actDb = _fixture.CreateDbContext(new FixedTimeProvider(tClose)))
        {
            closed = await CreateService(actDb, new FixedTimeProvider(tClose))
                .CloseFindingAsync(findingId, "Verified and resolved", qaUserId);
        }

        Assert.Equal(AuditFindingStatus.Closed, closed.Status);
        Assert.Equal("Verified and resolved", closed.ClosedComment);
        Assert.Equal(qaUserId, closed.ClosedByUserId);
        Assert.Equal(tClose.UtcDateTime, closed.ClosedAtUtc);

        await using (var secondActDb = _fixture.CreateDbContext(new FixedTimeProvider(tClose)))
        {
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                CreateService(secondActDb, new FixedTimeProvider(tClose))
                    .CloseFindingAsync(findingId, "Second close", qaUserId));
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.AuditTrailReviewFindings.AsNoTracking().SingleAsync(f => f.Id == findingId);
        Assert.Equal(AuditFindingStatus.Closed, persisted.Status);
        Assert.Equal("Verified and resolved", persisted.ClosedComment);
        Assert.Equal(qaUserId, persisted.ClosedByUserId);
        Assert.Equal(tClose.UtcDateTime, persisted.ClosedAtUtc);

        var updateAudit = await assertDb.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.EntityName == nameof(AuditTrailReviewFinding)
                              && a.EntityId == findingId.ToString()
                              && a.Action == "Update");
        Assert.Equal(qaUserId, updateAudit.UserId);
        Assert.Contains("\"Status\":0", updateAudit.PreviousValue!);
        Assert.Contains("\"Status\":1", updateAudit.NewValue!);
    }

    [PostgresFact]
    public async Task Period_in_future_refused()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var t0 = new DateTimeOffset(2026, 6, 12, 10, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(t0);

        int qaUserId;
        await using (var arrangeDb = _fixture.CreateDbContext(clock))
        {
            qaUserId = await CreateUserWithPermissionsAsync(
                arrangeDb, RoleType.Qa, "qa_future", "QA Future", PermissionConstants.AuditReview);
        }

        await using (var actDb = _fixture.CreateDbContext(clock))
        {
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                CreateService(actDb, clock).StartAsync(
                    new StartAuditTrailReviewRequest(
                        $"Future {tag}",
                        t0.UtcDateTime.AddHours(-1),
                        t0.UtcDateTime.AddMinutes(5),
                        AuditReviewScopeKind.All),
                    qaUserId));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.AuditTrailReviews.AsNoTracking().AnyAsync(r => r.Title == $"Future {tag}"));
    }

    [PostgresFact]
    public async Task Finding_update_of_note_rejected_by_trigger()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var entityName = $"ATR_TrigFNote_{tag}";
        var t0 = new DateTimeOffset(2026, 6, 13, 10, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(t0);

        int qaUserId;
        int entryId;
        await using (var arrangeDb = _fixture.CreateDbContext(clock))
        {
            qaUserId = await CreateUserWithPermissionsAsync(
                arrangeDb, RoleType.Qa, "qa_fnote", "QA FNote", PermissionConstants.AuditReview);
            (entryId, _, _) = await SeedUniqueAuditEntryAsync(arrangeDb, qaUserId, t0.UtcDateTime.AddMinutes(-10), entityName, "1");
        }

        int findingId;
        await using (var startDb = _fixture.CreateDbContext(clock))
        {
            var svc = CreateService(startDb, clock);
            var review = await svc.StartAsync(
                new StartAuditTrailReviewRequest($"FNote {tag}", t0.UtcDateTime.AddHours(-1), t0.UtcDateTime, AuditReviewScopeKind.EntityName, entityName),
                qaUserId);
            var finding = await svc.AddFindingAsync(review.Id, entryId, "Original note", qaUserId);
            findingId = finding.Id;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            // Updating Note alone is rejected
            await Assert.ThrowsAsync<PostgresException>(() =>
                actDb.Database.ExecuteSqlRawAsync("UPDATE \"AuditTrailReviewFindings\" SET \"Note\" = 'Altered note' WHERE \"Id\" = {0}", findingId));

            // Smuggling a Note change alongside a valid Open -> Closed transition is also rejected
            await Assert.ThrowsAsync<PostgresException>(() =>
                actDb.Database.ExecuteSqlRawAsync(
                    "UPDATE \"AuditTrailReviewFindings\" SET \"Status\" = 1, \"ClosedComment\" = 'ok', \"ClosedByUserId\" = {0}, \"ClosedAtUtc\" = now(), \"Note\" = 'Altered note' WHERE \"Id\" = {1}",
                    qaUserId, findingId));
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.AuditTrailReviewFindings.AsNoTracking().SingleAsync(f => f.Id == findingId);
        Assert.Equal("Original note", persisted.Note);
        Assert.Equal(AuditFindingStatus.Open, persisted.Status);
    }

    [PostgresFact]
    public async Task Closed_finding_cannot_be_reopened_by_trigger()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var entityName = $"ATR_TrigReopen_{tag}";
        var t0 = new DateTimeOffset(2026, 6, 14, 10, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(t0);

        int qaUserId;
        int entryId;
        await using (var arrangeDb = _fixture.CreateDbContext(clock))
        {
            qaUserId = await CreateUserWithPermissionsAsync(
                arrangeDb, RoleType.Qa, "qa_reopen", "QA Reopen", PermissionConstants.AuditReview);
            (entryId, _, _) = await SeedUniqueAuditEntryAsync(arrangeDb, qaUserId, t0.UtcDateTime.AddMinutes(-10), entityName, "1");
        }

        int findingId;
        await using (var startDb = _fixture.CreateDbContext(clock))
        {
            var svc = CreateService(startDb, clock);
            var review = await svc.StartAsync(
                new StartAuditTrailReviewRequest($"Reopen {tag}", t0.UtcDateTime.AddHours(-1), t0.UtcDateTime, AuditReviewScopeKind.EntityName, entityName),
                qaUserId);
            var finding = await svc.AddFindingAsync(review.Id, entryId, "Initial note", qaUserId);
            await svc.CloseFindingAsync(finding.Id, "Closed once", qaUserId);
            findingId = finding.Id;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            await Assert.ThrowsAsync<PostgresException>(() =>
                actDb.Database.ExecuteSqlRawAsync("UPDATE \"AuditTrailReviewFindings\" SET \"Status\" = 0 WHERE \"Id\" = {0}", findingId));
            await Assert.ThrowsAsync<PostgresException>(() =>
                actDb.Database.ExecuteSqlRawAsync("UPDATE \"AuditTrailReviewFindings\" SET \"ClosedComment\" = 'Rewritten' WHERE \"Id\" = {0}", findingId));
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.AuditTrailReviewFindings.AsNoTracking().SingleAsync(f => f.Id == findingId);
        Assert.Equal(AuditFindingStatus.Closed, persisted.Status);
        Assert.Equal("Closed once", persisted.ClosedComment);
    }

    [PostgresFact]
    public async Task Truncate_rejected_by_trigger()
    {
        await using var db = _fixture.CreateDbContext();
        var ex1 = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"AuditTrailReviews\" CASCADE"));
        Assert.Contains("TRUNCATE", ex1.Message);

        var ex2 = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"AuditTrailReviewFindings\""));
        Assert.Contains("TRUNCATE", ex2.Message);
    }

    [PostgresFact]
    public async Task User_without_audit_review_permission_is_refused_and_nothing_persisted()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var entityName = $"ATR_NoPerm_{tag}";
        var t0 = new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(t0);

        int qaUserId;
        int analystUserId;
        int entryId;
        await using (var arrangeDb = _fixture.CreateDbContext(clock))
        {
            qaUserId = await CreateUserWithPermissionsAsync(
                arrangeDb, RoleType.Qa, "qa_perm", "QA Perm", PermissionConstants.AuditReview);
            analystUserId = await CreateUserWithPermissionsAsync(
                arrangeDb, RoleType.Analyst, "analyst_noperm", "Analyst NoPerm", PermissionConstants.AuditView);
            (entryId, _, _) = await SeedUniqueAuditEntryAsync(arrangeDb, qaUserId, t0.UtcDateTime.AddMinutes(-10), entityName, "1");
        }

        AuditTrailReviewDto review;
        AuditTrailReviewFindingDto finding;
        await using (var setupDb = _fixture.CreateDbContext(clock))
        {
            var svc = CreateService(setupDb, clock);
            review = await svc.StartAsync(
                new StartAuditTrailReviewRequest($"Perm {tag}", t0.UtcDateTime.AddHours(-1), t0.UtcDateTime, AuditReviewScopeKind.EntityName, entityName),
                qaUserId);
            finding = await svc.AddFindingAsync(review.Id, entryId, "Open finding", qaUserId);
        }

        await using (var actDb = _fixture.CreateDbContext(clock))
        {
            var svc = CreateService(actDb, clock);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                svc.StartAsync(
                    new StartAuditTrailReviewRequest($"Unauth {tag}", t0.UtcDateTime.AddHours(-1), t0.UtcDateTime, AuditReviewScopeKind.EntityName, entityName),
                    analystUserId));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                svc.AddFindingAsync(review.Id, entryId, "Unauthorized finding", analystUserId));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                svc.SignAsync(review.Id, QaPassword, "Unauthorized sign", review.Version, analystUserId, null));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                svc.CloseFindingAsync(finding.Id, "Unauthorized close", analystUserId));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.AuditTrailReviews.AsNoTracking().AnyAsync(r => r.Title == $"Unauth {tag}"));
        var persistedReview = await assertDb.AuditTrailReviews.AsNoTracking().SingleAsync(r => r.Id == review.Id);
        Assert.Equal(AuditTrailReviewStatus.InProgress, persistedReview.Status);
        var persistedFinding = await assertDb.AuditTrailReviewFindings.AsNoTracking().SingleAsync(f => f.Id == finding.Id);
        Assert.Equal(AuditFindingStatus.Open, persistedFinding.Status);
    }

    [PostgresFact]
    public async Task Entries_page_size_is_clamped_to_100()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var entityName = $"ATR_Page_{tag}";
        var t0 = new DateTimeOffset(2026, 6, 16, 10, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(t0);

        int qaUserId;
        await using (var arrangeDb = _fixture.CreateDbContext(clock))
        {
            qaUserId = await CreateUserWithPermissionsAsync(
                arrangeDb, RoleType.Qa, "qa_page", "QA Page", PermissionConstants.AuditReview);
            for (var i = 1; i <= 105; i++)
            {
                arrangeDb.AuditLogs.Add(new AuditLog
                {
                    EntityName = entityName,
                    EntityId = i.ToString(),
                    Action = "Create",
                    UserId = qaUserId,
                    Timestamp = t0.UtcDateTime.AddMinutes(-5)
                });
            }
            await arrangeDb.SaveChangesAsync();
        }

        int reviewId;
        await using (var startDb = _fixture.CreateDbContext(clock))
        {
            var review = await CreateService(startDb, clock).StartAsync(
                new StartAuditTrailReviewRequest($"Page clamp {tag}", t0.UtcDateTime.AddHours(-1), t0.UtcDateTime, AuditReviewScopeKind.EntityName, entityName),
                qaUserId);
            reviewId = review.Id;
        }

        await using var assertDb = _fixture.CreateDbContext(clock);
        var page1 = await CreateService(assertDb, clock).GetEntriesAsync(reviewId, 1, 500);
        Assert.Equal(100, page1.PageSize);
        Assert.Equal(100, page1.Items.Count);
        Assert.Equal(105, page1.TotalCount);
    }

    [PostgresFact]
    public async Task Record_scope_lists_only_that_records_entries()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var entityName = $"ATR_Rec_{tag}";
        var targetRecordId = $"REC-A-{tag}";
        var otherRecordId = $"REC-B-{tag}";
        var t0 = new DateTimeOffset(2026, 6, 17, 10, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(t0);

        int qaUserId;
        int target1;
        int target2;
        int other1;
        await using (var arrangeDb = _fixture.CreateDbContext(clock))
        {
            qaUserId = await CreateUserWithPermissionsAsync(
                arrangeDb, RoleType.Qa, "qa_rec", "QA Record", PermissionConstants.AuditReview);
            (target1, _, _) = await SeedUniqueAuditEntryAsync(arrangeDb, qaUserId, t0.UtcDateTime.AddMinutes(-20), entityName, targetRecordId);
            (target2, _, _) = await SeedUniqueAuditEntryAsync(arrangeDb, qaUserId, t0.UtcDateTime.AddMinutes(-10), entityName, targetRecordId);
            (other1, _, _) = await SeedUniqueAuditEntryAsync(arrangeDb, qaUserId, t0.UtcDateTime.AddMinutes(-15), entityName, otherRecordId);
        }

        int reviewId;
        await using (var startDb = _fixture.CreateDbContext(clock))
        {
            var review = await CreateService(startDb, clock).StartAsync(
                new StartAuditTrailReviewRequest(
                    $"Record scope {tag}",
                    t0.UtcDateTime.AddHours(-1),
                    t0.UtcDateTime,
                    AuditReviewScopeKind.Record,
                    entityName,
                    targetRecordId),
                qaUserId);
            reviewId = review.Id;
            Assert.Equal(2, review.EntryCount);
        }

        await using var assertDb = _fixture.CreateDbContext(clock);
        var svc = CreateService(assertDb, clock);
        var entries = await svc.GetEntriesAsync(reviewId, 1, 25);

        Assert.Equal(2, entries.TotalCount);
        Assert.Equal(2, entries.Items.Count);
        Assert.All(entries.Items, item =>
        {
            Assert.Equal(entityName, item.EntityName);
            Assert.Equal(targetRecordId, item.EntityId);
        });
        Assert.Contains(entries.Items, e => e.Id == target1);
        Assert.Contains(entries.Items, e => e.Id == target2);
        Assert.DoesNotContain(entries.Items, e => e.Id == other1);
    }
}
