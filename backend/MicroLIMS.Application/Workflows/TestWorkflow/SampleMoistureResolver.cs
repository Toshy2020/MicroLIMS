using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Workflows;

public record SampleMoisture(decimal Percent, int TestOrderId, int ParameterResultId, string TestCode, MoistureRole Role);

public record SampleMoistureView(MoistureRole Role, decimal Percent, string TestCode);

// The loss on drying / water % a dried or anhydrous-basis assay uses: the active result of
// the same sample's own LOD / Karl Fischer test (the test definitions flagged with the role),
// never a typed value.
public sealed class SampleMoistureResolver
{
    private readonly IMicroLimsDbContext _db;

    public SampleMoistureResolver(IMicroLimsDbContext db) => _db = db;

    // The moisture role a set of specification bases asks for (None when no dried / anhydrous basis).
    public static MoistureRole NeededRole(IEnumerable<ResultBasis?> bases)
    {
        var list = bases.ToList();
        bool dried = list.Contains(ResultBasis.PercentDriedBasis), anhydrous = list.Contains(ResultBasis.PercentAnhydrousBasis);
        if (dried && anhydrous) throw new InvalidOperationException("One titration cannot report on both dried and anhydrous basis.");
        return dried ? MoistureRole.LossOnDrying : anhydrous ? MoistureRole.Water : MoistureRole.None;
    }

    public static string MissingMessage(MoistureRole role) => role == MoistureRole.LossOnDrying
        ? "Record the sample's loss on drying result before this assay."
        : "Record the sample's water content (Karl Fischer) result before this assay.";

    // Null while the sample has no active result from a test of that role.
    public async Task<SampleMoisture?> ResolveAsync(int sampleId, MoistureRole role)
    {
        var codes = _db.TestDefinitions.Where(d => d.MoistureRole == role).Select(d => d.Code);
        var results = await _db.ParameterResults
            .Where(r => r.IsActive && r.TestOrder!.SampleId == sampleId && codes.Contains(r.TestOrder.TestCode))
            .Select(r => new { r.Id, r.TestOrderId, r.TestOrder!.TestCode, r.ReportedValue })
            .ToListAsync();
        if (results.Count == 0) return null;

        string refusal = role == MoistureRole.LossOnDrying
            ? "The loss on drying test must report one numeric result."
            : "The water test must report one numeric result.";
        if (results.Count != 1 || results[0].ReportedValue is not decimal percent || percent < 0m || percent >= 100m)
            throw new InvalidOperationException(refusal);
        return new SampleMoisture(percent, results[0].TestOrderId, results[0].Id, results[0].TestCode, role);
    }

    // For the titration screen: the moisture value this order's specification bases will use, or null
    // when it needs none or the sample's moisture test has no result yet.
    public async Task<SampleMoistureView?> GetForTitrationOrderAsync(int testOrderId, CancellationToken ct = default)
    {
        var order = await _db.TestOrders.AsNoTracking().Include(t => t.Sample)
            .FirstOrDefaultAsync(t => t.Id == testOrderId, ct)
            ?? throw new NotFoundException($"Test order {testOrderId} not found.");
        if (order.Sample?.ItemId is not int itemId) return null;

        var specs = await SpecificationLookup.ForSampleAsync(_db, order.SampleId, itemId, order.TestCode, ct);
        var role = NeededRole(specs.Select(s => s.ResultBasis));
        if (role == MoistureRole.None) return null;
        var m = await ResolveAsync(order.SampleId, role);
        return m == null ? null : new SampleMoistureView(role, m.Percent, m.TestCode);
    }
}
