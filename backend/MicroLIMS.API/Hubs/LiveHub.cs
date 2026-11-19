using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MicroLIMS.API.Hubs;

/// <summary>
/// SignalR hub at <c>/hubs/live</c> delivering data-free invalidation signals
/// to authenticated users, readable laboratory sections, and authorized records.
/// </summary>
[Authorize]
public sealed class LiveHub : Hub
{
    private readonly LiveHubConnectionTracker _tracker;
    private readonly LiveRecordReadAuthorizer _authorizer;

    public LiveHub(LiveHubConnectionTracker tracker, LiveRecordReadAuthorizer authorizer)
    {
        _tracker = tracker;
        _authorizer = authorizer;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId is null || Context.User is null)
        {
            Context.Abort();
            return;
        }

        await _tracker.RegisterAsync(
            Context.ConnectionId,
            userId.Value,
            Context.User,
            Context.Abort,
            Context.ConnectionAborted);

        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _tracker.Unregister(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    public async Task WatchRecord(string type, int id)
    {
        var userId = GetUserId();
        if (userId is null || Context.User is null || id <= 0 || string.IsNullOrWhiteSpace(type))
            throw new HubException("Invalid watch request.");

        var stillValid = await _tracker.ReevaluateAsync(Context.ConnectionId, Context.ConnectionAborted);
        if (!stillValid)
            throw new HubException("Session is no longer active.");

        if (!await _authorizer.CanReadAsync(userId.Value, Context.User, type, id, Context.ConnectionAborted))
            throw new HubException("You do not have read access to this record.");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"record:{type}:{id}", Context.ConnectionAborted);
        _tracker.TrackWatch(Context.ConnectionId, type, id);
    }

    public Task UnwatchRecord(string type, int id)
    {
        if (id <= 0 || string.IsNullOrWhiteSpace(type))
            return Task.CompletedTask;

        _tracker.UntrackWatch(Context.ConnectionId, type, id);
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, $"record:{type}:{id}", Context.ConnectionAborted);
    }

    private int? GetUserId()
    {
        var raw = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : null;
    }
}
