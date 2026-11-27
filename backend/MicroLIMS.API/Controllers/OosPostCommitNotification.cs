namespace MicroLIMS.API.Controllers;

internal static class OosPostCommitNotification
{
    internal static async Task SendAsync(Func<int, CancellationToken, Task> send, int investigationId, ILogger logger,
        CancellationToken ct = default)
    {
        try
        {
            await send(investigationId, ct);
        }
        catch (Exception error)
        {
            logger.LogWarning(error, "Failed to publish {NotificationEvent} for committed OOS investigation {InvestigationId}.",
                send.Method.Name, investigationId);
        }
    }
}
