using MicroLIMS.Application.Abstractions.Persistence;

namespace MicroLIMS.Application.Services.Kpi;

// Tile 4. A lab-quality signal, not an analyst measure.
public sealed class RetestRateKpi : IKpi
{
    private readonly IMicroLimsDbContext _db;
    public RetestRateKpi(IMicroLimsDbContext db) => _db = db;

    public string Key => "retest-rate";
    public string Name => "Retest rate";
    public string Unit => "%";
    public KpiDirection Direction => KpiDirection.LowerIsBetter;
    public KpiGroup Group => KpiGroup.Headline;
    public int Order => 4;
    public string Definition => "Samples with a 'retest retained sample' decision, divided by all samples that received an approval decision in the period. It is a subset of the OOS rate. It describes the laboratory, not an individual analyst.";
    public string ClockEvents => "The decision time is when the section sign-off decision was electronically signed.";
    public string Population => "Distinct samples with at least one decided section sign-off (approve, reject, retest, new sample) in the period, within your sections. Retest samples created from an earlier sample are not counted again.";
    public string Source => "SampleSectionSignoff, Sample";

    public Task<KpiResult> ComputeAsync(KpiQuery q, CancellationToken ct) =>
        KpiScope.ComputeAsync(q, Unit, async (qq, c) => (await LoadAsync(qq, c)).Core, ct);

    public async Task<KpiRecordPageDto> GetRecordsAsync(KpiQuery q, int page, int pageSize, CancellationToken ct) =>
        KpiScope.Page(Key, (await LoadAsync(q, ct)).Records, page, pageSize);

    private Task<(KpiCore Core, List<KpiRecordDto> Records)> LoadAsync(KpiQuery q, CancellationToken ct) =>
        KpiScope.DecisionRateAsync(_db, q, d => d.Retest, d => d.Retest ? "Decision: retest of retained sample" : null, ct);
}
