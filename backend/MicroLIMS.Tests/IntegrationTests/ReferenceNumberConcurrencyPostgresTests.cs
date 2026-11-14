using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Sample numbers are "count of this month's samples + 1". Two receipts that
// count at the same moment pick the same number; the advisory lock in
// ReferenceNumberGenerator makes the next one wait for the first to commit.
// Only a real PostgreSQL shows that: the lock, the transaction and the unique
// index do not exist in the in-memory provider.
[Collection("PostgresDatabaseCollection")]
public class ReferenceNumberConcurrencyPostgresTests
{
    private static readonly TimeSpan Blocked = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan Generous = TimeSpan.FromSeconds(30);

    private readonly PostgresTestFixture _fixture;

    public ReferenceNumberConcurrencyPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed class FixedClock(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    // Mid-month noon UTC: the same month in the lab's time zone. Every test
    // uses its own year so tests sharing the database never share a counter.
    private static FixedClock ClockIn(int year, int month) => new(new DateTimeOffset(year, month, 15, 12, 0, 0, TimeSpan.Zero));

    private async Task<int> NewCauseAsync()
    {
        await using var db = _fixture.CreateDbContext();
        var cause = new CauseOfTesting { Name = $"Routine {Guid.NewGuid():N}", IsActive = true };
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();
        return cause.Id;
    }

    private Sample NewSample(string referenceNumber, SampleCategory category, int causeId, DateTime receivedAtUtc) => new()
    {
        ReferenceNumber = referenceNumber,
        Category = category,
        CauseOfTestingId = causeId,
        SampledBy = "Analyst",
        ControlNumber = "CTRL",
        ReceivedByUserId = _fixture.SeededUserId,
        ReceivedAt = receivedAtUtc,
    };

    private async Task<string> GenerateAndInsertAsync(MicroLimsDbContext db, TimeProvider clock, SampleCategory category, int causeId)
    {
        var number = await new ReferenceNumberGenerator(db, clock).GenerateAsync(category);
        db.Samples.Add(NewSample(number, category, causeId, clock.GetUtcNow().UtcDateTime));
        await db.SaveChangesAsync();
        return number;
    }

    [PostgresFact]
    public async Task TwoConcurrentTransactions_SameCategory_GetDistinctConsecutiveNumbers()
    {
        var clock = ClockIn(2081, 4);
        var causeId = await NewCauseAsync();
        var firstHasNumber = new TaskCompletionSource();
        var letFirstFinish = new TaskCompletionSource();

        var first = Task.Run(async () =>
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var number = await new ReferenceNumberGenerator(db, clock).GenerateAsync(SampleCategory.FinishedProduct);
            firstHasNumber.SetResult();
            // The number is generated but the sample is not inserted yet:
            // a second transaction counting now would get the same number.
            await letFirstFinish.Task.WaitAsync(Generous);
            db.Samples.Add(NewSample(number, SampleCategory.FinishedProduct, causeId, clock.GetUtcNow().UtcDateTime));
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return number;
        });

        await firstHasNumber.Task.WaitAsync(Generous);

        var second = Task.Run(async () =>
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var number = await GenerateAndInsertAsync(db, clock, SampleCategory.FinishedProduct, causeId);
            await tx.CommitAsync();
            return number;
        });

        // The second transaction must be waiting for the first one's lock.
        var finishedEarly = await Task.WhenAny(second, Task.Delay(Blocked));
        Assert.NotSame(second, finishedEarly);

        letFirstFinish.SetResult();
        var firstNumber = await first.WaitAsync(Generous);
        var secondNumber = await second.WaitAsync(Generous);

        Assert.Equal("FP0481001", firstNumber);
        Assert.Equal("FP0481002", secondNumber);
        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.Samples.AsNoTracking()
            .Where(s => s.ReferenceNumber.StartsWith("FP0481"))
            .Select(s => s.ReferenceNumber).OrderBy(n => n).ToListAsync();
        Assert.Equal(new[] { "FP0481001", "FP0481002" }, persisted);
    }

    [PostgresFact]
    public async Task TwoConcurrentProductReceives_BothSucceed()
    {
        var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        int itemId, causeId, microId;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var micro = await arrangeDb.DocumentSections.FirstAsync(s => s.Code == "MICRO");
            microId = micro.Id;
            var code = $"CC{uid}";
            arrangeDb.TestDefinitions.Add(new TestDefinition { Code = code, DisplayName = code, SectionId = micro.Id, IsActive = true });
            var item = new Item { Code = $"RM-{uid}", Name = $"Concurrent {uid}", Category = SampleCategory.RawMaterial, IsActive = true };
            item.AssignedTests.Add(new SampleTest { TestCode = code, DisplayName = code });
            arrangeDb.Items.Add(item);
            await arrangeDb.SaveChangesAsync();
            var cause = new CauseOfTesting { Name = $"Routine {uid}", IsActive = true };
            arrangeDb.CausesOfTesting.Add(cause);
            await arrangeDb.SaveChangesAsync();
            itemId = item.Id;
            causeId = cause.Id;
        }

        var gate = new TaskCompletionSource();
        async Task<string> ReceiveAsync(string suffix)
        {
            await using var db = _fixture.CreateDbContext();
            await gate.Task;
            var sample = await new ProductWorkflowEngine(db, new ReferenceNumberGenerator(db)).ReceiveAsync(new ItemBasedReceiveRequest(
                itemId, causeId, "10 g", "Analyst", $"LOT-{uid}-{suffix}", $"CTRL-{uid}-{suffix}", DateTime.UtcNow, DateTime.UtcNow.AddYears(1), null,
                _fixture.SeededUserId, new[] { microId }));
            return sample.ReferenceNumber;
        }

        var tasks = Enumerable.Range(0, 2).Select(i => Task.Run(() => ReceiveAsync(i.ToString()))).ToArray();
        gate.SetResult();
        var numbers = await Task.WhenAll(tasks).WaitAsync(Generous);

        Assert.NotEqual(numbers[0], numbers[1]);
        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.Samples.AsNoTracking()
            .Where(s => s.ItemId == itemId).Select(s => s.ReferenceNumber).ToListAsync();
        Assert.Equal(numbers.OrderBy(n => n), persisted.OrderBy(n => n));
        // Consecutive: no number was burned by a failed attempt.
        var sequences = persisted.Select(n => int.Parse(n[^3..])).OrderBy(n => n).ToList();
        Assert.Equal(sequences[0] + 1, sequences[1]);
    }

    [PostgresFact]
    public async Task FiveSamplesInOneTransaction_GetFiveConsecutiveNumbers()
    {
        var clock = ClockIn(2082, 5);
        var causeId = await NewCauseAsync();

        await using (var actDb = _fixture.CreateDbContext())
        {
            await using var tx = await actDb.Database.BeginTransactionAsync();
            for (var i = 0; i < 5; i++)
                await GenerateAndInsertAsync(actDb, clock, SampleCategory.RawMaterial, causeId);
            await tx.CommitAsync();
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.Samples.AsNoTracking()
            .Where(s => s.ReferenceNumber.StartsWith("RM0582"))
            .Select(s => s.ReferenceNumber).OrderBy(n => n).ToListAsync();
        Assert.Equal(new[] { "RM0582001", "RM0582002", "RM0582003", "RM0582004", "RM0582005" }, persisted);
    }

    [PostgresFact]
    public async Task DifferentCategories_DoNotBlockEachOther()
    {
        var clock = ClockIn(2083, 6);
        var causeId = await NewCauseAsync();

        await using var firstDb = _fixture.CreateDbContext();
        await using var firstTx = await firstDb.Database.BeginTransactionAsync();
        Assert.Equal("FP0683001", await new ReferenceNumberGenerator(firstDb, clock).GenerateAsync(SampleCategory.FinishedProduct));

        // The first transaction is still open and holds its lock.
        var other = Task.Run(async () =>
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var number = await GenerateAndInsertAsync(db, clock, SampleCategory.RawMaterial, causeId);
            await tx.CommitAsync();
            return number;
        });

        Assert.Equal("RM0683001", await other.WaitAsync(Generous));
        await firstTx.RollbackAsync();
    }

    [PostgresFact]
    public async Task NumberFormat_Unchanged()
    {
        var clock = ClockIn(2085, 7);
        var causeId = await NewCauseAsync();
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            for (var i = 1; i <= 4; i++)
                arrangeDb.Samples.Add(NewSample($"FP078{i}", SampleCategory.FinishedProduct, causeId, clock.GetUtcNow().UtcDateTime.AddHours(i)));
            // Another category and another month do not count.
            arrangeDb.Samples.Add(NewSample("RM0785001", SampleCategory.RawMaterial, causeId, clock.GetUtcNow().UtcDateTime));
            arrangeDb.Samples.Add(NewSample("FP0685001", SampleCategory.FinishedProduct, causeId, clock.GetUtcNow().UtcDateTime.AddMonths(-1)));
            await arrangeDb.SaveChangesAsync();
        }

        string number;
        await using (var actDb = _fixture.CreateDbContext())
        {
            await using var tx = await actDb.Database.BeginTransactionAsync();
            number = await new ReferenceNumberGenerator(actDb, clock).GenerateAsync(SampleCategory.FinishedProduct);
            await tx.RollbackAsync();
        }

        Assert.Equal("FP0785005", number);
    }

    [PostgresFact]
    public async Task NoTransaction_Throws()
    {
        await using var db = _fixture.CreateDbContext();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ReferenceNumberGenerator(db, ClockIn(2084, 8)).GenerateAsync(SampleCategory.Water));

        Assert.Contains("transaction", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // A clash the lock cannot prevent (a number taken outside the monthly
    // count) is reported by TrySaveChangesAsync, and the surrounding
    // transaction stays usable: EF rolls a failed save back to a savepoint,
    // so the one-retry pattern is valid inside a transaction.
    [PostgresFact]
    public async Task NumberTakenOutsideTheCount_SaveReportsClash_AndTransactionStaysUsable()
    {
        var clock = ClockIn(2086, 9);
        var causeId = await NewCauseAsync();
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            // Holds "RM0986001" but was received in another month, so the count stays 0.
            arrangeDb.Samples.Add(NewSample("RM0986001", SampleCategory.RawMaterial, causeId, clock.GetUtcNow().UtcDateTime.AddMonths(-2)));
            await arrangeDb.SaveChangesAsync();
        }

        await using var actDb = _fixture.CreateDbContext();
        await using var tx = await actDb.Database.BeginTransactionAsync();
        var number = await new ReferenceNumberGenerator(actDb, clock).GenerateAsync(SampleCategory.RawMaterial);
        Assert.Equal("RM0986001", number);
        actDb.Samples.Add(NewSample(number, SampleCategory.RawMaterial, causeId, clock.GetUtcNow().UtcDateTime));

        Assert.False(await actDb.TrySaveChangesAsync(UniqueIndexNames.SampleReferenceNumber));
        // The failed statement did not poison the transaction.
        Assert.Equal(1, await actDb.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM \"Samples\" WHERE \"ReferenceNumber\" = 'RM0986001'").SingleAsync());
        await tx.RollbackAsync();
    }
}
