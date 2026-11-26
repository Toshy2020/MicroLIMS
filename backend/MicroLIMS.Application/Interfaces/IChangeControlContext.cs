using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Application.Interfaces;

public sealed record ChangeControlGrant(
    int RecordId,
    string Number,
    int LinkId,
    IReadOnlySet<AuditLog> PriorAuditEntries);

public interface IChangeControlContext
{
    string? Number { get; set; }
}

public sealed class ChangeControlContext : IChangeControlContext
{
    public string? Number { get; set; }
}

public sealed class NullChangeControlContext : IChangeControlContext
{
    public static readonly NullChangeControlContext Instance = new();

    private NullChangeControlContext() { }

    public string? Number
    {
        get => null;
        set { }
    }
}
