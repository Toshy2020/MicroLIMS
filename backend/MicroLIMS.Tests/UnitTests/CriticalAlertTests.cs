using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Email;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Critical incident alerting: one email per Incident, never per ErrorLog,
// and never at the cost of the laboratory operation that hit the error.
//
// No real email is sent - IEmailSender is faked throughout.
public class CriticalAlertTests
{
    private const string Recipient = "operations@example.test";

    // Records what would have been sent, and can be told to fail.
    private sealed class FakeEmailSender : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = new();
        public Exception? ThrowOnSend { get; set; }

        public Task SendAsync(string to, string subject, string body)
        {
            if (ThrowOnSend is not null) throw ThrowOnSend;
            Sent.Add((to, subject, body));
            return Task.CompletedTask;
        }
    }

    private static MicroLimsDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static CriticalAlertService CreateService(
        MicroLimsDbContext db, FakeEmailSender email, bool enabled = true, string? recipient = Recipient) =>
        new(db, email,
            new CriticalAlertOptions(enabled, recipient, "Testing"),
            NullLogger<CriticalAlertService>.Instance);

    private static Incident SeedIncident(
        MicroLimsDbContext db,
        ErrorSeverity severity = ErrorSeverity.Critical,
        IncidentStatus status = IncidentStatus.Open,
        DateTime? alertedAt = null,
        int errorLogCount = 1)
    {
        var incident = new Incident
        {
            Id = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid().ToString("N"),
            Severity = severity,
            Status = status,
            AlertedAtUtc = alertedAt,
            Summary = "NullReferenceException - POST /api/results"
        };
        db.Incidents.Add(incident);

        for (var i = 0; i < errorLogCount; i++)
        {
            db.ErrorLogs.Add(new ErrorLog
            {
                Id = Guid.NewGuid(),
                IncidentId = incident.Id,
                Source = ErrorSource.Backend,
                Severity = severity,
                ExceptionType = "NullReferenceException",
                Message = "Object reference not set",
                CorrelationId = incident.CorrelationId
            });
        }

        db.SaveChanges();
        return incident;
    }

    // ---- 5. A Critical incident produces one alert ----

    [Fact]
    public async Task CriticalIncident_SendsExactlyOneAlert()
    {
        using var db = CreateDbContext();
        var incident = SeedIncident(db);
        var email = new FakeEmailSender();

        var sent = await CreateService(db, email).DispatchPendingAsync();

        Assert.Equal(1, sent);
        var message = Assert.Single(email.Sent);
        Assert.Equal(Recipient, message.To);
        Assert.Contains("Critical incident", message.Subject);
        Assert.Contains("Testing", message.Subject);
        Assert.NotNull((await db.Incidents.FindAsync(incident.Id))!.AlertedAtUtc);
    }

    // ---- 6. Many ErrorLogs in one Incident is still one alert ----

    [Fact]
    public async Task ManyErrorLogsInOneIncident_StillSendOnlyOneAlert()
    {
        using var db = CreateDbContext();
        SeedIncident(db, errorLogCount: 50);
        var email = new FakeEmailSender();

        await CreateService(db, email).DispatchPendingAsync();

        var message = Assert.Single(email.Sent);
        Assert.Contains("Occurrences:    50", message.Body);
    }

    // ---- 13. A restart must not resend ----

    [Fact]
    public async Task SecondDispatch_DoesNotResend_EvenFromAFreshServiceInstance()
    {
        using var db = CreateDbContext();
        SeedIncident(db);
        var email = new FakeEmailSender();

        await CreateService(db, email).DispatchPendingAsync();
        // A new service instance stands in for a restarted process: the
        // debounce lives in the database, not in memory.
        await CreateService(db, email).DispatchPendingAsync();

        Assert.Single(email.Sent);
    }

    [Fact]
    public async Task AlreadyAlertedIncident_IsNeverAlertedAgain()
    {
        using var db = CreateDbContext();
        SeedIncident(db, alertedAt: DateTime.UtcNow.AddHours(-3));
        var email = new FakeEmailSender();

        Assert.Equal(0, await CreateService(db, email).DispatchPendingAsync());
        Assert.Empty(email.Sent);
    }

    // ---- 7. Lower severities do not alert ----

    [Theory]
    [InlineData(ErrorSeverity.Info)]
    [InlineData(ErrorSeverity.Warning)]
    [InlineData(ErrorSeverity.Error)]
    public async Task NonCriticalIncident_DoesNotAlert(ErrorSeverity severity)
    {
        using var db = CreateDbContext();
        SeedIncident(db, severity);
        var email = new FakeEmailSender();

        Assert.Equal(0, await CreateService(db, email).DispatchPendingAsync());
        Assert.Empty(email.Sent);
    }

    // ---- 12. A resolved incident does not alert ----

    [Fact]
    public async Task ResolvedCriticalIncident_DoesNotAlert()
    {
        using var db = CreateDbContext();
        SeedIncident(db, status: IncidentStatus.Resolved);
        var email = new FakeEmailSender();

        Assert.Equal(0, await CreateService(db, email).DispatchPendingAsync());
        Assert.Empty(email.Sent);
    }

    // ---- 8 & 9. Disabled, or no recipient, sends nothing ----

    [Fact]
    public async Task AlertingDisabled_SendsNothing()
    {
        using var db = CreateDbContext();
        var incident = SeedIncident(db);
        var email = new FakeEmailSender();

        Assert.Equal(0, await CreateService(db, email, enabled: false).DispatchPendingAsync());
        Assert.Empty(email.Sent);
        // Left unalerted, so enabling it later still notifies.
        Assert.Null((await db.Incidents.FindAsync(incident.Id))!.AlertedAtUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task NoRecipientConfigured_SendsNothingAndDoesNotThrow(string? recipient)
    {
        using var db = CreateDbContext();
        SeedIncident(db);
        var email = new FakeEmailSender();

        Assert.Equal(0, await CreateService(db, email, recipient: recipient).DispatchPendingAsync());
        Assert.Empty(email.Sent);
    }

    // ---- 10. An SMTP failure must not break anything ----

    [Fact]
    public async Task SmtpFailure_IsContained_AndTheIncidentIsRetriedLater()
    {
        using var db = CreateDbContext();
        var incident = SeedIncident(db);
        var email = new FakeEmailSender { ThrowOnSend = new InvalidOperationException("SMTP host unreachable") };

        // Never throws - a failed alert must not become a second incident.
        var sent = await CreateService(db, email).DispatchPendingAsync();

        Assert.Equal(0, sent);
        // Still unalerted, so the next pass tries again.
        Assert.Null((await db.Incidents.FindAsync(incident.Id))!.AlertedAtUtc);

        email.ThrowOnSend = null;
        Assert.Equal(1, await CreateService(db, email).DispatchPendingAsync());
        Assert.Single(email.Sent);
    }

    // The whole dispatch is defensive: even a broken store cannot throw
    // out of it, which is what keeps a logging/alerting fault from
    // cascading into the request that produced the error.
    [Fact]
    public async Task StoreFailure_IsContained()
    {
        var db = CreateDbContext();
        SeedIncident(db);
        var service = CreateService(db, new FakeEmailSender());
        await db.DisposeAsync();

        var exception = await Record.ExceptionAsync(() => service.DispatchPendingAsync());

        Assert.Null(exception);
    }

    // ---- 11. The alert carries no secrets or error content ----

    [Fact]
    public async Task AlertBody_ContainsOperatorContextButNoSensitiveContent()
    {
        using var db = CreateDbContext();
        var incident = SeedIncident(db);
        var email = new FakeEmailSender();

        await CreateService(db, email).DispatchPendingAsync();
        var body = Assert.Single(email.Sent).Body;

        // Enough to triage.
        Assert.Contains(incident.Id.ToString(), body);
        Assert.Contains(incident.CorrelationId, body);
        Assert.Contains("Critical", body);
        Assert.Contains("Error Monitoring page", body);
        Assert.Contains("not a GxP record", body);

        // Nothing that could carry a credential or a laboratory result.
        foreach (var forbidden in new[]
                 {
                     "Password", "Bearer ", "Authorization", "ConnectionStrings",
                     "Host=", "Jwt", "StackTrace", "   at ", "Object reference not set"
                 })
        {
            Assert.DoesNotContain(forbidden, body, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---- 22. GxP boundary ----

    [Fact]
    public async Task Alerting_CreatesNoGxpAuditEntries()
    {
        using var db = CreateDbContext();
        SeedIncident(db);
        var email = new FakeEmailSender();

        await CreateService(db, email).DispatchPendingAsync();

        // Incident and ErrorLog are on the CaptureAuditEntries deny-list,
        // and stamping AlertedAtUtc must not change that.
        Assert.Empty(await db.AuditLogs
            .Where(a => a.EntityName == nameof(Incident) || a.EntityName == nameof(ErrorLog))
            .ToListAsync());
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }
}
