using System.Collections.Concurrent;
using System.Security.Claims;
using MicroLIMS.API.Authorization;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace MicroLIMS.API.Hubs;

/// <summary>
/// Tracks active <see cref="LiveHub"/> connections so each connection's
/// section memberships and token revocation state can be re-evaluated every
/// 5 minutes (using the injected <see cref="TimeProvider"/>) and on every
/// <c>WatchRecord</c> call without holding scoped services on a singleton.
/// </summary>
public sealed class LiveHubConnectionTracker
{
    private static readonly TimeSpan ReevaluationInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<LiveHub> _hubContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<LiveHubConnectionTracker> _logger;
    private readonly ConcurrentDictionary<string, ConnectionEntry> _connections = new();

    public LiveHubConnectionTracker(
        IServiceScopeFactory scopeFactory,
        IHubContext<LiveHub> hubContext,
        TimeProvider timeProvider,
        ILogger<LiveHubConnectionTracker> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task RegisterAsync(
        string connectionId,
        int userId,
        ClaimsPrincipal principal,
        Action abortConnection,
        CancellationToken ct)
    {
        var entry = new ConnectionEntry(connectionId, userId, principal, abortConnection);
        if (!_connections.TryAdd(connectionId, entry))
        {
            Unregister(connectionId);
            _connections[connectionId] = entry;
        }

        await _hubContext.Groups.AddToGroupAsync(connectionId, $"user:{userId}", ct);
        await RefreshConnectionCoreAsync(entry, ct);

        if (!entry.IsDisposed)
        {
            entry.Timer = _timeProvider.CreateTimer(
                static state =>
                {
                    var (tracker, connId) = ((LiveHubConnectionTracker, string))state!;
                    _ = tracker.RefreshSafeAsync(connId);
                },
                (this, connectionId),
                ReevaluationInterval,
                ReevaluationInterval);
        }
    }

    public async Task<bool> ReevaluateAsync(string connectionId, CancellationToken ct)
    {
        if (!_connections.TryGetValue(connectionId, out var entry) || entry.IsDisposed)
            return false;

        return await RefreshConnectionCoreAsync(entry, ct);
    }

    /// <summary>Remembers a record group the connection joined so a later re-evaluation can drop it.</summary>
    public void TrackWatch(string connectionId, string type, int id)
    {
        if (_connections.TryGetValue(connectionId, out var entry))
        {
            lock (entry.Watched)
                entry.Watched.Add((type, id));
        }
    }

    public void UntrackWatch(string connectionId, string type, int id)
    {
        if (_connections.TryGetValue(connectionId, out var entry))
        {
            lock (entry.Watched)
                entry.Watched.Remove((type, id));
        }
    }

    public void Unregister(string connectionId)
    {
        if (_connections.TryRemove(connectionId, out var entry))
            entry.Dispose();
    }

    private async Task RefreshSafeAsync(string connectionId)
    {
        try
        {
            if (!_connections.TryGetValue(connectionId, out var entry) || entry.IsDisposed)
                return;

            await RefreshConnectionCoreAsync(entry, entry.Cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Connection disconnected while re-evaluation was queued.
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "LiveHub periodic re-evaluation failed for connection {ConnectionId}", connectionId);
        }
    }

    private async Task<bool> RefreshConnectionCoreAsync(ConnectionEntry entry, CancellationToken ct)
    {
        await entry.SyncLock.WaitAsync(ct);
        try
        {
            if (entry.IsDisposed)
                return false;

            await using var scope = _scopeFactory.CreateAsyncScope();
            var revalidator = scope.ServiceProvider.GetRequiredService<AccessTokenRevalidator>();
            var rejection = await revalidator.GetRejectionReasonAsync(entry.Principal, ct);
            if (rejection is not null)
            {
                _logger.LogInformation(
                    "Closing LiveHub connection {ConnectionId} for user {UserId}: {Reason}",
                    entry.ConnectionId, entry.UserId, rejection);
                Unregister(entry.ConnectionId);
                entry.AbortConnection();
                return false;
            }

            var scopeService = scope.ServiceProvider.GetRequiredService<IUserSectionScopeService>();
            var readable = await scopeService.GetReadableSectionIdsAsync(entry.UserId, ct);

            IReadOnlyList<int> targetIds;
            if (readable is null)
            {
                var db = scope.ServiceProvider.GetRequiredService<IMicroLimsDbContext>();
                targetIds = await db.DocumentSections
                    .AsNoTracking()
                    .Where(s => s.IsActive)
                    .Select(s => s.Id)
                    .ToListAsync(ct);
            }
            else
            {
                targetIds = readable;
            }

            var targetSet = targetIds.ToHashSet();
            foreach (var addedId in targetSet.Where(id => !entry.SectionIds.Contains(id)))
            {
                await _hubContext.Groups.AddToGroupAsync(entry.ConnectionId, $"section:{addedId}", ct);
            }

            foreach (var removedId in entry.SectionIds.Where(id => !targetSet.Contains(id)).ToList())
            {
                await _hubContext.Groups.RemoveFromGroupAsync(entry.ConnectionId, $"section:{removedId}", ct);
            }

            entry.SectionIds = targetSet;

            // Record groups joined through WatchRecord: drop the ones the user can no longer read.
            (string Type, int Id)[] watched;
            lock (entry.Watched)
                watched = entry.Watched.ToArray();
            if (watched.Length > 0)
            {
                var authorizer = scope.ServiceProvider.GetRequiredService<LiveRecordReadAuthorizer>();
                foreach (var (type, id) in watched)
                {
                    if (await authorizer.CanReadAsync(entry.UserId, entry.Principal, type, id, ct))
                        continue;
                    await _hubContext.Groups.RemoveFromGroupAsync(entry.ConnectionId, $"record:{type}:{id}", ct);
                    lock (entry.Watched)
                        entry.Watched.Remove((type, id));
                }
            }

            return true;
        }
        finally
        {
            entry.SyncLock.Release();
        }
    }

    private sealed class ConnectionEntry : IDisposable
    {
        public string ConnectionId { get; }
        public int UserId { get; }
        public ClaimsPrincipal Principal { get; }
        public Action AbortConnection { get; }
        public SemaphoreSlim SyncLock { get; } = new(1, 1);
        public CancellationTokenSource Cts { get; } = new();
        public HashSet<int> SectionIds { get; set; } = new();
        public HashSet<(string Type, int Id)> Watched { get; } = new();
        public ITimer? Timer { get; set; }
        public volatile bool IsDisposed;

        public ConnectionEntry(
            string connectionId,
            int userId,
            ClaimsPrincipal principal,
            Action abortConnection)
        {
            ConnectionId = connectionId;
            UserId = userId;
            Principal = principal;
            AbortConnection = abortConnection;
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;
            Timer?.Dispose();
            Cts.Cancel();
            Cts.Dispose();
        }
    }
}
