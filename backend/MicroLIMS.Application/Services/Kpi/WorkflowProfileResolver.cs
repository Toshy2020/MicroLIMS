using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Kpi;

public sealed record ResolvedProfile(decimal? WorkUnits, decimal? TargetAnalystDays, int? WorkUnitsVersionId, int? TargetVersionId)
{
    public bool IsWeighted => WorkUnits.HasValue;
    public bool IsTargetAware => TargetAnalystDays.HasValue;
}

// Resolves a test's profile per field (spec 2.2). Pure after construction:
// callers load the Effective versions and the TestCode -> WorkflowType map once
// and resolve any number of tests without touching the database. The type is the
// test definition's CURRENT type (spec 2.2), so re-typing a test moves its history
// to the new type's default; an override keyed by test code is unaffected.
public sealed class WorkflowProfileResolver
{
    private static readonly ResolvedProfile Unresolved = new(null, null, null, null);

    private readonly ILookup<string, WorkflowProfileVersion> _overrides;
    private readonly ILookup<WorkflowType, WorkflowProfileVersion> _defaults;
    private readonly IReadOnlyDictionary<string, WorkflowType> _typeByTestCode;

    public WorkflowProfileResolver(
        IEnumerable<WorkflowProfileVersion> versions,
        IReadOnlyDictionary<string, WorkflowType> workflowTypeByTestCode)
    {
        _typeByTestCode = workflowTypeByTestCode;

        // Newest first; ToLookup keeps that order inside each group.
        var effective = versions
            .Where(v => v.Status == WorkflowProfileStatus.Effective)
            .OrderByDescending(v => v.EffectiveFrom)
            .ThenByDescending(v => v.Id)
            .ToList();
        _overrides = effective.Where(v => v.TestCode != null).ToLookup(v => v.TestCode!, StringComparer.Ordinal);
        _defaults = effective.Where(v => v.TestCode == null).ToLookup(v => v.WorkflowType);
    }

    // `at` is the approval time in UTC; only its calendar date is compared with EffectiveFrom.
    public ResolvedProfile Resolve(string testCode, DateTime at)
    {
        if (!_typeByTestCode.TryGetValue(testCode, out var type)) return Unresolved;
        var day = DateOnly.FromDateTime(at);
        return Pick(InForce(_overrides[testCode], day), InForce(_defaults[type], day));
    }

    // The type default alone, for the configuration overview.
    public ResolvedProfile ResolveDefault(WorkflowType type, DateTime at) =>
        Pick(Array.Empty<WorkflowProfileVersion>(), InForce(_defaults[type], DateOnly.FromDateTime(at)));

    public bool HasOverride(string testCode, DateTime at)
    {
        var day = DateOnly.FromDateTime(at);
        return _overrides[testCode].Any(v => v.EffectiveFrom <= day);
    }

    private static ResolvedProfile Pick(IReadOnlyList<WorkflowProfileVersion> own, IReadOnlyList<WorkflowProfileVersion> def)
    {
        var units = own.FirstOrDefault(v => v.WorkUnits.HasValue) ?? def.FirstOrDefault(v => v.WorkUnits.HasValue);
        var target = own.FirstOrDefault(v => v.TargetAnalystDays.HasValue) ?? def.FirstOrDefault(v => v.TargetAnalystDays.HasValue);
        return new ResolvedProfile(units?.WorkUnits, target?.TargetAnalystDays, units?.Id, target?.Id);
    }

    private static IReadOnlyList<WorkflowProfileVersion> InForce(IEnumerable<WorkflowProfileVersion> rows, DateOnly day) =>
        rows.Where(v => v.EffectiveFrom <= day).ToList();
}
