namespace MicroLIMS.Application.Abstractions.Live;

/// <summary>
/// Canonical <see cref="LiveSignal.Kind"/> values carried on the live signal hub.
/// </summary>
public static class LiveSignalKinds
{
    public const string Notifications = "notifications";
    public const string Counters = "counters";
    public const string Record = "record";
    public const string MyWork = "mywork";
    public const string Messages = "messages";
}

/// <summary>
/// Minimal invalidation signal emitted after a database transaction commits.
/// Contains no PHI or business payload—clients re-fetch via normal authorized APIs.
/// </summary>
public sealed record LiveSignal(string Kind, string Key, int? ActorUserId);

/// <summary>
/// Publishes live invalidation signals to connected clients after commit.
/// Implementations must be safe to resolve as singletons (or without leaking scopes).
/// </summary>
public interface ILiveSignalPublisher
{
    Task PublishAsync(IReadOnlyCollection<LiveSignal> signals, CancellationToken ct = default);
}
