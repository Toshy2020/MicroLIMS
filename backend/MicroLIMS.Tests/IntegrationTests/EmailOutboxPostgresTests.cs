using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MicroLIMS.Application.Abstractions.Email;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Notifications;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class EmailOutboxPostgresTests
{
    private readonly PostgresTestFixture _fixture;

    public EmailOutboxPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 12, 30)];

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        private readonly Func<string, string, string, Task>? _handler;
        public ConcurrentQueue<(string To, string Subject, string Body)> Sent { get; } = new();

        public RecordingEmailSender(Func<string, string, string, Task>? handler = null) => _handler = handler;

        public async Task SendAsync(string to, string subject, string body)
        {
            Sent.Enqueue((to, subject, body));
            if (_handler is not null)
            {
                await _handler(to, subject, body);
            }
        }
    }

    private sealed class FailingEmailSender : IEmailSender
    {
        private readonly string _errorMessage;
        public int CallCount { get; private set; }

        public FailingEmailSender(string errorMessage = "SMTP unavailable") => _errorMessage = errorMessage;

        public Task SendAsync(string to, string subject, string body)
        {
            CallCount++;
            throw new InvalidOperationException(_errorMessage);
        }
    }

    private static IConfiguration DefaultConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Frontend:Origin"] = "https://lims.example.test",
                ["Lab:TimeZoneId"] = "Africa/Cairo"
            })
            .Build();

    private async Task<User> CreateUserAsync(MicroLimsDbContext db, string prefix, string? email, string? username = null)
    {
        var role = await db.Roles.AsNoTracking().FirstAsync(r => r.Type == RoleType.Analyst);
        var user = new User
        {
            FullName = $"{prefix} {Guid.NewGuid():N}"[..30],
            Username = username ?? UniqueCode($"{prefix}_").ToLowerInvariant(),
            PasswordHash = TestPasswords.Hash("Outbox-Pass-123!"),
            RoleId = role.Id,
            Email = email,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task ClearPendingOutboxAsync(MicroLimsDbContext db)
    {
        await db.EmailOutbox
            .Where(e => e.SentAt == null && e.FailedAt == null)
            .ExecuteDeleteAsync();
    }

    [PostgresFact]
    public async Task Outbox_SentOnce_WhenTwoSendersRun()
    {
        var now = new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);
        var clock = new MutableTimeProvider(now);
        int outboxId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            await ClearPendingOutboxAsync(arrangeDb);
            var user = await CreateUserAsync(arrangeDb, "once", "once@lab.test");
            var row = new EmailOutbox
            {
                UserId = user.Id,
                ToAddress = "once@lab.test",
                Subject = "Item overdue",
                Body = "Test overdue body",
                Kind = EmailOutboxKind.Immediate,
                CreatedAt = now.UtcDateTime,
                NextAttemptAt = now.UtcDateTime,
                Attempts = 0
            };
            arrangeDb.EmailOutbox.Add(row);
            await arrangeDb.SaveChangesAsync();
            outboxId = row.Id;
        }

        var sentCount = 0;
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var sender1 = new RecordingEmailSender(async (_, _, _) =>
        {
            Interlocked.Increment(ref sentCount);
            firstEntered.TrySetResult();
            await releaseFirst.Task;
        });
        var sender2 = new RecordingEmailSender((_, _, _) =>
        {
            Interlocked.Increment(ref sentCount);
            return Task.CompletedTask;
        });

        await using var actDb1 = _fixture.CreateDbContext();
        await using var actDb2 = _fixture.CreateDbContext();

        var task1 = new EmailOutboxSender(actDb1, sender1, clock).SendDueAsync();
        var completed = await Task.WhenAny(firstEntered.Task, task1).WaitAsync(TimeSpan.FromSeconds(15));
        if (completed == task1)
        {
            await task1; // surface any early fault immediately instead of waiting forever
            throw new InvalidOperationException("sender1 completed without invoking IEmailSender.");
        }
        var task2 = new EmailOutboxSender(actDb2, sender2, clock).SendDueAsync();
        await task2;
        releaseFirst.SetResult();
        await task1;

        Assert.Equal(1, sentCount);
        Assert.Single(sender1.Sent);
        Assert.Empty(sender2.Sent);

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var persisted = await assertDb.EmailOutbox.AsNoTracking().FirstAsync(e => e.Id == outboxId);
            Assert.NotNull(persisted.SentAt);
            Assert.Null(persisted.FailedAt);
            Assert.Equal(0, persisted.Attempts);
        }
    }

    [PostgresFact]
    public async Task Outbox_FailedSend_SchedulesNextAttempt_1_5_15_60_240()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero));
        int outboxId;
        var longError = new string('E', 650);

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            await ClearPendingOutboxAsync(arrangeDb);
            var user = await CreateUserAsync(arrangeDb, "backoff", "backoff@lab.test");
            var row = new EmailOutbox
            {
                UserId = user.Id,
                ToAddress = "backoff@lab.test",
                Subject = "Item overdue",
                Body = "Retry test",
                Kind = EmailOutboxKind.Immediate,
                CreatedAt = clock.UtcNow.UtcDateTime,
                NextAttemptAt = clock.UtcNow.UtcDateTime,
                Attempts = 0
            };
            arrangeDb.EmailOutbox.Add(row);
            await arrangeDb.SaveChangesAsync();
            outboxId = row.Id;
        }

        var fakeEmail = new FailingEmailSender(longError);
        int[] expectedMinutes = [1, 5, 15, 60, 240];

        for (var i = 0; i < expectedMinutes.Length; i++)
        {
            var attemptTime = clock.UtcNow.UtcDateTime;
            await using (var actDb = _fixture.CreateDbContext())
            {
                var sent = await new EmailOutboxSender(actDb, fakeEmail, clock).SendDueAsync();
                Assert.Equal(0, sent);
            }

            await using (var checkDb = _fixture.CreateDbContext())
            {
                var persisted = await checkDb.EmailOutbox.AsNoTracking().FirstAsync(e => e.Id == outboxId);
                Assert.Equal(i + 1, persisted.Attempts);
                Assert.Equal(attemptTime.AddMinutes(expectedMinutes[i]), persisted.NextAttemptAt);
                Assert.Null(persisted.SentAt);
                Assert.NotNull(persisted.LastError);
                Assert.Equal(500, persisted.LastError!.Length);

                if (i < 4)
                {
                    Assert.Null(persisted.FailedAt);
                }
                else
                {
                    Assert.NotNull(persisted.FailedAt);
                }
            }

            clock.UtcNow = clock.UtcNow.AddMinutes(expectedMinutes[i]);
        }

        Assert.Equal(5, fakeEmail.CallCount);
    }

    [PostgresFact]
    public async Task Outbox_FifthFailure_MarksFailed_StopsRetrying()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero));
        int outboxId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            await ClearPendingOutboxAsync(arrangeDb);
            var user = await CreateUserAsync(arrangeDb, "fifth", "fifth@lab.test");
            var row = new EmailOutbox
            {
                UserId = user.Id,
                ToAddress = "fifth@lab.test",
                Subject = "Item overdue",
                Body = "Final failure test",
                Kind = EmailOutboxKind.Immediate,
                CreatedAt = clock.UtcNow.UtcDateTime,
                NextAttemptAt = clock.UtcNow.UtcDateTime,
                Attempts = 4
            };
            arrangeDb.EmailOutbox.Add(row);
            await arrangeDb.SaveChangesAsync();
            outboxId = row.Id;
        }

        var fakeEmail = new FailingEmailSender("Permanent mailbox error");

        // 5th attempt fails -> sets FailedAt.
        await using (var actDb1 = _fixture.CreateDbContext())
        {
            await new EmailOutboxSender(actDb1, fakeEmail, clock).SendDueAsync();
        }

        // Advance well past the 240-minute backoff and run again -> must NOT retry.
        clock.UtcNow = clock.UtcNow.AddHours(6);
        await using (var actDb2 = _fixture.CreateDbContext())
        {
            await new EmailOutboxSender(actDb2, fakeEmail, clock).SendDueAsync();
        }

        Assert.Equal(1, fakeEmail.CallCount);

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var persisted = await assertDb.EmailOutbox.AsNoTracking().FirstAsync(e => e.Id == outboxId);
            Assert.Equal(5, persisted.Attempts);
            Assert.NotNull(persisted.FailedAt);
            Assert.Null(persisted.SentAt);
        }
    }

    [PostgresFact]
    public async Task Outbox_UsesUserEmail_NeverMicrolimsLocal()
    {
        var now = new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);
        var clock = new MutableTimeProvider(now);
        var cfg = DefaultConfig();
        var labClock = new LabClock(clock, LabClock.ResolveTimeZone("Africa/Cairo"));
        int userId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            await ClearPendingOutboxAsync(arrangeDb);
            var user = await CreateUserAsync(arrangeDb, "jdoe", "real.analyst@company.example", username: $"jdoe_{Guid.NewGuid():N}"[..16]);
            userId = user.Id;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var publisher = new NotificationPublisher(actDb, clock, cfg, labClock);
            await publisher.PublishAsync(new NotificationRequest(
                Type: "ItemOverdue",
                UserIds: new[] { userId },
                Message: "Sample S-100 is overdue.",
                TargetType: "Sample",
                TargetId: 100,
                SectionId: null,
                DedupeKey: $"test-email-{Guid.NewGuid():N}",
                SampleId: 100,
                DueAtUtc: now.UtcDateTime.AddHours(1)));
            await actDb.SaveChangesAsync();
        }

        var recorder = new RecordingEmailSender();
        await using (var sendDb = _fixture.CreateDbContext())
        {
            await new EmailOutboxSender(sendDb, recorder, clock).SendDueAsync();
        }

        var delivered = Assert.Single(recorder.Sent);
        Assert.Equal("real.analyst@company.example", delivered.To);
        Assert.DoesNotContain("@microlims.local", delivered.To, StringComparison.OrdinalIgnoreCase);

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var outbox = await assertDb.EmailOutbox.AsNoTracking().SingleAsync(e => e.UserId == userId);
            Assert.Equal("real.analyst@company.example", outbox.ToAddress);
            Assert.DoesNotContain("@microlims.local", outbox.ToAddress, StringComparison.OrdinalIgnoreCase);
        }
    }

    [PostgresFact]
    public async Task UserWithoutEmail_NoOutboxRow_InAppStillCreated()
    {
        var now = new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);
        var clock = new MutableTimeProvider(now);
        var cfg = DefaultConfig();
        var labClock = new LabClock(clock, LabClock.ResolveTimeZone("Africa/Cairo"));
        int userId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var user = await CreateUserAsync(arrangeDb, "noemail", email: null);
            userId = user.Id;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var publisher = new NotificationPublisher(actDb, clock, cfg, labClock);
            await publisher.PublishAsync(new NotificationRequest(
                Type: "MediaExpiry",
                UserIds: new[] { userId },
                Message: "TSA Lot L1 expires soon.",
                TargetType: "Media",
                TargetId: 55,
                SectionId: null,
                DedupeKey: $"test-nomail-{Guid.NewGuid():N}",
                DueAtUtc: now.UtcDateTime.AddDays(2)));
            await actDb.SaveChangesAsync();
        }

        await using (var assertDb = _fixture.CreateDbContext())
        {
            Assert.Single(await assertDb.NotificationLogs.AsNoTracking().Where(n => n.UserId == userId).ToListAsync());
            Assert.Empty(await assertDb.EmailOutbox.AsNoTracking().Where(e => e.UserId == userId).ToListAsync());
        }
    }

    [PostgresFact]
    public async Task EmailBody_HasTypeIdentifierDueLinkAndPreferenceFooter_NoResultValue()
    {
        var now = new DateTimeOffset(2026, 11, 15, 8, 30, 0, TimeSpan.Zero);
        var dueUtc = new DateTime(2026, 11, 16, 10, 30, 0, DateTimeKind.Utc); // 12:30 in Africa/Cairo (UTC+2)
        var clock = new MutableTimeProvider(now);
        var cfg = DefaultConfig();
        var labClock = new LabClock(clock, LabClock.ResolveTimeZone("Africa/Cairo"));
        int userId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            await ClearPendingOutboxAsync(arrangeDb);
            var user = await CreateUserAsync(arrangeDb, "composer", "composer@lab.test");
            userId = user.Id;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var publisher = new NotificationPublisher(actDb, clock, cfg, labClock);
            await publisher.PublishAsync(new NotificationRequest(
                Type: "ItemOverdue",
                UserIds: new[] { userId },
                Message: "Sample FP-2026-042 is overdue for review.",
                TargetType: "Sample",
                TargetId: 42,
                SectionId: null,
                DedupeKey: $"test-compose-{Guid.NewGuid():N}",
                SampleId: 42,
                DueAtUtc: dueUtc));
            await actDb.SaveChangesAsync();
        }

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var outbox = await assertDb.EmailOutbox.AsNoTracking().SingleAsync(e => e.UserId == userId);
            Assert.Equal("Item overdue", outbox.Subject);
            Assert.Contains("Item overdue (ItemOverdue)", outbox.Body);
            Assert.Contains("Sample #42", outbox.Body);
            Assert.Contains("Sample FP-2026-042 is overdue for review.", outbox.Body);
            Assert.Contains("Due time (Africa/Cairo): 2026-11-16 12:30", outbox.Body);
            Assert.Contains("https://lims.example.test/samples/42", outbox.Body);
            Assert.Contains("notification preferences", outbox.Body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Password", outbox.Body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Token", outbox.Body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [PostgresFact]
    public async Task SendFailure_DoesNotRollBackNotifications()
    {
        var now = new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);
        var clock = new MutableTimeProvider(now);
        var cfg = DefaultConfig();
        var labClock = new LabClock(clock, LabClock.ResolveTimeZone("Africa/Cairo"));
        int userId;
        var dedupeKey = $"test-sendfail-{Guid.NewGuid():N}";

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            await ClearPendingOutboxAsync(arrangeDb);
            var user = await CreateUserAsync(arrangeDb, "failnb", "failnb@lab.test");
            userId = user.Id;
        }

        await using (var publishDb = _fixture.CreateDbContext())
        {
            var publisher = new NotificationPublisher(publishDb, clock, cfg, labClock);
            await publisher.PublishAsync(new NotificationRequest(
                Type: "MediaExpiry",
                UserIds: new[] { userId },
                Message: "Media lot L99 expires tomorrow.",
                TargetType: "Media",
                TargetId: 99,
                SectionId: null,
                DedupeKey: dedupeKey,
                DueAtUtc: now.UtcDateTime.AddDays(1)));
            await publishDb.SaveChangesAsync();
        }

        var failingSender = new FailingEmailSender("SMTP 503 down");
        await using (var sendDb = _fixture.CreateDbContext())
        {
            var sent = await new EmailOutboxSender(sendDb, failingSender, clock).SendDueAsync();
            Assert.Equal(0, sent);
        }

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var log = await assertDb.NotificationLogs.AsNoTracking()
                .SingleAsync(n => n.UserId == userId && n.DedupeKey == dedupeKey);
            Assert.False(log.EmailSent);

            var outbox = await assertDb.EmailOutbox.AsNoTracking()
                .SingleAsync(e => e.NotificationLogId == log.Id);
            Assert.Equal(1, outbox.Attempts);
            Assert.Equal("SMTP 503 down", outbox.LastError);
            Assert.Null(outbox.SentAt);
        }
    }
}
