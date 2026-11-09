using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// CLAUDE.md Rule 3.3.2 / 5.6: the audit timestamp comes from the injected
// TimeProvider, never the static system clock. A clock fixed far from "now"
// proves which one stamped the row, and the timestamptz round trip proves the
// value is stored as UTC.
[Collection("PostgresDatabaseCollection")]
public class AuditClockPostgresTests
{
    private static readonly DateTimeOffset FixedNow = new(2031, 3, 4, 5, 6, 7, TimeSpan.Zero);
    private readonly PostgresTestFixture _fixture;

    public AuditClockPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    [PostgresFact]
    public async Task AuditRows_AreStampedByTheInjectedClock_InUtc()
    {
        var name = "Clock organism " + Guid.NewGuid().ToString("N")[..8];
        int organismId;

        await using (var actDb = _fixture.CreateDbContext(new FixedTimeProvider(FixedNow)))
        {
            actDb.CurrentUserId = _fixture.SeededUserId;
            var organism = new Organism { ScientificName = name };
            actDb.Organisms.Add(organism);
            await actDb.SaveChangesAsync();
            organismId = organism.Id;
        }

        await using var assertDb = _fixture.CreateDbContext();
        var audit = await assertDb.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.EntityName == nameof(Organism) && a.EntityId == organismId.ToString() && a.Action == "Create");

        Assert.Equal(FixedNow.UtcDateTime, audit.Timestamp);
        Assert.Equal(DateTimeKind.Utc, audit.Timestamp.Kind);
        Assert.Equal(_fixture.SeededUserId, audit.UserId);
        Assert.Contains(name, audit.NewValue);
    }
}
