using MicroLIMS.Application.Abstractions.Persistence;

namespace MicroLIMS.Application.Services.Kpi;

// Tile 5. Nothing writes the OOSInvestigation decision, so the OOS definition used by OosTrackingService
// (the sample carries an OosGroupCode) is used instead.
public sealed class OosRateKpi : IKpi
{
    private readonly IMicroLimsDbContext _db;
    public OosRateKpi(IMicroLimsDbContext db) => _db = db;

    public string Key => "oos-rate";
    public string Name => "OOS rate";
    public string Unit => "%";
    public KpiDirection Direction => KpiDirection.LowerIsBetter;
    public KpiGroup Group => KpiGroup.Headline;
    public int Order => 5;
    public string Definition => "Samples that belong to an out-of-specification group, divided by all samples that received an approval decision in the period. The retest rate is a subset of this figure. This is a laboratory quality signal, not an analyst penalty.";
    public string ClockEvents => "The decision time is when the section sign-off decision was electronically signed.";
    public string Population => "Distinct samples with at least one decided section sign-off in the period, within your sections. Retest samples created from an earlier sample are not counted again, so one OOS event counts once. The OOS group is recorded on the sample, so a sample whose OOS arose in another laboratory is still counted.";
    public string Source => "SampleSectionSignoff, Sample (OosGroupCode)";

    public Task<KpiResult> ComputeAsync(KpiQuery q, CancellationToken ct) =>
        KpiScope.ComputeAsync(q, Unit, async (qq, c) => (await LoadAsync(qq, c)).Core, ct);

    public async Task<KpiRecordPageDto> GetRecordsAsync(KpiQuery q, int page, int pageSize, CancellationToken ct) =>
        KpiScope.Page(Key, (await LoadAsync(q, ct)).Records, page, pageSize);

    // ponytail: OosGroupCode is sample-level, so a lab-1-only viewer still sees a lab-2 OOS; scope it per section if that matters.
    private Task<(KpiCore Core, List<KpiRecordDto> Records)> LoadAsync(KpiQuery q, CancellationToken ct) =>
        KpiScope.DecisionRateAsync(_db, q, d => d.Oos, d => d.OosGroupCode, ct);
}
