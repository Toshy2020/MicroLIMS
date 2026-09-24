using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class TestingWorkspacePerformancePostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Upper bound budget for SQL commands executed by GetActiveSamplesAsync(filter).
    /// In commit 73d06ea, measured at a maximum of 10 commands (typically 5 single-query
    /// commands in EF Core 8: Samples with navigations, TestDefinitions with steps, Incubations,
    /// SampleLocations count, and Users; up to 10 if collection navigation splitting occurs).
    /// Protects against N+1 query regressions where queries are executed per sample,
    /// per test order, or per incubation instead of in bounded bulk queries.
    /// </summary>
    public const int ExpectedMaxSqlCommandCount = 10;

    /// <summary>
    /// Default page size when filter.PageSize is not specified or <= 0.
    /// Defined in TestingWorkspaceService.GetActiveSamplesAsync(filter).
    /// Protects against returning unbounded sample records by default.
    /// </summary>
    public const int DefaultPageSize = 50;

    /// <summary>
    /// Hard maximum page size enforced server-side.
    /// Defined in TestingWorkspaceService.GetActiveSamplesAsync(filter): Math.Min(filter.PageSize, 200).
    /// Protects against excessive database materialisation or memory exhaustion when a caller
    /// requests an unbounded or maliciously large page size (e.g. pageSize=10000).
    /// </summary>
    public const int MaxServerPageSizeClamp = 200;

    /// <summary>
    /// Total sample volumes for the scaling benchmark: 150 and then 1,200 samples,
    /// each with 4 test orders and 2 incubations per test order (up to 4,800 test
    /// orders and 9,600 incubations). The benchmark always reads one page of
    /// ScalingPageSize, so the data volume grows 8x while the page stays the same.
    /// </summary>
    public const int BaseSampleCountN = 150;

    public const int ScaledSampleCount8N = 1200;

    /// <summary>
    /// The page the scaling benchmark reads - the list's default page size.
    /// </summary>
    public const int ScalingPageSize = DefaultPageSize;

    /// <summary>
    /// Sample volumes for the SQL command-count test. An N+1 regression issues extra
    /// commands per sample, so any growth shows up at 10 vs 40 samples just as it does
    /// at 150 vs 600 - only the timing test needs benchmark-scale data.
    /// </summary>
    public const int CommandCountSampleCount = 10;
    public const int CommandCountScaledSampleCount = 40;

    /// <summary>
    /// Typical number of test orders associated with each sample in the synthetic workload.
    /// Reflects standard microbiological releases requiring concurrent assays (e.g. TAMC, TYMC, EC, SA).
    /// </summary>
    public const int TestOrdersPerSample = 4;

    /// <summary>
    /// Typical number of incubation stages associated with each test order.
    /// Reflects multi-stage incubation workflows (e.g. enrichment stage + plating stage).
    /// </summary>
    public const int IncubationsPerTestOrder = 2;

    /// <summary>
    /// Maximum allowable ratio time(page at 8N) / time(page at N). Reading one fixed-size
    /// page should cost about the same whatever the table holds (~1x); work that
    /// scales with every sample in the table - mapping or loading everything and
    /// paging in memory - predicts ~8x. 2x sits between the two, with headroom for
    /// CI runner jitter and GC pauses.
    /// </summary>
    public const double MaxPageTimeGrowthRatio = 2.0;

    public TestingWorkspacePerformancePostgresIntegrationTests(PostgresTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [PostgresFact]
    public async Task GetActiveSamples_SqlCommandCount_IsBounded_AndDoesNotGrowWithData()
    {
        var interceptor = new SqlCommandCountingInterceptor();
        await using var db = _fixture.CreateDbContext(interceptor);

        await CleanUpSamplesAsync(db);
        var (causeId, itemId) = await EnsureBaselineDataAsync(db);

        // --- Phase 1: Seed N samples and measure SQL command count ---
        await SeedSamplesBulkAsync(db, CommandCountSampleCount, startIndex: 1, batchTag: "N", causeId, itemId);

        var service = new TestingWorkspaceService(db);

        interceptor.Reset();
        // One page large enough to hold every seeded sample, so the mapped rows grow 4x.
        var wholeList = new TestingWorkspaceFilterDto { PageSize = MaxServerPageSizeClamp };
        var samplesN = await service.GetActiveSamplesAsync(wholeList);
        int countN = interceptor.CommandCount;

        Assert.Equal(CommandCountSampleCount, samplesN.Items.Count);

        // --- Phase 2: Seed remaining samples up to 4N and measure SQL command count ---
        int additionalSamples = CommandCountScaledSampleCount - CommandCountSampleCount;
        await SeedSamplesBulkAsync(db, additionalSamples, startIndex: CommandCountSampleCount + 1, batchTag: "4N", causeId, itemId);

        interceptor.Reset();
        var samples4N = await service.GetActiveSamplesAsync(wholeList);
        int count4N = interceptor.CommandCount;

        Assert.Equal(CommandCountScaledSampleCount, samples4N.Items.Count);

        // --- Primary Assertion: SQL count must be IDENTICAL between N and 4N ---
        _output.WriteLine($"[SQL Budget] Commands at N={CommandCountSampleCount}: {countN}, Commands at 4N={CommandCountScaledSampleCount}: {count4N}");
        foreach (var cmd in interceptor.Commands)
        {
            _output.WriteLine($"  Command: {cmd.Replace(Environment.NewLine, " ").Substring(0, Math.Min(120, cmd.Length))}...");
        }

        Assert.True(countN == count4N,
            $"SQL command count grew with data volume: issued {countN} queries at N={CommandCountSampleCount} " +
            $"and {count4N} queries at 4N={CommandCountScaledSampleCount}. Commands at N:\n{string.Join("\n---\n", interceptor.Commands)}");

        // The query count is bounded by budget (measured at 5-10 commands depending on EF Core collection splitting)
        Assert.True(countN <= ExpectedMaxSqlCommandCount,
            $"SQL command count {countN} exceeded budget of {ExpectedMaxSqlCommandCount}. Commands:\n{string.Join("\n---\n", interceptor.Commands)}");
    }

    [PostgresFact]
    public async Task GetActiveSamples_PagedOverload_NeverMaterialisesMoreThanPageSize_AndClampsExcessiveRequests()
    {
        await using var db = _fixture.CreateDbContext();

        await CleanUpSamplesAsync(db);
        var (causeId, itemId) = await EnsureBaselineDataAsync(db);

        // Seed 250 samples (enough to exceed default 50 and clamp threshold 200)
        const int seededCount = 250;
        await SeedSamplesBulkAsync(db, seededCount, startIndex: 1, batchTag: "PAGE", causeId, itemId);

        var service = new TestingWorkspaceService(db);

        // 1. Default request (no filter / default page size 50)
        var defaultResult = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto());
        _output.WriteLine($"[Paging Budget] Default pageSize: requested 0 -> returned {defaultResult.Items.Count}, PageSize={defaultResult.PageSize}, TotalCount={defaultResult.TotalCount}");
        Assert.Equal(DefaultPageSize, defaultResult.PageSize);
        Assert.Equal(DefaultPageSize, defaultResult.Items.Count);
        Assert.Equal(seededCount, defaultResult.TotalCount);

        // 2. Specific page size (e.g. 25)
        var specificResult = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { Page = 1, PageSize = 25 });
        _output.WriteLine($"[Paging Budget] Explicit pageSize: requested 25 -> returned {specificResult.Items.Count}, PageSize={specificResult.PageSize}, TotalCount={specificResult.TotalCount}");
        Assert.Equal(25, specificResult.PageSize);
        Assert.Equal(25, specificResult.Items.Count);
        Assert.Equal(seededCount, specificResult.TotalCount);

        // 3. Excessive page size (10,000) must be clamped to MaxServerPageSizeClamp (200)
        var clampedResult = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { Page = 1, PageSize = 10000 });
        _output.WriteLine($"[Paging Budget] Excessive pageSize: requested 10000 -> clamped to {clampedResult.PageSize}, returned {clampedResult.Items.Count}, TotalCount={clampedResult.TotalCount}");
        Assert.Equal(MaxServerPageSizeClamp, clampedResult.PageSize);
        Assert.Equal(MaxServerPageSizeClamp, clampedResult.Items.Count);
        Assert.Equal(seededCount, clampedResult.TotalCount);

        // Items materialised should never exceed clamped page size
        Assert.True(clampedResult.Items.Count <= MaxServerPageSizeClamp,
            $"Paged endpoint materialised {clampedResult.Items.Count} items, which exceeds the server clamp of {MaxServerPageSizeClamp}.");
    }

    [PostgresFact]
    public async Task GetActiveSamples_PageTime_DoesNotGrowWithTotalData()
    {
        await using var db = _fixture.CreateDbContext();

        await CleanUpSamplesAsync(db);
        var (causeId, itemId) = await EnsureBaselineDataAsync(db);

        // 1. Seed N samples
        await SeedSamplesBulkAsync(db, BaseSampleCountN, startIndex: 1, batchTag: "LN_N", causeId, itemId);

        var service = new TestingWorkspaceService(db);
        var firstPage = new TestingWorkspaceFilterDto { Page = 1, PageSize = ScalingPageSize };

        // Discard warm-up call before timing (warms EF Core query compilation, model caches, connection pool)
        var warmupResult = await service.GetActiveSamplesAsync(firstPage);
        Assert.Equal(ScalingPageSize, warmupResult.Items.Count);

        double timeN = await BestPageTimeMsAsync(service, firstPage, BaseSampleCountN);

        // 2. Seed remaining up to 8N samples
        int additionalSamples = ScaledSampleCount8N - BaseSampleCountN;
        await SeedSamplesBulkAsync(db, additionalSamples, startIndex: BaseSampleCountN + 1, batchTag: "LN_8N", causeId, itemId);

        double time8N = await BestPageTimeMsAsync(service, firstPage, ScaledSampleCount8N);

        double ratio = time8N / timeN;

        _output.WriteLine($"[Page Time Budget] page of {ScalingPageSize} at N={BaseSampleCountN}: {timeN:F1} ms | at 8N={ScaledSampleCount8N}: {time8N:F1} ms | Ratio: {ratio:F2}x (Budget: < {MaxPageTimeGrowthRatio:F1}x)");

        Assert.True(ratio < MaxPageTimeGrowthRatio,
            $"Reading one page of {ScalingPageSize} got slower as the table grew: time(8N)={time8N:F1}ms / time(N)={timeN:F1}ms = {ratio:F2}x, " +
            $"which exceeds the budget of {MaxPageTimeGrowthRatio:F1}x (a fixed page predicts ~1x, work over every sample ~8x). " +
            "Likely regression: the request maps or loads the whole table instead of the page.");
    }

    // Best of 5 passes filters out transient GC/runner jitter.
    private static async Task<double> BestPageTimeMsAsync(TestingWorkspaceService service, TestingWorkspaceFilterDto filter, int expectedTotal)
    {
        double best = double.MaxValue;
        for (int i = 0; i < 5; i++)
        {
            var sw = Stopwatch.StartNew();
            var res = await service.GetActiveSamplesAsync(filter);
            sw.Stop();
            Assert.Equal(ScalingPageSize, res.Items.Count);
            Assert.Equal(expectedTotal, res.TotalCount);
            best = Math.Min(best, sw.Elapsed.TotalMilliseconds);
        }
        return Math.Max(best, 0.1);
    }

    private static async Task CleanUpSamplesAsync(MicroLimsDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Samples\" CASCADE;");
    }

    private async Task<(int CauseId, int ItemId)> EnsureBaselineDataAsync(MicroLimsDbContext db)
    {
        var cause = await db.CausesOfTesting.FirstOrDefaultAsync();
        if (cause == null)
        {
            cause = new CauseOfTesting { Name = "Routine Release" };
            db.CausesOfTesting.Add(cause);
            await db.SaveChangesAsync();
        }

        var item = await db.Items.FirstOrDefaultAsync();
        if (item == null)
        {
            item = new Item { Code = "TEST-PERF-01", Name = "Performance Test Item" };
            db.Items.Add(item);
            await db.SaveChangesAsync();
        }

        var testCodes = new[] { "TAMC", "TYMC", "EC", "SA" };
        var existingCodes = await db.TestDefinitions
            .Where(t => testCodes.Contains(t.Code))
            .Select(t => t.Code)
            .ToListAsync();

        foreach (var code in testCodes)
        {
            if (!existingCodes.Contains(code))
            {
                var def = new TestDefinition
                {
                    Code = code,
                    DisplayName = $"Assay {code}",
                    WorkflowType = WorkflowType.Observation,
                    IsActive = true,
                    Steps = new List<TestWorkflowStep>
                    {
                        new()
                        {
                            StepOrder = 1,
                            StepName = "Enrichment TSB",
                            StepType = StepType.BrothEnrichment,
                            IncubationMinHours = 24,
                            IncubationMaxHours = 48,
                            TemperatureMin = 30m,
                            TemperatureMax = 35m,
                            IsFinalStep = false
                        }
                    }
                };
                db.TestDefinitions.Add(def);
            }
        }
        await db.SaveChangesAsync();

        return (cause.Id, item.Id);
    }

    // Seeds `count` samples, each with TestOrdersPerSample test orders (one location
    // and IncubationsPerTestOrder incubations per order), in ONE set-based statement.
    //
    // Building the same graph through EF Core meant change-tracking and inserting
    // ~10,000 entities per 600-sample run, which dominated this class's runtime.
    // The chained data-modifying CTEs let PostgreSQL generate the rows and wire the
    // foreign keys itself; the values match what the EF graph used to write.
    private async Task SeedSamplesBulkAsync(
        MicroLimsDbContext db,
        int count,
        int startIndex,
        string batchTag,
        int causeId,
        int itemId)
    {
        var testCodes = new[] { "TAMC", "TYMC", "EC", "SA" };
        var baseTime = DateTime.UtcNow;
        int endIndex = startIndex + count - 1;
        int lastTestOrderSlot = TestOrdersPerSample - 1;
        int userId = _fixture.SeededUserId;

        await db.Database.ExecuteSqlAsync($"""
            WITH seeded_samples AS (
                INSERT INTO "Samples" ("ReferenceNumber", "ControlNumber", "Category", "Status", "PreparationStatus",
                                       "ReceivedByUserId", "CauseOfTestingId", "ItemId", "ReceivedAt", "SampledBy")
                SELECT 'FP-' || {batchTag} || '-' || lpad(g::text, 5, '0'),
                       'CTRL-' || {batchTag} || '-' || lpad(g::text, 5, '0'),
                       {(int)SampleCategory.FinishedProduct}, {(int)SampleStatus.Received}, {(int)SamplePreparationStatus.Ready},
                       {userId}, {causeId}, {itemId}, {baseTime} - make_interval(mins => g),
                       'Performance Benchmark Sampler'
                FROM generate_series({startIndex}, {endIndex}) AS g
                RETURNING "Id"
            ),
            seeded_orders AS (
                INSERT INTO "TestOrders" ("SampleId", "TestCode", "Status", "CurrentStep", "AssignedAnalystId")
                SELECT s."Id", ({testCodes})[t % {testCodes.Length} + 1],
                       {(int)ApprovalStatus.InProgress}, {(int)WorkflowStep.Incubating}, {userId}
                FROM seeded_samples s CROSS JOIN generate_series(0, {lastTestOrderSlot}) AS t
                RETURNING "Id", "SampleId"
            ),
            seeded_incubations AS (
                INSERT INTO "Incubations" ("TestOrderId", "StepNumber", "StepName", "StartedAt", "IncubationStartUtc",
                                           "IncubationEndUtc", "ExpectedReadingAt", "StartedByUserId", "StageNumber")
                SELECT o."Id", inc, CASE WHEN inc = 1 THEN 'Enrichment TSB' ELSE 'Subculture' END,
                       {baseTime} - make_interval(hours => 12 * inc), {baseTime} - make_interval(hours => 12 * inc),
                       {baseTime} + make_interval(hours => 12 * inc), {baseTime} + make_interval(hours => 12 * inc),
                       {userId}, 1
                FROM seeded_orders o CROSS JOIN generate_series(1, {IncubationsPerTestOrder}) AS inc
            )
            INSERT INTO "SampleLocations" ("SampleId", "TestOrderId", "LocationType", "DilutionFactor")
            SELECT o."SampleId", o."Id", {(int)LocationType.Room}, 1.0
            FROM seeded_orders o
            """);
    }

    private sealed class SqlCommandCountingInterceptor : DbCommandInterceptor
    {
        private int _commandCount;
        private readonly List<string> _commands = new();

        public int CommandCount => Volatile.Read(ref _commandCount);
        public IReadOnlyList<string> Commands { get { lock (_commands) return _commands.ToList(); } }

        public void Reset()
        {
            Interlocked.Exchange(ref _commandCount, 0);
            lock (_commands) _commands.Clear();
        }

        public override DbDataReader ReaderExecuted(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result)
        {
            Interlocked.Increment(ref _commandCount);
            lock (_commands) _commands.Add(command.CommandText);
            return base.ReaderExecuted(command, eventData, result);
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _commandCount);
            lock (_commands) _commands.Add(command.CommandText);
            return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
        }
    }
}
