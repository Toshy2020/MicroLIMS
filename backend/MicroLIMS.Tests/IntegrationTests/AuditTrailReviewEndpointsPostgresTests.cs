using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Pdf;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;
using MicroLIMS.Tests.ArchitectureTests;
using MicroLIMS.Tests.Fixtures;
using UglyToad.PdfPig;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class AuditTrailReviewEndpointsPostgresTests
{
    private const string TestPassword = "QaRev1ew!Pass";
    private readonly PostgresTestFixture _fixture;

    public AuditTrailReviewEndpointsPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static AuditTrailReviewsController CreateController(
        MicroLimsDbContext db,
        int userId,
        RoleType roleType,
        TimeProvider? clock = null)
    {
        var t = clock ?? TimeProvider.System;
        var service = new AuditTrailReviewService(
            db,
            new ElectronicSignatureService(db, t),
            new AuditSearchService(db),
            new PermissionService(db),
            t);
        var exportAudit = new DataExportAuditService(db, t);
        var pdfGenerator = new PdfGenerator();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, $"user_{userId}"),
            new(ClaimTypes.Role, roleType.ToString())
        };
        var identity = new ClaimsIdentity(claims, "PostgresTestAuth");

        return new AuditTrailReviewsController(service, pdfGenerator, exportAudit)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity),
                    Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1") }
                }
            }
        };
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
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(TestPassword, 4),
            RoleId = role.Id,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<int> SeedAuditEntryAsync(
        MicroLimsDbContext db,
        int userId,
        DateTime timestampUtc,
        string entityName,
        string recordId,
        string? newValue = null)
    {
        var entry = new AuditLog
        {
            EntityName = entityName,
            EntityId = recordId,
            Action = "Update",
            NewValue = newValue ?? JsonSerializer.Serialize(new { SecretField = $"raw-entry-{recordId}" }),
            UserId = userId,
            Timestamp = timestampUtc
        };
        db.AuditLogs.Add(entry);
        await db.SaveChangesAsync();
        return entry.Id;
    }

    private static T UnwrapOk<T>(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(ok.Value);
        Assert.True(response.Success);
        return Assert.IsAssignableFrom<T>(response.Data);
    }

    private static string ExtractPdfText(byte[] pdfBytes)
    {
        using var pdf = PdfDocument.Open(pdfBytes);
        return string.Join("\n", pdf.GetPages().Select(p => string.Join(" ", p.GetWords().Select(w => w.Text))));
    }

    [PostgresFact]
    public async Task Reviewer_without_audit_review_cannot_start()
    {
        var allowed = AuthorizationMatrixTests.AllowedRoles<AuditTrailReviewsController>(nameof(AuditTrailReviewsController.Start));
        Assert.NotNull(allowed);
        Assert.DoesNotContain(RoleType.Reviewer, allowed!);
        Assert.Equal(new[] { RoleType.Qa }, allowed.OrderBy(r => r).ToArray());

        var tag = Guid.NewGuid().ToString("N")[..8];
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero));
        int reviewerId;
        await using (var arrangeDb = _fixture.CreateDbContext(clock))
        {
            reviewerId = await CreateUserWithPermissionsAsync(
                arrangeDb,
                RoleType.Reviewer,
                "rev",
                $"Reviewer {tag}",
                PermissionConstants.SamplesReview);
        }

        await using (var actDb = _fixture.CreateDbContext(clock))
        {
            var controller = CreateController(actDb, reviewerId, RoleType.Reviewer, clock);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                controller.Start(new StartAuditTrailReviewRequest(
                    $"Unauth Start {tag}",
                    clock.GetUtcNow().UtcDateTime.AddHours(-2),
                    clock.GetUtcNow().UtcDateTime.AddHours(-1),
                    AuditReviewScopeKind.All)));
        }

        await using (var assertDb = _fixture.CreateDbContext(clock))
        {
            Assert.False(await assertDb.AuditTrailReviews.AsNoTracking().AnyAsync(r => r.Title == $"Unauth Start {tag}"));
        }
    }

    [PostgresFact]
    public async Task Qa_full_flow_over_http()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var t0 = new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(t0);
        var entityName = $"Sample_{tag}";
        var recordId = $"REC-{tag}";
        int qaUserId;
        int entryId;

        await using (var arrangeDb = _fixture.CreateDbContext(clock))
        {
            qaUserId = await CreateUserWithPermissionsAsync(
                arrangeDb,
                RoleType.Qa,
                "qa",
                $"Nadia QA {tag}",
                PermissionConstants.AuditView,
                PermissionConstants.AuditReview);
            entryId = await SeedAuditEntryAsync(arrangeDb, qaUserId, t0.UtcDateTime.AddMinutes(-30), entityName, recordId);
        }

        int reviewId;
        uint version;
        await using (var actDb = _fixture.CreateDbContext(clock))
        {
            var controller = CreateController(actDb, qaUserId, RoleType.Qa, clock);

            var started = UnwrapOk<AuditTrailReviewDto>(await controller.Start(
                new StartAuditTrailReviewRequest(
                    $"Flow Review {tag}",
                    t0.UtcDateTime.AddHours(-1),
                    t0.UtcDateTime,
                    AuditReviewScopeKind.Record,
                    entityName,
                    recordId)));

            reviewId = started.Id;
            version = started.Version;
            Assert.Equal(1, started.EntryCount);

            var entries = UnwrapOk<PagedResult<AuditLogDto>>(await controller.GetEntries(reviewId, 1, 25));
            Assert.Single(entries.Items);
            Assert.Equal(entryId, entries.Items[0].Id);

            var finding = UnwrapOk<AuditTrailReviewFindingDto>(await controller.AddFinding(
                reviewId,
                new AddAuditTrailReviewFindingRequest(entryId, $"Finding note {tag}")));
            Assert.Equal(AuditFindingStatus.Open, finding.Status);

            var closed = UnwrapOk<AuditTrailReviewFindingDto>(await controller.CloseFinding(
                finding.Id,
                new CloseAuditTrailReviewFindingRequest($"Verified OK {tag}")));
            Assert.Equal(AuditFindingStatus.Closed, closed.Status);

            var signed = UnwrapOk<AuditTrailReviewDto>(await controller.Sign(
                reviewId,
                new SignAuditTrailReviewRequest(TestPassword, $"Review complete {tag}"),
                ifMatch: $"\"{version}\""));

            Assert.Equal(AuditTrailReviewStatus.Signed, signed.Status);
            Assert.Equal(AuditTrailReviewOutcome.FindingsRecorded, signed.Outcome);

            var fetched = UnwrapOk<AuditTrailReviewDto>(await controller.Get(reviewId));
            Assert.Equal($"Nadia QA {tag}", fetched.SignerFullName);
            Assert.Equal(t0.UtcDateTime, fetched.SignedAtUtc);
            Assert.Equal("Audit trail review", fetched.SignatureMeaningText);
        }

        await using (var assertDb = _fixture.CreateDbContext(clock))
        {
            var persisted = await assertDb.AuditTrailReviews
                .AsNoTracking()
                .SingleAsync(r => r.Id == reviewId);
            Assert.Equal(AuditTrailReviewStatus.Signed, persisted.Status);
            Assert.Equal(AuditTrailReviewOutcome.FindingsRecorded, persisted.Outcome);
            Assert.Equal(t0.UtcDateTime, persisted.SignedAtUtc);
            Assert.NotNull(persisted.ElectronicSignatureId);

            var sig = await assertDb.ElectronicSignatures
                .AsNoTracking()
                .SingleAsync(s => s.Id == persisted.ElectronicSignatureId);
            Assert.Equal($"Nadia QA {tag}", sig.UserFullNameSnapshot);
            Assert.Equal(SignatureMeaning.AuditTrailReviewed, sig.MeaningOfSignature);
            Assert.Equal("AuditTrailReview", sig.EntityType);
            Assert.Equal(reviewId, sig.EntityId);

            Assert.True(await assertDb.AuditLogs.AsNoTracking()
                .AnyAsync(a => a.EntityName == nameof(AuditTrailReview) && a.EntityId == reviewId.ToString()));
        }
    }

    [PostgresFact]
    public async Task Sign_without_if_match_refused()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var t0 = new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(t0);
        int qaUserId;
        int reviewId;

        await using (var arrangeDb = _fixture.CreateDbContext(clock))
        {
            qaUserId = await CreateUserWithPermissionsAsync(
                arrangeDb,
                RoleType.Qa,
                "qa",
                $"QA {tag}",
                PermissionConstants.AuditView,
                PermissionConstants.AuditReview);
        }

        await using (var startDb = _fixture.CreateDbContext(clock))
        {
            var controller = CreateController(startDb, qaUserId, RoleType.Qa, clock);
            var started = UnwrapOk<AuditTrailReviewDto>(await controller.Start(
                new StartAuditTrailReviewRequest(
                    $"No IfMatch {tag}",
                    t0.UtcDateTime.AddHours(-1),
                    t0.UtcDateTime,
                    AuditReviewScopeKind.All)));
            reviewId = started.Id;
        }

        await using (var actDb = _fixture.CreateDbContext(clock))
        {
            var controller = CreateController(actDb, qaUserId, RoleType.Qa, clock);
            var missingResult = await controller.Sign(
                reviewId,
                new SignAuditTrailReviewRequest(TestPassword, "Conclusion without version"),
                ifMatch: null);

            var badRequest = Assert.IsType<BadRequestObjectResult>(missingResult);
            var payload = Assert.IsType<ApiResponse<object>>(badRequest.Value);
            Assert.False(payload.Success);

            var invalidResult = await controller.Sign(
                reviewId,
                new SignAuditTrailReviewRequest(TestPassword, "Conclusion with invalid version"),
                ifMatch: "\"not-a-version\"");
            Assert.IsType<BadRequestObjectResult>(invalidResult);
        }

        await using (var assertDb = _fixture.CreateDbContext(clock))
        {
            var persisted = await assertDb.AuditTrailReviews.AsNoTracking().SingleAsync(r => r.Id == reviewId);
            Assert.Equal(AuditTrailReviewStatus.InProgress, persisted.Status);
            Assert.Null(persisted.ElectronicSignatureId);
            Assert.False(await assertDb.ElectronicSignatures.AsNoTracking()
                .AnyAsync(s => s.EntityType == "AuditTrailReview" && s.EntityId == reviewId));
        }
    }

    [PostgresFact]
    public async Task User_with_audit_view_only_can_read_but_not_act()
    {
        var viewActions = new[]
        {
            nameof(AuditTrailReviewsController.List),
            nameof(AuditTrailReviewsController.Get),
            nameof(AuditTrailReviewsController.GetEntries),
            nameof(AuditTrailReviewsController.ExportPdf)
        };
        var reviewActions = new[]
        {
            nameof(AuditTrailReviewsController.Start),
            nameof(AuditTrailReviewsController.AddFinding),
            nameof(AuditTrailReviewsController.Sign),
            nameof(AuditTrailReviewsController.CloseFinding)
        };

        foreach (var action in viewActions)
        {
            var allowed = AuthorizationMatrixTests.AllowedRoles<AuditTrailReviewsController>(action);
            Assert.NotNull(allowed);
            Assert.Equal(
                new[] { RoleType.SystemAdministrator, RoleType.SectionHead, RoleType.Qa }.OrderBy(r => r).ToArray(),
                allowed!.OrderBy(r => r).ToArray());
        }

        foreach (var action in reviewActions)
        {
            var allowed = AuthorizationMatrixTests.AllowedRoles<AuditTrailReviewsController>(action);
            Assert.NotNull(allowed);
            Assert.Equal(new[] { RoleType.Qa }, allowed!.OrderBy(r => r).ToArray());
        }

        var tag = Guid.NewGuid().ToString("N")[..8];
        var t0 = new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(t0);
        var entityName = $"Entity_{tag}";
        int qaUserId;
        int viewOnlyUserId;
        int entryId;
        int reviewId;
        uint reviewVersion;
        int findingId;

        await using (var arrangeDb = _fixture.CreateDbContext(clock))
        {
            qaUserId = await CreateUserWithPermissionsAsync(
                arrangeDb,
                RoleType.Qa,
                "qa",
                $"QA {tag}",
                PermissionConstants.AuditView,
                PermissionConstants.AuditReview);
            viewOnlyUserId = await CreateUserWithPermissionsAsync(
                arrangeDb,
                RoleType.SectionHead,
                "shview",
                $"SectionHead ViewOnly {tag}",
                PermissionConstants.AuditView);
            entryId = await SeedAuditEntryAsync(arrangeDb, qaUserId, t0.UtcDateTime.AddMinutes(-20), entityName, tag);
        }

        await using (var seedReviewDb = _fixture.CreateDbContext(clock))
        {
            var qaController = CreateController(seedReviewDb, qaUserId, RoleType.Qa, clock);
            var review = UnwrapOk<AuditTrailReviewDto>(await qaController.Start(
                new StartAuditTrailReviewRequest(
                    $"ReadOnly Check {tag}",
                    t0.UtcDateTime.AddHours(-1),
                    t0.UtcDateTime,
                    AuditReviewScopeKind.EntityName,
                    entityName)));
            reviewId = review.Id;
            reviewVersion = review.Version;

            var finding = UnwrapOk<AuditTrailReviewFindingDto>(await qaController.AddFinding(
                reviewId,
                new AddAuditTrailReviewFindingRequest(entryId, $"QA finding {tag}")));
            findingId = finding.Id;
        }

        await using (var actDb = _fixture.CreateDbContext(clock))
        {
            var viewController = CreateController(actDb, viewOnlyUserId, RoleType.SectionHead, clock);

            var list = UnwrapOk<IReadOnlyList<AuditTrailReviewListItemDto>>(await viewController.List(1, 25));
            Assert.Contains(list, r => r.Id == reviewId);

            var got = UnwrapOk<AuditTrailReviewDto>(await viewController.Get(reviewId));
            Assert.Equal(reviewId, got.Id);

            var entries = UnwrapOk<PagedResult<AuditLogDto>>(await viewController.GetEntries(reviewId, 1, 25));
            Assert.Single(entries.Items);

            var pdfResult = Assert.IsType<FileContentResult>(await viewController.ExportPdf(reviewId));
            Assert.Equal("application/pdf", pdfResult.ContentType);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => viewController.Start(
                new StartAuditTrailReviewRequest(
                    $"Forbidden Start {tag}",
                    t0.UtcDateTime.AddHours(-1),
                    t0.UtcDateTime,
                    AuditReviewScopeKind.All)));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => viewController.AddFinding(
                reviewId,
                new AddAuditTrailReviewFindingRequest(entryId, "Forbidden finding")));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => viewController.CloseFinding(
                findingId,
                new CloseAuditTrailReviewFindingRequest("Forbidden close")));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => viewController.Sign(
                reviewId,
                new SignAuditTrailReviewRequest(TestPassword, "Forbidden sign"),
                ifMatch: reviewVersion.ToString()));
        }

        await using (var assertDb = _fixture.CreateDbContext(clock))
        {
            Assert.False(await assertDb.AuditTrailReviews.AsNoTracking().AnyAsync(r => r.Title == $"Forbidden Start {tag}"));
            var persistedReview = await assertDb.AuditTrailReviews.AsNoTracking().SingleAsync(r => r.Id == reviewId);
            Assert.Equal(AuditTrailReviewStatus.InProgress, persistedReview.Status);
            var persistedFinding = await assertDb.AuditTrailReviewFindings.AsNoTracking().SingleAsync(f => f.Id == findingId);
            Assert.Equal(AuditFindingStatus.Open, persistedFinding.Status);
        }
    }

    [PostgresFact]
    public async Task Pdf_contains_signer_name_utc_time_and_meaning()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var t0 = new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(t0);
        var entityName = $"PdfSample_{tag}";
        var recordId = $"PDF-REC-{tag}";
        var hiddenPayload = $"RAW_PAYLOAD_MUST_NOT_PRINT_{tag}";
        int qaUserId;
        int entryId;
        AuditTrailReviewDto review;

        await using (var arrangeDb = _fixture.CreateDbContext(clock))
        {
            qaUserId = await CreateUserWithPermissionsAsync(
                arrangeDb,
                RoleType.Qa,
                "qa",
                $"Clara Quality {tag}",
                PermissionConstants.AuditView,
                PermissionConstants.AuditReview);
            entryId = await SeedAuditEntryAsync(
                arrangeDb,
                qaUserId,
                new DateTime(2026, 6, 9, 8, 30, 0, DateTimeKind.Utc),
                entityName,
                recordId,
                JsonSerializer.Serialize(new { Secret = hiddenPayload }));
        }

        await using (var actDb = _fixture.CreateDbContext(clock))
        {
            var controller = CreateController(actDb, qaUserId, RoleType.Qa, clock);
            var started = UnwrapOk<AuditTrailReviewDto>(await controller.Start(
                new StartAuditTrailReviewRequest(
                    $"Monthly Audit Review {tag}",
                    new DateTime(2026, 6, 9, 8, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 6, 9, 9, 30, 0, DateTimeKind.Utc),
                    AuditReviewScopeKind.Record,
                    entityName,
                    recordId)));

            var finding = UnwrapOk<AuditTrailReviewFindingDto>(await controller.AddFinding(
                started.Id,
                new AddAuditTrailReviewFindingRequest(entryId, $"Late entry note {tag}")));

            await controller.CloseFinding(
                finding.Id,
                new CloseAuditTrailReviewFindingRequest($"CAPA verified {tag}"));

            // Create a newer audit entry after start so NewerEntriesCount == 1
            await SeedAuditEntryAsync(
                actDb,
                qaUserId,
                new DateTime(2026, 6, 9, 8, 45, 0, DateTimeKind.Utc),
                entityName,
                recordId,
                JsonSerializer.Serialize(new { Secret = $"NEWER_{hiddenPayload}" }));

            review = UnwrapOk<AuditTrailReviewDto>(await controller.Sign(
                started.Id,
                new SignAuditTrailReviewRequest(TestPassword, $"All entries verified {tag}"),
                ifMatch: started.Version.ToString()));

            var pdfResult = Assert.IsType<FileContentResult>(await controller.ExportPdf(review.Id));
            Assert.Equal("application/pdf", pdfResult.ContentType);

            var text = ExtractPdfText(pdfResult.FileContents);

            Assert.Contains($"Clara Quality {tag}", text);
            Assert.Contains("2026-06-09 10:00 UTC", text);
            Assert.Contains("Audit trail review", text);
            Assert.Contains($"Monthly Audit Review {tag}", text);
            Assert.Contains(entityName, text);
            Assert.Contains(recordId, text);
            Assert.Contains("2026-06-09 08:00 UTC", text);
            Assert.Contains("2026-06-09 09:30 UTC", text);
            Assert.Contains(review.CutoffAuditLogId.ToString(), text);
            Assert.Contains($"Late entry note {tag}", text);
            Assert.Contains($"CAPA verified {tag}", text);
            Assert.Contains($"All entries verified {tag}", text);
            Assert.Contains("Findings recorded", text);
            Assert.Contains("1 newer entry", text);
            Assert.DoesNotContain(hiddenPayload, text);
        }
    }

    [PostgresFact]
    public async Task Unsigned_review_pdf_says_not_signed()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var t0 = new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(t0);
        int qaUserId;
        int reviewId;

        await using (var arrangeDb = _fixture.CreateDbContext(clock))
        {
            qaUserId = await CreateUserWithPermissionsAsync(
                arrangeDb,
                RoleType.Qa,
                "qa",
                $"Unsigned QA {tag}",
                PermissionConstants.AuditView,
                PermissionConstants.AuditReview);
        }

        await using (var actDb = _fixture.CreateDbContext(clock))
        {
            var controller = CreateController(actDb, qaUserId, RoleType.Qa, clock);
            var started = UnwrapOk<AuditTrailReviewDto>(await controller.Start(
                new StartAuditTrailReviewRequest(
                    $"Unsigned Review {tag}",
                    t0.UtcDateTime.AddHours(-2),
                    t0.UtcDateTime.AddHours(-1),
                    AuditReviewScopeKind.All)));
            reviewId = started.Id;

            var pdfResult = Assert.IsType<FileContentResult>(await controller.ExportPdf(reviewId));
            Assert.Equal("application/pdf", pdfResult.ContentType);

            var text = ExtractPdfText(pdfResult.FileContents);
            Assert.Contains("Not signed - review in progress", text);
            Assert.Contains($"Unsigned Review {tag}", text);
        }
    }

    [PostgresFact]
    public async Task Pdf_export_is_logged()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var t0 = new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(t0);
        var entityName = $"ExportEntity_{tag}";
        int qaUserId;
        int reviewId;
        int entryCount;

        await using (var arrangeDb = _fixture.CreateDbContext(clock))
        {
            qaUserId = await CreateUserWithPermissionsAsync(
                arrangeDb,
                RoleType.Qa,
                "qa",
                $"Exporter QA {tag}",
                PermissionConstants.AuditView,
                PermissionConstants.AuditReview);
            await SeedAuditEntryAsync(arrangeDb, qaUserId, t0.UtcDateTime.AddMinutes(-15), entityName, tag);
        }

        await using (var actDb = _fixture.CreateDbContext(clock))
        {
            var controller = CreateController(actDb, qaUserId, RoleType.Qa, clock);
            var started = UnwrapOk<AuditTrailReviewDto>(await controller.Start(
                new StartAuditTrailReviewRequest(
                    $"Export Logged {tag}",
                    t0.UtcDateTime.AddHours(-1),
                    t0.UtcDateTime,
                    AuditReviewScopeKind.EntityName,
                    entityName)));
            reviewId = started.Id;
            entryCount = started.EntryCount;

            var pdfResult = Assert.IsType<FileContentResult>(await controller.ExportPdf(reviewId));
            Assert.Equal("application/pdf", pdfResult.ContentType);
            Assert.NotEmpty(pdfResult.FileContents);
        }

        await using (var assertDb = _fixture.CreateDbContext(clock))
        {
            var logs = await assertDb.DataExportLogs
                .AsNoTracking()
                .Where(l => l.UserId == qaUserId && l.ExportType == "AuditTrailReviewPdf")
                .ToListAsync();

            var matching = Assert.Single(logs, l => JsonDocument.Parse(l.FilterJson).RootElement.GetProperty("reviewId").GetInt32() == reviewId);
            Assert.Equal(qaUserId, matching.UserId);
            Assert.Equal($"Exporter QA {tag}", matching.UserName);
            Assert.Equal("AuditTrailReviewPdf", matching.ExportType);
            Assert.Equal(entryCount, matching.RowCount);
            Assert.Equal(t0.UtcDateTime, matching.ExportedAt);
        }
    }
}
