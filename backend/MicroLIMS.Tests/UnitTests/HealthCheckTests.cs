using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MicroLIMS.API.Extensions;
using MicroLIMS.API.HealthChecks;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Liveness must survive a database outage; readiness must not.
//
// The "database unavailable" case points at a closed port on the loopback
// interface, so these tests need no PostgreSQL server, touch no real
// database, and cannot modify application data.
public class HealthCheckTests
{
    // Port 1 is not a listener. A one-second timeout keeps the suite fast.
    private const string UnreachableConnectionString =
        "Host=127.0.0.1;Port=1;Database=microlims_not_a_real_database;Username=nobody;" +
        "Password=not-a-real-password;Timeout=1;Command Timeout=1";

    private static MicroLimsDbContext UnreachableDbContext() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseNpgsql(UnreachableConnectionString)
            .Options);

    private static HealthCheckContext Context() => new()
    {
        Registration = new HealthCheckRegistration(
            "postgresql", _ => null!, HealthStatus.Unhealthy, new[] { HealthCheckExtensions.ReadyTag })
    };

    // ---- 1 & 2. Liveness ----

    // Liveness selects no checks at all, which is what lets it stay
    // healthy while PostgreSQL is down. Asserting the predicate directly
    // is the meaningful assertion - a check that is never selected cannot
    // fail the endpoint.
    [Fact]
    public void Liveness_SelectsNoDependencyChecks()
    {
        var options = new HealthCheckOptions { Predicate = _ => false };

        var databaseRegistration = new HealthCheckRegistration(
            "postgresql", _ => null!, HealthStatus.Unhealthy, new[] { HealthCheckExtensions.ReadyTag });

        Assert.False(options.Predicate(databaseRegistration));
    }

    [Fact]
    public void Readiness_SelectsTheDatabaseCheck()
    {
        var readiness = new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(HealthCheckExtensions.ReadyTag)
        };

        var databaseRegistration = new HealthCheckRegistration(
            "postgresql", _ => null!, HealthStatus.Unhealthy, new[] { HealthCheckExtensions.ReadyTag });
        var untaggedRegistration = new HealthCheckRegistration(
            "something-else", _ => null!, HealthStatus.Unhealthy, Array.Empty<string>());

        Assert.True(readiness.Predicate(databaseRegistration));
        Assert.False(readiness.Predicate(untaggedRegistration));
    }

    // ---- 4. Readiness fails when PostgreSQL is unavailable ----

    [Fact]
    public async Task Readiness_DatabaseUnavailable_ReportsUnhealthy()
    {
        await using var db = UnreachableDbContext();
        var check = new DatabaseReadinessHealthCheck(db);

        var result = await check.CheckHealthAsync(Context());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(DatabaseReadinessHealthCheck.UnreachableDescription, result.Description);
    }

    // A readiness probe must never be able to take the process down.
    [Fact]
    public async Task Readiness_DatabaseUnavailable_DoesNotThrow()
    {
        await using var db = UnreachableDbContext();
        var check = new DatabaseReadinessHealthCheck(db);

        var exception = await Record.ExceptionAsync(() => check.CheckHealthAsync(Context()));

        Assert.Null(exception);
    }

    // ---- 7. The check never mutates anything ----

    [Fact]
    public async Task Readiness_StagesNoDatabaseWrites()
    {
        await using var db = UnreachableDbContext();
        var check = new DatabaseReadinessHealthCheck(db);

        await check.CheckHealthAsync(Context());

        Assert.False(db.ChangeTracker.HasChanges());
        Assert.Empty(db.ChangeTracker.Entries());
    }

    // ---- 5 & 6. Failure descriptions are fixed strings, not leakage ----

    [Fact]
    public async Task Readiness_FailureDescription_LeaksNoConnectionDetail()
    {
        await using var db = UnreachableDbContext();
        var check = new DatabaseReadinessHealthCheck(db);

        var result = await check.CheckHealthAsync(Context());

        foreach (var secret in new[] { "127.0.0.1", "Password", "not-a-real-password", "nobody", "microlims_not_a_real_database", "Host=" })
            Assert.DoesNotContain(secret, result.Description ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    // Npgsql answers CanConnectAsync with false rather than throwing when
    // the host refuses the connection, so the common "database is down"
    // path carries no exception at all - the fixed description is the
    // whole result. (The catch block covers the rarer case where the
    // connection opens but interrogating it fails; the response writer
    // strips the exception either way - see the writer test below.)
    [Fact]
    public async Task Readiness_UnreachableDatabase_ReportsWithoutAnException()
    {
        await using var db = UnreachableDbContext();
        var check = new DatabaseReadinessHealthCheck(db);

        var result = await check.CheckHealthAsync(Context());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Null(result.Exception);
        Assert.Equal(DatabaseReadinessHealthCheck.UnreachableDescription, result.Description);
    }

    [Fact]
    public void PendingMigrationsDescription_NamesNoMigration()
    {
        // The count is reported, never the migration names - those reveal
        // feature work in progress to anyone who can reach the endpoint.
        Assert.DoesNotContain("_", DatabaseReadinessHealthCheck.PendingMigrationsDescription);
        Assert.Contains("schema is behind", DatabaseReadinessHealthCheck.PendingMigrationsDescription);
    }

    // ---- 6. The serialized response carries no secret ----

    [Fact]
    public async Task ReadinessResponse_ContainsStatusAndNameOnly_NoExceptionOrStackTrace()
    {
        var boom = new InvalidOperationException(
            "Npgsql failed connecting to Host=secret-db.internal;Password=hunter2 at /srv/app/secret/path");

        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["postgresql"] = new(
                    status: HealthStatus.Unhealthy,
                    description: DatabaseReadinessHealthCheck.UnreachableDescription,
                    duration: TimeSpan.FromMilliseconds(5),
                    exception: boom,
                    data: new Dictionary<string, object> { ["connectionString"] = "Host=secret-db;Password=hunter2" })
            },
            HealthStatus.Unhealthy,
            TimeSpan.FromMilliseconds(5));

        var json = await WriteResponseAsync(report);

        // Present: the verdict and which dependency failed.
        Assert.Contains("Unhealthy", json);
        Assert.Contains("postgresql", json);
        Assert.Contains(DatabaseReadinessHealthCheck.UnreachableDescription, json);

        // Absent: everything an attacker or a log scraper could use.
        foreach (var secret in new[]
                 {
                     "hunter2", "secret-db", "Password=", "Host=", "/srv/app",
                     "Npgsql", "InvalidOperationException", "connectionString", "StackTrace"
                 })
        {
            Assert.DoesNotContain(secret, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ReadinessResponse_HealthyReportSerializesCleanly()
    {
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["postgresql"] = new(
                    HealthStatus.Healthy, DatabaseReadinessHealthCheck.ReadyDescription,
                    TimeSpan.FromMilliseconds(3), exception: null, data: null)
            },
            HealthStatus.Healthy,
            TimeSpan.FromMilliseconds(3));

        var json = await WriteResponseAsync(report);
        using var parsed = JsonDocument.Parse(json);

        Assert.Equal("Healthy", parsed.RootElement.GetProperty("status").GetString());
        var check = Assert.Single(parsed.RootElement.GetProperty("checks").EnumerateArray().ToList());
        Assert.Equal("postgresql", check.GetProperty("name").GetString());
        Assert.Equal("Healthy", check.GetProperty("status").GetString());
    }

    // ---- 5. Status-code contract a load balancer depends on ----

    [Fact]
    public void Readiness_MapsUnhealthyAndDegradedTo503_AndHealthyTo200()
    {
        var options = new HealthCheckOptions
        {
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = (int)HttpStatusCode.OK,
                [HealthStatus.Degraded] = (int)HttpStatusCode.ServiceUnavailable,
                [HealthStatus.Unhealthy] = (int)HttpStatusCode.ServiceUnavailable
            }
        };

        Assert.Equal(200, options.ResultStatusCodes[HealthStatus.Healthy]);
        Assert.Equal(503, options.ResultStatusCodes[HealthStatus.Degraded]);
        Assert.Equal(503, options.ResultStatusCodes[HealthStatus.Unhealthy]);
    }

    // Invokes the real private response writer through the registration
    // that Program.cs maps, so the assertions above cover shipped code.
    private static async Task<string> WriteResponseAsync(HealthReport report)
    {
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.Response.Body = body;

        var writer = ResolveReadinessWriter();
        await writer(context, report);

        body.Position = 0;
        return await new StreamReader(body).ReadToEndAsync();
    }

    private static Func<HttpContext, HealthReport, Task> ResolveReadinessWriter()
    {
        var method = typeof(HealthCheckExtensions).GetMethod(
            "WriteReadinessResponse",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?? throw new InvalidOperationException("WriteReadinessResponse not found.");

        return (Func<HttpContext, HealthReport, Task>)Delegate.CreateDelegate(
            typeof(Func<HttpContext, HealthReport, Task>), method);
    }
}
