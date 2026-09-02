using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Infrastructure.Notifications;
using MicroLIMS.Infrastructure.Pdf;
using MicroLIMS.Infrastructure.Storage;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Tests;

// Keeps archived files in memory so tests never touch the filesystem.
public class InMemoryFileStorageService : IFileStorageService
{
    public Dictionary<string, byte[]> Files { get; } = new();

    public Task<string> SaveAsync(string fileName, byte[] content)
    {
        Files[fileName] = content;
        return Task.FromResult(fileName);
    }

    public Task<byte[]> ReadAsync(string path) => Task.FromResult(Files[path]);
}

// Tests assert on persisted state, not on delivery.
public class NoOpNotificationService : INotificationService
{
    public Task NotifyAsync(int userId, string message) => Task.CompletedTask;
}

public class NoOpEmailSender : MicroLIMS.Infrastructure.Email.IEmailSender
{
    public Task SendAsync(string to, string subject, string body) => Task.CompletedTask;
}

// For the handful of tests that DO need to assert delivery (e.g. the
// reviewer send-back notifying the analyst).
public class SpyNotificationService : INotificationService
{
    public List<(int UserId, string Message)> Sent { get; } = new();

    public Task NotifyAsync(int userId, string message)
    {
        Sent.Add((userId, message));
        return Task.CompletedTask;
    }
}

// Builds the real service graph for tests. Centralised so adding a
// dependency to a service does not mean editing every test file that
// constructs it.
public static class TestServiceFactory
{
    public static DashboardNotificationService DashboardNotification(MicroLimsDbContext db, INotificationService? notifications = null, MicroLIMS.Infrastructure.Email.IEmailSender? emailSender = null) =>
        new(db, notifications ?? new NoOpNotificationService(), emailSender ?? new NoOpEmailSender());
    public static ReviewGateService ReviewGate(MicroLimsDbContext db) =>
        new(db, new ElectronicSignatureService(db));

    public static SamplePreparationService SamplePreparation(MicroLimsDbContext db) =>
        new(db, new PreparationParameterValidator(db), new ElectronicSignatureService(db));

    public static ItemPreparationConfigurationService ItemPreparationConfiguration(MicroLimsDbContext db) =>
        new(db, new PreparationParameterValidator(db));

    public static ReviewService Review(MicroLimsDbContext db) =>
        new(db, new SegregationOfDutiesGuard(db), new ElectronicSignatureService(db));

    public static RecordArchiveService Archive(MicroLimsDbContext db, IFileStorageService? storage = null) =>
        new(db, new PdfGenerator(), storage ?? new InMemoryFileStorageService(), NullLogger<RecordArchiveService>.Instance);

    public static SampleSummaryService SampleSummary(MicroLimsDbContext db) =>
        new(db, new PdfGenerator(), new MicroLIMS.Infrastructure.Word.WordGenerator(), ReviewGate(db));

    public static MediaSummaryService MediaSummary(MicroLimsDbContext db) =>
        new(db, new PdfGenerator(), new MicroLIMS.Infrastructure.Word.WordGenerator(), ReviewGate(db));

    public static CryovialSummaryService CryovialSummary(MicroLimsDbContext db) =>
        new(db, new PdfGenerator(), new MicroLIMS.Infrastructure.Word.WordGenerator(), ReviewGate(db));

    public static SampleReviewService SampleReview(MicroLimsDbContext db) =>
        new(db, new SegregationOfDutiesGuard(db), ReviewGate(db));

    public static ResultProjectionService ResultProjection(MicroLimsDbContext db) =>
        new(db, NullLogger<ResultProjectionService>.Instance);

    public static TestWorkflowEngine TestWorkflow(MicroLimsDbContext db, INotificationService? notifications = null) =>
        new(db, SampleReview(db), ResultProjection(db), IncubatorEligibility(db), AppearanceSnapshot(db),
            new SegregationOfDutiesGuard(db), ReviewGate(db), notifications ?? new NoOpNotificationService());

    public static SampleApprovalService SampleApproval(MicroLimsDbContext db, IFileStorageService? storage = null) =>
        new(db, ReviewGate(db), SampleSummary(db), Archive(db, storage), ResultProjection(db), new ReferenceNumberGenerator(db));

    public static MediaReleaseService MediaRelease(MicroLimsDbContext db, IFileStorageService? storage = null) =>
        new(db, new SegregationOfDutiesGuard(db), ReviewGate(db), MediaSummary(db), Archive(db, storage));

    public static CryovialService Cryovial(MicroLimsDbContext db, IFileStorageService? storage = null) =>
        new(db, new MaterialService(db), new SegregationOfDutiesGuard(db), ReviewGate(db),
            CryovialSummary(db), Archive(db, storage));

    public static MediaPreparationService MediaPreparation(MicroLimsDbContext db) =>
        new(db, new MaterialService(db), ReviewGate(db));

    public static IncubatorEligibilityService IncubatorEligibility(MicroLimsDbContext db) => new(db);

    public static MediaAppearanceSnapshotService AppearanceSnapshot(MicroLimsDbContext db) =>
        new(db, NullLogger<MediaAppearanceSnapshotService>.Instance);

    public static KpiService Kpi(MicroLimsDbContext db) => new(db);

    public static DashboardService Dashboard(MicroLimsDbContext db) => new(db, Kpi(db));

    public static MyTasksService MyTasks(MicroLimsDbContext db) => new(db);

    public static DiscussionService Discussion(MicroLimsDbContext db, IFileStorageService? storage = null, INotificationService? notifications = null) =>
        new(db, storage ?? new InMemoryFileStorageService(), notifications ?? new NoOpNotificationService(), NullLogger<DiscussionService>.Instance);

    public static MessageService Message(MicroLimsDbContext db, INotificationService? notifications = null) =>
        new(db, notifications ?? new NoOpNotificationService(), NullLogger<MessageService>.Instance);
}
