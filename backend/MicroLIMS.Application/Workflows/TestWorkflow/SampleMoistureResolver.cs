using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
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

    private readonly IUserSectionScopeService? _scope;

    public SampleMoistureResolver(IMicroLimsDbContext db, IUserSectionScopeService? scope = null)
    {
        _db = db;
        _scope = scope;
    }

    // Serializes recording an assay against returning its moisture result: both take this row lock
    // (held to the end of the caller's transaction) before they read or scan, so one always sees
    // what the other committed. A no-op on a non-relational provider (unit tests).
    public static async Task LockMoistureOrderAsync(IMicroLimsDbContext db, int moistureOrderId)
    {
        if (!db.Database.IsRelational()) return;
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"TestOrders\" WHERE \"Id\" = {moistureOrderId} FOR UPDATE");
    }

    // ResolveAsync, then lock the moisture order and read it again: the result an assay records
    // against is the one still active once no return can interleave. Null when it is gone or changed.
    public async Task<SampleMoisture?> ResolveLockedAsync(int sampleId, MoistureRole role)
    {
        var first = await ResolveAsync(sampleId, role);
        if (first == null) return null;
        await LockMoistureOrderAsync(_db, first.TestOrderId);
        var second = await ResolveAsync(sampleId, role);
        return second?.ParameterResultId == first.ParameterResultId ? second : null;
    }

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

    // Null while the sample has no active result from a test of that role. Cancelled, voided and
    // superseded (retested) moisture orders are ignored.
    public async Task<SampleMoisture?> ResolveAsync(int sampleId, MoistureRole role)
    {
        var codes = _db.TestDefinitions.Where(d => d.MoistureRole == role).Select(d => d.Code);
        var results = await _db.ParameterResults
            .Where(r => r.IsActive && r.TestOrder!.SampleId == sampleId && codes.Contains(r.TestOrder.TestCode)
                && !r.TestOrder.IsSuperseded && r.TestOrder.Status != ApprovalStatus.Cancelled && r.TestOrder.Status != ApprovalStatus.Voided)
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

    // For the titration and HPLC screens: the moisture value this order's specification bases will use, or null
    // when it needs none or the sample's moisture test has no result yet.
    public Task<SampleMoistureView?> GetForTitrationOrderAsync(int testOrderId, int userId, CancellationToken ct = default) =>
        GetForAssayOrderAsync(testOrderId, userId, ct);

    public async Task<SampleMoistureView?> GetForAssayOrderAsync(int testOrderId, int userId, CancellationToken ct = default)
    {
        var order = await _db.TestOrders.AsNoTracking().Include(t => t.Sample)
            .FirstOrDefaultAsync(t => t.Id == testOrderId, ct)
            ?? throw new NotFoundException($"Test order {testOrderId} not found.");
        if (order.Sample?.ItemId is not int itemId) return null;

        var specs = await SpecificationLookup.ForSampleAsync(_db, order.SampleId, itemId, order.TestCode, ct);
        var role = NeededRole(specs.Select(s => s.ResultBasis));
        if (role == MoistureRole.None) return null;
        var m = await ResolveAsync(order.SampleId, role);
        if (m == null) return null;
        // the value comes from another test order, possibly in another laboratory section
        await (_scope ?? throw new InvalidOperationException("Section scope is not available.")).EnsureTestOrderAccessAsync(userId, m.TestOrderId, ct);
        return new SampleMoistureView(role, m.Percent, m.TestCode);
    }
}
