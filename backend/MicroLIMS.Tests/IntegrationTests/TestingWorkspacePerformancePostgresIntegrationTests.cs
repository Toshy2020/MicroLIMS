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
    /// Upper bound budget for SQL commands executed by GetActiveSamplesAsync().
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
    /// Baseline sample batch size N for scalability benchmarking.
    /// 150 samples with 4 test orders and 2 incubations per test order yields 150 samples,
    /// 600 test orders, and 1,200 incubations. This provides sufficient workload to establish
    /// reliable execution timings above system timer jitter while keeping bulk insert fast.
    /// </summary>
    public const int BaseSampleCountN = 150;

    /// <summary>
    /// Scaled sample batch size 4N for scalability benchmarking.
    /// 600 samples with 4 test orders and 2 incubations per test order yields 600 samples,
    /// 2,400 test orders, and 4,800 incubations.
    /// In commit 73d06ea, the pre-fix quadratic ToDto incubation re-scanning incurred
    /// 600 * 4,800 = 2,880,000 comparisons (16x more work than N=150), whereas the post-fix
    /// bucketed algorithm executes in linear O(Samples + Incubations) = 5,400 operations (~4x).
    /// </summary>
    public const int ScaledSampleCount4N = 600;

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
    /// Maximum allowable ratio of execution time between 4N and N sample volumes: time(4N) / time(N).
    /// Under linear scaling O(N), quadrupling data predicts an execution time ratio of ~4.0x.
    /// Under quadratic scaling O(N^2) (the pre-fix defect in ToDto), quadrupling data predicts
    /// a ratio of ~16.0x (4 * 4).
    /// A threshold of 8.0x sits midway between linear (4x) and quadratic (16x), leaving generous
    /// headroom for CI runner CPU jitter and GC pauses while definitively catching quadratic regressions.
    /// </summary>
    public const double MaxLinearGrowthRatio = 8.0;

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
        await SeedSamplesBulkAsync(db, BaseSampleCountN, startIndex: 1, batchTag: "N", causeId, itemId);

        var service = new TestingWorkspaceService(db);

        interceptor.Reset();
        var samplesN = await service.GetActiveSamplesAsync();
        int countN = interceptor.CommandCount;

        Assert.Equal(BaseSampleCountN, samplesN.Count);

        // --- Phase 2: Seed remaining samples up to 4N and measure SQL command count ---
        int additionalSamples = ScaledSampleCount4N - BaseSampleCountN;
        await SeedSamplesBulkAsync(db, additionalSamples, startIndex: BaseSampleCountN + 1, batchTag: "4N", causeId, itemId);

        interceptor.Reset();
        var samples4N = await service.GetActiveSamplesAsync();
        int count4N = interceptor.CommandCount;

        Assert.Equal(ScaledSampleCount4N, samples4N.Count);

        // --- Primary Assertion: SQL count must be IDENTICAL between N and 4N ---
        _output.WriteLine($"[SQL Budget] Commands at N={BaseSampleCountN}: {countN}, Commands at 4N={ScaledSampleCount4N}: {count4N}");
        foreach (var cmd in interceptor.Commands)
        {
            _output.WriteLine($"  Command: {cmd.Replace(Environment.NewLine, " ").Substring(0, Math.Min(120, cmd.Length))}...");
        }

        Assert.True(countN == count4N,
            $"SQL command count grew with data volume: issued {countN} queries at N={BaseSampleCountN} " +
            $"and {count4N} queries at 4N={ScaledSampleCount4N}. Commands at N:\n{string.Join("\n---\n", interceptor.Commands)}");

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
    public async Task GetActiveSamples_WorkGrowsLinearlyNotQuadratically_UnderDataScaling()
    {
        await using var db = _fixture.CreateDbContext();

        await CleanUpSamplesAsync(db);
        var (causeId, itemId) = await EnsureBaselineDataAsync(db);

        // 1. Seed N samples
        await SeedSamplesBulkAsync(db, BaseSampleCountN, startIndex: 1, batchTag: "LN_N", causeId, itemId);

        var service = new TestingWorkspaceService(db);

        // Discard warm-up call before timing (warms EF Core query compilation, model caches, connection pool)
        var warmupResult = await service.GetActiveSamplesAsync();
        Assert.Equal(BaseSampleCountN, warmupResult.Count);

        // Measure time at N (take best of 3 passes to filter out transient GC/runner jitter)
        long elapsedN = long.MaxValue;
        for (int i = 0; i < 3; i++)
        {
            var sw = Stopwatch.StartNew();
            var res = await service.GetActiveSamplesAsync();
            sw.Stop();
            Assert.Equal(BaseSampleCountN, res.Count);
            if (sw.ElapsedMilliseconds < elapsedN)
                elapsedN = sw.ElapsedMilliseconds;
        }

        // 2. Seed remaining up to 4N samples
        int additionalSamples = ScaledSampleCount4N - BaseSampleCountN;
        await SeedSamplesBulkAsync(db, additionalSamples, startIndex: BaseSampleCountN + 1, batchTag: "LN_4N", causeId, itemId);

        // Measure time at 4N (best of 3 passes)
        long elapsed4N = long.MaxValue;
        for (int i = 0; i < 3; i++)
        {
            var sw = Stopwatch.StartNew();
            var res = await service.GetActiveSamplesAsync();
            sw.Stop();
            Assert.Equal(ScaledSampleCount4N, res.Count);
            if (sw.ElapsedMilliseconds < elapsed4N)
                elapsed4N = sw.ElapsedMilliseconds;
        }

        // Prevent division by zero if elapsed time is sub-millisecond
        double timeN = Math.Max(elapsedN, 1);
        double time4N = Math.Max(elapsed4N, 1);
        double ratio = time4N / timeN;

        _output.WriteLine($"[Linear Scaling Budget] N={BaseSampleCountN}: {timeN} ms | 4N={ScaledSampleCount4N}: {time4N} ms | Ratio: {ratio:F2}x (Budget: < {MaxLinearGrowthRatio:F1}x)");

        // Primary Assertion: Ratio must be well under quadratic (8.0x budget vs 16.0x quadratic)
        Assert.True(ratio < MaxLinearGrowthRatio,
            $"Execution time scaled quadratically: time(4N)={time4N}ms / time(N)={timeN}ms = {ratio:F2}x, " +
            $"which exceeds the performance budget ratio of {MaxLinearGrowthRatio:F1}x (linear predicts ~4.0x, " +
            "quadratic predicts ~16.0x). Likely regression: ToDto incubation comparison nested loop reintroduced.");
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
        var samples = new List<Sample>(count);

        for (int i = 0; i < count; i++)
        {
            int index = startIndex + i;
            var sample = new Sample
            {
                ReferenceNumber = $"FP-{batchTag}-{index:D5}",
                ControlNumber = $"CTRL-{batchTag}-{index:D5}",
                Category = SampleCategory.FinishedProduct,
                Status = SampleStatus.Received,
                PreparationStatus = SamplePreparationStatus.Ready,
                ReceivedByUserId = _fixture.SeededUserId,
                CauseOfTestingId = causeId,
                ItemId = itemId,
                ReceivedAt = baseTime.AddMinutes(-index),
                SampledBy = "Performance Benchmark Sampler"
            };

            for (int t = 0; t < TestOrdersPerSample; t++)
            {
                var testOrder = new TestOrder
                {
                    Sample = sample,
                    TestCode = testCodes[t % testCodes.Length],
                    Status = ApprovalStatus.InProgress,
                    CurrentStep = WorkflowStep.Incubating,
                    AssignedAnalystId = _fixture.SeededUserId
                };

                for (int inc = 1; inc <= IncubationsPerTestOrder; inc++)
                {
                    var incubation = new Incubation
                    {
                        TestOrder = testOrder,
                        StepNumber = inc,
                        StepName = inc == 1 ? "Enrichment TSB" : "Subculture",
                        StartedAt = baseTime.AddHours(-12 * inc),
                        IncubationStartUtc = baseTime.AddHours(-12 * inc),
                        IncubationEndUtc = baseTime.AddHours(12 * inc),
                        ExpectedReadingAt = baseTime.AddHours(12 * inc),
                        StartedByUserId = _fixture.SeededUserId,
                        StageNumber = 1
                    };
                    testOrder.Incubations.Add(incubation);
                }

                var location = new SampleLocation
                {
                    Sample = sample,
                    TestOrder = testOrder,
                    LocationType = LocationType.Room,
                    DilutionFactor = 1.0m
                };
                sample.Locations.Add(location);

                sample.TestOrders.Add(testOrder);
            }

            samples.Add(sample);
        }

        db.Samples.AddRange(samples);
        await db.SaveChangesAsync();
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
