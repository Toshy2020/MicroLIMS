using MicroLIMS.Application.Services.BackgroundJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces.DocumentControl;

namespace MicroLIMS.API.BackgroundServices;

public class DocumentEffectiveDateWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DocumentEffectiveDateWorker> _logger;
    private readonly SemaphoreSlim _concurrencyLock = new(1, 1);
    private readonly IBackgroundJobRunRecorder? _recorder;

    public const string ProcessName = "DocumentEffectiveDateWorker";
    public const int DefaultIntervalMinutes = 60;

    public DocumentEffectiveDateWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<DocumentEffectiveDateWorker> logger,
        IBackgroundJobRunRecorder? recorder = null)
    {
        _recorder = recorder;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[{Worker}] Background service is starting.", ProcessName);

        // Determine configured poll interval
        var intervalMinutes = _configuration.GetValue<int?>(
            "DocumentControl:EffectiveDateWorkerIntervalMinutes") ?? DefaultIntervalMinutes;

        if (intervalMinutes <= 0)
        {
            intervalMinutes = DefaultIntervalMinutes;
        }

        var interval = TimeSpan.FromMinutes(intervalMinutes);
        _logger.LogInformation(
            "[{Worker}] Configured evaluation interval: {Minutes} minute(s).",
            ProcessName, intervalMinutes);

        // 1. Immediate Startup / Downtime Recovery Cycle (DC-URS-181)
        try
        {
            _logger.LogInformation(
                "[{Worker}] Executing initial startup / downtime recovery catch-up cycle.", ProcessName);
            await ScheduledJobPass.RunAsync(_recorder, ProcessName, async ct => await RunCycleAsync(ct), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("[{Worker}] Startup cycle cancelled during shutdown.", ProcessName);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Worker}] Unhandled error during startup recovery cycle.", ProcessName);
        }

        // 2. Periodic Scheduled Execution Loop (DC-URS-177, DC-URS-182)
        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await ScheduledJobPass.RunAsync(_recorder, ProcessName, async ct => await RunCycleAsync(ct), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Worker}] Unhandled exception encountered during execution loop.", ProcessName);
            }
        }

        _logger.LogInformation("[{Worker}] Background service has stopped gracefully.", ProcessName);
    }

    // Returns the number of items acted on: revisions activated, review tasks created, escalations created.
    public async Task<int> RunCycleAsync(CancellationToken cancellationToken = default)
    {
        // Non-overlapping execution guard: if a cycle is already actively executing, skip (concurrency-safe)
        if (!await _concurrencyLock.WaitAsync(0, cancellationToken))
        {
            _logger.LogWarning(
                "[{Worker}] A previous processing cycle is still in progress. Skipping overlapping execution tick.",
                ProcessName);
            return 0;
        }

        var processed = 0;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<IMicroLimsDbContext>().CurrentSystemProcessName = ProcessName;
            var effectiveDateService = scope.ServiceProvider.GetRequiredService<IDocumentEffectiveDateService>();
            var periodicReviewService = scope.ServiceProvider.GetService<IPeriodicReviewService>();

            // 1. Effective Date Matured Revisions Processing (WP5: DC-URS-177, DC-URS-178)
            var result = await effectiveDateService.ProcessMaturedRevisionsAsync(
                utcNowOverride: null,
                cancellationToken: cancellationToken);

            processed += result.SuccessfullyActivatedCount;
            if (result.EligibleRevisionsCount > 0)
            {
                _logger.LogInformation(
                    "[{Worker}] Effective date cycle completed. Activated: {Activated}/{Eligible}, Failures: {Failures}",
                    ProcessName,
                    result.SuccessfullyActivatedCount,
                    result.EligibleRevisionsCount,
                    result.FailedRevisionsCount);
            }

            // 2. Periodic Review Due Tasks Generation (WP6: DC-URS-044, DC-URS-179, DC-URS-181)
            if (periodicReviewService != null)
            {
                var reviewResult = await periodicReviewService.GenerateDueReviewTasksAsync(
                    utcNowOverride: null,
                    cancellationToken: cancellationToken);

                processed += reviewResult.CreatedTasksCount;
                if (reviewResult.CreatedTasksCount > 0 || reviewResult.Errors.Count > 0)
                {
                    _logger.LogInformation(
                        "[{Worker}] Periodic review task generation cycle completed. Created: {Created}, Evaluated: {Evaluated}, Errors: {Errors}",
                        ProcessName,
                        reviewResult.CreatedTasksCount,
                        reviewResult.EvaluatedMastersCount,
                        reviewResult.Errors.Count);
                }
            }

            // 3. Document Training Escalation & Overdue Evaluation (Release 1c - WP4: DC-URS-084, DC-URS-116)
            var escalationService = scope.ServiceProvider.GetService<IDocumentEscalationService>();
            if (escalationService != null)
            {
                var escalationResult = await escalationService.ProcessDueEscalationsAsync(
                    utcNowOverride: null,
                    processName: ProcessName,
                    cancellationToken: cancellationToken);

                processed += escalationResult.TotalEscalationsCreated;
                if (escalationResult.TotalEscalationsCreated > 0 || escalationResult.Errors.Count > 0)
                {
                    _logger.LogInformation(
                        "[{Worker}] Document training escalation cycle completed. Created: {Created} (Approaching: {Approaching}, Due: {Due}, Overdue: {Overdue}), Evaluated: {Evaluated}, Errors: {Errors}",
                        ProcessName,
                        escalationResult.TotalEscalationsCreated,
                        escalationResult.ApproachingDueCreated,
                        escalationResult.DueCreated,
                        escalationResult.OverdueCreated,
                        escalationResult.TotalEvaluatedCount,
                        escalationResult.Errors.Count);
                }
            }
            return processed;
        }
        finally
        {
            _concurrencyLock.Release();
        }
    }
}
