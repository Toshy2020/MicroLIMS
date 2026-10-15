namespace MicroLIMS.Application.Services.Kpi;

public interface IKpi
{
    string Key { get; }
    string Name { get; }
    string Unit { get; }   // "days", "%", "tests", "units"
    KpiDirection Direction { get; }
    KpiGroup Group { get; }
    int Order { get; }
    string Definition { get; }
    string ClockEvents { get; }
    string Population { get; }
    string Source { get; }
    Task<KpiResult> ComputeAsync(KpiQuery query, CancellationToken ct);
    Task<KpiRecordPageDto> GetRecordsAsync(KpiQuery query, int page, int pageSize, CancellationToken ct);
}
