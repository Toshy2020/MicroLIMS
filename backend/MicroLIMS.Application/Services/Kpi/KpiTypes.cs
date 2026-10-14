using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Kpi;

public enum KpiDirection { HigherIsBetter, LowerIsBetter, Neutral }
public enum KpiGroup { Headline, Flow, Quality, Micro, Workload }
public enum KpiStatus { Ok, NoData, Partial }

// From/To is the half-open UTC window [From, To). Now is read once by the
// service so every KPI in one response shares a single clock.
public sealed record KpiQuery(DateTime From, DateTime To, IReadOnlyList<int>? SectionIds, string? Category, string? Location,
    string? TestCode, int? AnalystId, DateTime Now, int ViewerUserId, bool CanViewTeam)
{
    // The equal-length window immediately before this one: [From-(To-From), From).
    public KpiQuery PriorPeriod()
    {
        var length = To - From;
        return this with { From = From - length, To = From };
    }
}

public sealed record KpiPrior(decimal? Value, int N, decimal? Numerator, decimal? Denominator);
public sealed record KpiSeriesPoint(string Label, string? Group, DateTime? At, decimal? Value, int N, decimal? Secondary = null);
public sealed record KpiGap(string Code, string Message, int Count);

public sealed record KpiResult(decimal? Value, decimal? Numerator, decimal? Denominator, int N, KpiPrior? Prior, KpiStatus Status,
    bool TooFewRecords, IReadOnlyList<KpiGap> Gaps, IReadOnlyList<KpiSeriesPoint> Series,
    IReadOnlyDictionary<string, decimal?> Extras, IReadOnlyList<string> Flags);

// Inclusion: "Numerator" | "Denominator" | "Excluded".
public sealed record KpiRecordDto(int? SampleId, int? TestOrderId, string? ControlNumber, string? TestCode, string? Category,
    string? AnalystName, DateTime? EventAt, decimal? Value, string? Unit, string Inclusion, string? Note);
public sealed record KpiRecordPageDto(string Key, int Page, int PageSize, int Total, IReadOnlyList<KpiRecordDto> Items);
public sealed record KpiEntryDto(string Key, string Name, string Unit, KpiDirection Direction, KpiGroup Group, int Order, KpiResult? Result, string? Error);
public sealed record KpiDashboardDto(DateTime From, DateTime To, DateTime PriorFrom, DateTime PriorTo, DateTime GeneratedAt, IReadOnlyList<KpiEntryDto> Kpis);
public sealed record KpiDefinitionDto(string Key, string Name, string Unit, KpiDirection Direction, KpiGroup Group, int Order, string Definition, string ClockEvents, string Population, string Source);
public sealed record ProfileInForceDto(WorkflowType WorkflowType, string? TestCode, decimal? WorkUnits, decimal? TargetAnalystDays, DateOnly EffectiveFrom, int VersionId);
public sealed record KpiDefinitionsDto(IReadOnlyList<KpiDefinitionDto> Kpis, IReadOnlyList<ProfileInForceDto> ProfilesInForce);

public static class KpiConstants
{
    public const int MinSampleForRate = 5;
    public const int DefaultTargetDays = 7;
    public const decimal EmAlertRateAdverse = 0.025m;
    public const decimal EmActionRateAdverse = 0.0015m;
    public const int IncubationGraceHours = 4;
}
