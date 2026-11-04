using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Abstractions.Email;
using MicroLIMS.Application.Abstractions.Storage;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Integrations;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Email;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// WP3.5 (critical-alert recipients, test alert) and WP4.1 (Integrations tab).
public class IntegrationsTests
{
    private sealed class FakeEmailSender : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = new();
        public HashSet<string> Failing { get; } = new();

        public Task SendAsync(string to, string subject, string body)
        {
            if (Failing.Contains(to)) throw new InvalidOperationException("mailbox unavailable");
            Sent.Add((to, subject, body));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSmtp : ISmtpStatusProvider
    {
        public bool Configured { get; set; } = true;
        public SmtpConfigurationInfo Describe() =>
            new(Configured, Configured ? "smtp.example.test" : "", 587, "no-reply@example.test", true, true);
    }

    private sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    // Storage that can be told to fail at a step and records deletions.
    private sealed class FakeStorage : IFileStorageService, IStorageDiagnostics
    {
        public Dictionary<string, byte[]> Files { get; } = new();
        public string? FailAt { get; set; }
        public bool CorruptRead { get; set; }

        public Task<string> SaveAsync(string fileName, byte[] content)
        {
            if (FailAt == "write") throw new IOException("disk full");
            Files[fileName] = content;
            return Task.FromResult(fileName);
        }

        public Task<byte[]> ReadAsync(string path)
        {
            if (FailAt == "read") throw new IOException("read failed");
            return Task.FromResult(CorruptRead ? new byte[] { 1 } : Files[path]);
        }

        public StorageDescription Describe() => new("Fake", "memory", 1000, 2000);

        public Task DeleteAsync(string key)
        {
            if (FailAt == "delete") throw new IOException("denied");
            Files.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class NoDeleteStorage : IFileStorageService
    {
        public Task<string> SaveAsync(string fileName, byte[] content) => Task.FromResult(fileName);
        public Task<byte[]> ReadAsync(string path) => Task.FromResult(Array.Empty<byte>());
    }

    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task SetRecipientsAsync(MicroLimsDbContext db, string value)
    {
        db.SystemSettings.Add(new SystemSetting
        {
            Key = SystemSettingKeys.AlertsCriticalRecipients, Value = value, ValueType = SystemSettingValueType.EmailList,
            Category = "Alerts", Description = "x", CurrentVersion = 1
        });
        await db.SaveChangesAsync();
    }

    private static SystemSettingsReader Reader(MicroLimsDbContext db, string? legacy = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            legacy is null ? new Dictionary<string, string?>() : new() { ["ErrorMonitoring:CriticalAlerts:Recipient"] = legacy }).Build();
        return new SystemSettingsReader(db, new SystemSettingsCache(), config);
    }

    private static CriticalAlertOptions Options(string? legacy = null) => new(true, legacy, "Testing");

    private static IntegrationsService Service(
        MicroLimsDbContext db, FakeEmailSender email, FakeStorage? storage = null, ISystemSettingsReader? settings = null,
        FakeSmtp? smtp = null, ManualTime? time = null, EmailDeliveryTracker? tracker = null, IFileStorageService? plainStorage = null)
    {
        var clock = time ?? new ManualTime();
        return new IntegrationsService(
            db, email, smtp ?? new FakeSmtp(), tracker ?? new EmailDeliveryTracker(), plainStorage ?? storage ?? new FakeStorage(),
            new SecurityAuditService(db, new SystemSecurityRequestContext(), clock), Options(),
            new AdminActionThrottle(clock), settings ?? Reader(db), clock);
    }

    // ---- Recipients: validation ----

    [Theory]
    [InlineData("a@example.test, b@example.test", "a@example.test,b@example.test")]
    [InlineData("A@Example.test;a@example.test\nb@example.test", "a@example.test,b@example.test")]
    [InlineData("", "")]
    public void Recipients_AreNormalisedDeduplicatedAndMayBeEmpty(string raw, string expected)
    {
        var definition = SystemSettingDefinitions.Get(SystemSettingKeys.AlertsCriticalRecipients);
        Assert.Equal(expected, definition.Normalize(raw));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a@b")]
    [InlineData("Name <a@example.test>")]
    [InlineData("a@example.test, nope")]
    public void Recipients_RejectInvalidAddresses(string raw)
    {
        var definition = SystemSettingDefinitions.Get(SystemSettingKeys.AlertsCriticalRecipients);
        Assert.Throws<InvalidOperationException>(() => definition.Normalize(raw));
    }

    [Fact]
    public void Recipients_RejectMoreThanTwentyAddresses()
    {
        var definition = SystemSettingDefinitions.Get(SystemSettingKeys.AlertsCriticalRecipients);
        var twenty = string.Join(",", Enumerable.Range(1, 20).Select(i => $"u{i}@example.test"));
        var twentyOne = twenty + ",u21@example.test";
        Assert.Equal(twenty, definition.Normalize(twenty));
        Assert.Throws<InvalidOperationException>(() => definition.Normalize(twentyOne));
    }

    [Fact]
    public async Task Recipients_AreReasonOnlyNotSigned()
    {
        var definition = SystemSettingDefinitions.Get(SystemSettingKeys.AlertsCriticalRecipients);
        Assert.False(definition.IsGxpRelevant);
        await Task.CompletedTask;
    }

    // ---- Recipients: fallback and multi-send ----

    [Fact]
    public async Task Recipients_FallBackToTheLegacyConfigurationKey()
    {
        using var db = NewDb();
        var reader = Reader(db, legacy: "ops@example.test");
        Assert.Equal(new[] { "ops@example.test" }, await CriticalAlertRecipients.ResolveAsync(reader, Options("ignored@example.test")));
    }

    [Fact]
    public async Task Recipients_StoredValueWinsOverLegacy_AndEmptyDisablesThem()
    {
        using var db = NewDb();
        await SetRecipientsAsync(db, "a@example.test,b@example.test");
        var reader = Reader(db, legacy: "ops@example.test");
        Assert.Equal(2, (await CriticalAlertRecipients.ResolveAsync(reader, Options())).Count);

        using var db2 = NewDb();
        await SetRecipientsAsync(db2, "");
        Assert.Empty(await CriticalAlertRecipients.ResolveAsync(Reader(db2, legacy: "ops@example.test"), Options()));
    }

    [Fact]
    public async Task CriticalAlert_IsSentToEveryRecipient_AndOneBadMailboxDoesNotBlockTheRest()
    {
        using var db = NewDb();
        await SetRecipientsAsync(db, "a@example.test,b@example.test,c@example.test");
        var incident = new Incident { Id = Guid.NewGuid(), CorrelationId = "c1", Severity = ErrorSeverity.Critical, Status = IncidentStatus.Open, Summary = "boom" };
        db.Incidents.Add(incident);
        await db.SaveChangesAsync();
        var email = new FakeEmailSender();
        email.Failing.Add("b@example.test");

        var service = new CriticalAlertService(db, email, Options(), NullLogger<CriticalAlertService>.Instance, settings: Reader(db));
        Assert.Equal(1, await service.DispatchPendingAsync());

        Assert.Equal(new[] { "a@example.test", "c@example.test" }, email.Sent.Select(s => s.To).ToArray());
        Assert.NotNull((await db.Incidents.FindAsync(incident.Id))!.AlertedAtUtc);
    }

    [Fact]
    public async Task CriticalAlert_StaysPendingWhenEveryRecipientFails()
    {
        using var db = NewDb();
        await SetRecipientsAsync(db, "a@example.test");
        var incident = new Incident { Id = Guid.NewGuid(), CorrelationId = "c2", Severity = ErrorSeverity.Critical, Status = IncidentStatus.Open, Summary = "boom" };
        db.Incidents.Add(incident);
        await db.SaveChangesAsync();
        var email = new FakeEmailSender();
        email.Failing.Add("a@example.test");

        var service = new CriticalAlertService(db, email, Options(), NullLogger<CriticalAlertService>.Instance, settings: Reader(db));
        Assert.Equal(0, await service.DispatchPendingAsync());
        Assert.Null((await db.Incidents.FindAsync(incident.Id))!.AlertedAtUtc);
    }

    // ---- Test alert ----

    [Fact]
    public async Task TestAlert_SendsLabelledMessageToAllRecipients_RecordsSecurityEvent_AndReportsPerRecipient()
    {
        using var db = NewDb();
        await SetRecipientsAsync(db, "a@example.test,b@example.test");
        var email = new FakeEmailSender();
        email.Failing.Add("b@example.test");

        var result = await Service(db, email).SendTestAlertAsync(7, "  Quarterly alert drill ");

        Assert.Equal(2, result.RecipientCount);
        Assert.Equal(1, result.Sent);
        Assert.Equal("Sent", result.Outcomes.Single(o => o.Recipient == "a@example.test").Outcome);
        Assert.Equal("Failed", result.Outcomes.Single(o => o.Recipient == "b@example.test").Outcome);
        var message = Assert.Single(email.Sent);
        Assert.Contains("TEST ALERT", message.Subject);
        Assert.Contains("THIS IS A TEST", message.Body);

        var evt = await db.SecurityAuditEvents.SingleAsync();
        Assert.Equal(SecurityEventCodes.CriticalAlertTestSent, evt.EventCode);
        Assert.Equal(7, evt.ActorUserId);
        Assert.Equal("Quarterly alert drill", evt.Reason);
        using var meta = JsonDocument.Parse(evt.Metadata!);
        Assert.Equal(2, meta.RootElement.GetProperty("recipientCount").GetInt32());
    }

    [Fact]
    public async Task TestAlert_NeedsAReasonAndRecipients()
    {
        using var db = NewDb();
        var email = new FakeEmailSender();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db, email).SendTestAlertAsync(1, " "));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db, email).SendTestAlertAsync(1, "drill"));
        Assert.Empty(email.Sent);
        Assert.Empty(db.SecurityAuditEvents);
    }

    [Fact]
    public async Task TestAlert_IsRateLimitedToOncePerMinute()
    {
        using var db = NewDb();
        await SetRecipientsAsync(db, "a@example.test");
        var time = new ManualTime();
        var email = new FakeEmailSender();
        var service = Service(db, email, time: time);

        await service.SendTestAlertAsync(1, "first");
        var ex = await Assert.ThrowsAsync<RateLimitedException>(() => service.SendTestAlertAsync(1, "second"));
        Assert.InRange(ex.RetryAfterSeconds, 1, 60);
        Assert.Single(email.Sent);

        time.Now = time.Now.AddSeconds(61);
        await service.SendTestAlertAsync(1, "third");
        Assert.Equal(2, email.Sent.Count);
    }

    [Fact]
    public async Task TestAlert_WithSmtpNotConfigured_ReportsSkippedInsteadOfSent()
    {
        using var db = NewDb();
        await SetRecipientsAsync(db, "a@example.test");
        var email = new FakeEmailSender();

        var result = await Service(db, email, smtp: new FakeSmtp { Configured = false }).SendTestAlertAsync(1, "drill");

        Assert.Equal(0, result.Sent);
        Assert.Equal("Skipped", result.Outcomes.Single().Outcome);
        Assert.Empty(email.Sent);
    }

    // ---- Test email ----

    [Fact]
    public async Task TestEmail_ValidatesAddressAndReason_AndRecordsSecurityEvent()
    {
        using var db = NewDb();
        var email = new FakeEmailSender();
        var service = Service(db, email);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendTestEmailAsync(1, "nope", "check"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendTestEmailAsync(1, "a@example.test", ""));

        var result = await service.SendTestEmailAsync(1, "A@Example.test", "SMTP relay change");
        Assert.Equal(1, result.Sent);
        Assert.Equal("a@example.test", Assert.Single(email.Sent).To);
        Assert.Equal(SecurityEventCodes.SmtpTestEmailSent, (await db.SecurityAuditEvents.SingleAsync()).EventCode);

        await Assert.ThrowsAsync<RateLimitedException>(() => service.SendTestEmailAsync(1, "a@example.test", "again"));
    }

    // ---- SMTP status ----

    [Fact]
    public async Task SmtpStatus_NeverSerialisesThePassword()
    {
        var options = new SmtpOptions { Host = "smtp.example.test", Port = 2525, Username = "mailer", Password = "S3cr3t-Value!", FromAddress = "from@example.test", EnableSsl = true };
        using var db = NewDb();
        var tracker = new EmailDeliveryTracker();
        var service = new IntegrationsService(
            db, new FakeEmailSender(), new SmtpStatusProvider(options), tracker, new FakeStorage(),
            new SecurityAuditService(db, new SystemSecurityRequestContext()), Options(), new AdminActionThrottle(), null);

        var status = service.GetSmtpStatus();
        var json = JsonSerializer.Serialize(status);

        Assert.DoesNotContain("S3cr3t", json);
        Assert.DoesNotContain("mailer", json);
        Assert.Equal("********", status.Password);
        Assert.True(status.AuthenticationConfigured);
        Assert.Equal("smtp.example.test", status.Host);
        Assert.Equal(2525, status.Port);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task TrackingSender_RecordsLastSendResult_WithoutRecipientOrBody()
    {
        var tracker = new EmailDeliveryTracker();
        var inner = new FakeEmailSender();
        var sender = new TrackingEmailSender(inner, tracker, new FakeSmtp());

        await sender.SendAsync("a@example.test", "subject", "body");
        Assert.Equal("Sent", tracker.Last!.Outcome);

        inner.Failing.Add("b@example.test");
        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync("b@example.test", "subject", "body"));
        Assert.Equal("Failed", tracker.Last!.Outcome);
        Assert.DoesNotContain("b@example.test", tracker.Last.Error);
    }

    // ---- Storage probe ----

    [Fact]
    public async Task StorageProbe_Success_WritesReadsAndDeletesTheTempObject()
    {
        using var db = NewDb();
        var storage = new FakeStorage();

        var result = await Service(db, new FakeEmailSender(), storage).RunStorageProbeAsync(3, "Verify B2 credentials");

        Assert.True(result.Succeeded);
        Assert.Equal(new[] { "Write", "Read back", "Delete" }, result.Steps.Select(s => s.Step).ToArray());
        Assert.Empty(storage.Files);
        var evt = await db.SecurityAuditEvents.SingleAsync();
        Assert.Equal(SecurityEventCodes.StorageProbeRun, evt.EventCode);
        Assert.Equal(SecurityEventOutcome.Success, evt.Outcome);
    }

    [Theory]
    [InlineData("write", "Write", 1)]
    [InlineData("read", "Read back", 3)] // the delete still runs so the probe object is not left behind
    [InlineData("delete", "Delete", 3)]
    public async Task StorageProbe_Failure_IsReportedPerStep(string failAt, string failedStep, int expectedSteps)
    {
        using var db = NewDb();
        var storage = new FakeStorage { FailAt = failAt };

        var result = await Service(db, new FakeEmailSender(), storage).RunStorageProbeAsync(3, "check");

        Assert.False(result.Succeeded);
        Assert.Equal(expectedSteps, result.Steps.Count);
        var failed = Assert.Single(result.Steps, st => !st.Succeeded);
        Assert.Equal(failedStep, failed.Step);
        Assert.Contains("IOException", failed.Error);
        Assert.Equal(SecurityEventOutcome.Failure, (await db.SecurityAuditEvents.SingleAsync()).Outcome);
    }

    [Fact]
    public async Task StorageProbe_DetectsCorruptReadBack()
    {
        using var db = NewDb();
        var result = await Service(db, new FakeEmailSender(), new FakeStorage { CorruptRead = true }).RunStorageProbeAsync(3, "check");
        Assert.False(result.Succeeded);
        Assert.Contains("differ", result.Steps.Single(s => s.Step == "Read back").Error);
    }

    [Fact]
    public async Task StorageProbe_WithoutDeleteSupport_WarnsAndNeedsReasonAndIsRateLimited()
    {
        using var db = NewDb();
        var service = Service(db, new FakeEmailSender(), plainStorage: new NoDeleteStorage());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunStorageProbeAsync(1, ""));
        var result = await service.RunStorageProbeAsync(1, "check");
        Assert.NotNull(result.Warning);
        await Assert.ThrowsAsync<RateLimitedException>(() => service.RunStorageProbeAsync(1, "again"));
    }

    [Fact]
    public async Task StorageStatus_ReportsProviderLocationAndFreeSpace_Local()
    {
        var dir = Path.Combine(Path.GetTempPath(), "microlims-probe-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var db = NewDb();
            var local = new MicroLIMS.Infrastructure.Storage.LocalFileStorageService(dir);
            var service = Service(db, new FakeEmailSender(), plainStorage: local);

            var status = service.GetStorageStatus();
            Assert.Equal("Local", status.Provider);
            Assert.Equal(Path.GetFullPath(dir), status.Location);
            Assert.True(status.FreeBytes > 0);

            // The real local provider round-trips and cleans up its probe object.
            var probe = await service.RunStorageProbeAsync(1, "check");
            Assert.True(probe.Succeeded);
            Assert.Empty(Directory.GetFiles(dir, "*", SearchOption.AllDirectories));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    // ---- Instrument agents ----

    [Fact]
    public async Task InstrumentAgents_ListStatusWithoutKeyMaterial()
    {
        using var db = NewDb();
        db.InstrumentAgents.Add(new InstrumentAgent { Code = "AGT-1", Name = "Lab PC", EquipmentId = 1, SectionId = 1, TimeZoneId = "UTC", KeyHash = "HASH-SECRET", KeyPrefix = "abcd1234", IsActive = true, LastSeenAt = DateTime.UtcNow });
        db.InstrumentAgents.Add(new InstrumentAgent { Code = "AGT-2", Name = "Old PC", EquipmentId = 2, SectionId = 1, TimeZoneId = "UTC", KeyHash = "HASH-SECRET-2", KeyPrefix = "zzzz9999", IsActive = false, RevokedAt = DateTime.UtcNow, RevokeReason = "retired" });
        await db.SaveChangesAsync();

        var agents = await Service(db, new FakeEmailSender()).ListInstrumentAgentsAsync();

        Assert.Equal(new[] { "Active", "Revoked" }, agents.Select(a => a.Status).ToArray());
        Assert.DoesNotContain("HASH-SECRET", JsonSerializer.Serialize(agents));
    }
}
