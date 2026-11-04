using MicroLIMS.Application.Services.BackgroundJobs;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.API.BackgroundServices;

// How a worker runs one scheduled pass. With a recorder the pass is recorded
// (start, finish, outcome, items, error) and a failure is captured instead of
// thrown. Without one (unit tests that build a worker directly) the body runs
// as it always did and exceptions propagate to the worker's own loop.
internal static class ScheduledJobPass
{
    public static async Task RunAsync(
        IBackgroundJobRunRecorder? recorder,
        string jobName,
        Func<CancellationToken, Task<int?>> body,
        CancellationToken cancellationToken)
    {
        if (recorder is null)
        {
            await body(cancellationToken);
            return;
        }

        await recorder.RunAsync(jobName, BackgroundJobTrigger.Schedule, null, null, body, cancellationToken);
    }
}
