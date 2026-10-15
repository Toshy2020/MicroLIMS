using MicroLIMS.Application.Services.Kpi;
using Xunit;
using static MicroLIMS.Tests.UnitTests.Kpi.KpiTestData;

namespace MicroLIMS.Tests.UnitTests.Kpi;

public class KpiPriorPeriodTests
{
    private static KpiQuery Q(DateTime from, DateTime to) =>
        new(from, to, null, null, null, null, null, to, 1, true);

    [Fact]
    public void PriorPeriod_IsEqualLengthWindowEndingAtFrom()
    {
        var q = Q(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
        var prior = q.PriorPeriod();
        Assert.Equal(new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc), prior.From);
        Assert.Equal(q.From, prior.To);
        Assert.Equal(q.To - q.From, prior.To - prior.From);
    }

    [Fact]
    public void PriorPeriod_OneDayPeriod_IsThePreviousDay()
    {
        var q = Q(new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc));
        var prior = q.PriorPeriod();
        Assert.Equal(new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc), prior.From);
        Assert.Equal(new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc), prior.To);
    }

    [Fact]
    public void PriorPeriod_KeepsEveryOtherField()
    {
        var q = new KpiQuery(DateTime.UtcNow.AddDays(-5), DateTime.UtcNow, new[] { 3 }, "Water", "P1", "TAMC", 9, DateTime.UtcNow, 4, false);
        var prior = q.PriorPeriod();
        Assert.Equal(q.SectionIds, prior.SectionIds);
        Assert.Equal((q.Category, q.Location, q.TestCode, q.AnalystId, q.Now, q.ViewerUserId, q.CanViewTeam),
            (prior.Category, prior.Location, prior.TestCode, prior.AnalystId, prior.Now, prior.ViewerUserId, prior.CanViewTeam));
    }
}

public class KpiPriorWiringTests
{
    [Fact]
    public async Task Prior_OnlyShownWhenPriorNIsAboveZero()
    {
        await using var db = NewDb();
        ApprovedSample(db, 1, Utc(10, 1), Utc(10, 3));
        await db.SaveChangesAsync();
        var kpi = new SampleTatKpi(db);

        Assert.Null((await kpi.ComputeAsync(Query(), default)).Prior);     // nothing before: no prior, not a prior of zero

        ApprovedSample(db, 2, Utc(9, 10), Utc(9, 20));
        await db.SaveChangesAsync();
        var withPrior = (await kpi.ComputeAsync(Query(), default)).Prior;
        Assert.NotNull(withPrior);
        Assert.Equal(1, withPrior!.N);
    }

    [Fact]
    public async Task Prior_BoundaryRecord_BelongsToCurrentPeriodNotPrior()
    {
        // Approved exactly at the start of October: current period (From is inclusive), not the prior one (To is exclusive).
        await using var db = NewDb();
        ApprovedSample(db, 1, Utc(9, 30), Utc(10, 1));
        await db.SaveChangesAsync();

        var r = await new SampleTatKpi(db).ComputeAsync(Query(), default);

        Assert.Equal(1, r.N);
        Assert.Null(r.Prior);
    }

    [Fact]
    public async Task Prior_OneDayPeriod_ComparesWithThePreviousDay()
    {
        await using var db = NewDb();
        ApprovedSample(db, 1, Utc(10, 11), Utc(10, 12, 9));    // approved on the 12th: 1.375 days
        ApprovedSample(db, 2, Utc(10, 10), Utc(10, 11, 18));   // approved on the 11th, the prior day: 1.75 days
        await db.SaveChangesAsync();

        var r = await new SampleTatKpi(db).ComputeAsync(Query(from: Utc(10, 12), to: Utc(10, 13)), default);

        Assert.Equal(1, r.N);
        Assert.Equal(1.38m, r.Value);
        Assert.Equal(1, r.Prior!.N);
        Assert.Equal(1.75m, r.Prior.Value);
    }
}

