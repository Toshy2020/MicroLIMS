using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Interfaces;

public sealed record ChangeControlGrant(int RecordId, string Number, int LinkId);

public interface IChangeControlContext
{
    string? Number { get; set; }
    ChangeControlGrant? GetScopedGrant(QualityLinkTargetType targetType, int targetId);
    void SetScopedGrant(QualityLinkTargetType targetType, int targetId, ChangeControlGrant grant);
}

public sealed class ChangeControlContext : IChangeControlContext
{
    private readonly Dictionary<(QualityLinkTargetType TargetType, int TargetId), ChangeControlGrant> _grants = new();

    public string? Number { get; set; }

    public ChangeControlGrant? GetScopedGrant(QualityLinkTargetType targetType, int targetId) =>
        _grants.GetValueOrDefault((targetType, targetId));

    public void SetScopedGrant(QualityLinkTargetType targetType, int targetId, ChangeControlGrant grant) =>
        _grants[(targetType, targetId)] = grant;
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

    public ChangeControlGrant? GetScopedGrant(QualityLinkTargetType targetType, int targetId) => null;

    public void SetScopedGrant(QualityLinkTargetType targetType, int targetId, ChangeControlGrant grant) { }
}
