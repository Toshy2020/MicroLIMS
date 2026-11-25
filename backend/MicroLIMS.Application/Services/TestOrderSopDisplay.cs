using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MicroLIMS.Application.Services;

internal static class TestOrderSopDisplay
{
    public static string LabTimeZoneId(TimeZoneInfo? zone) =>
        zone is null ? "UTC"
            : TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var ianaId) ? ianaId : zone.Id;

    public static SopCaptureDto? Capture(TestOrder order) =>
        order.SopCaptureStatus is { } status
            ? new(status.ToString(), order.SopDocumentMasterId, order.SopDocumentCode, order.SopDocumentTitle,
                order.SopRevisionId, order.SopRevisionNumber, order.SopEffectiveDateUtc, order.SopCapturedAtUtc)
            : null;

    public static async Task<Dictionary<int, NewerSopRevisionDto>> ReadNoticesAsync(
        IMicroLimsDbContext db, IEnumerable<TestOrder> orders, DateTime nowUtc, CancellationToken ct = default)
    {
        var ids = orders.Where(o => o.SopCaptureStatus == SopCaptureStatus.Captured &&
            o.SopDocumentMasterId != null && o.SopRevisionId != null).Select(o => o.Id).Distinct().ToArray();
        var notices = new Dictionary<int, NewerSopRevisionDto>();
        if (ids.Length == 0)
            return notices;

        var candidates = await db.TestOrders.AsNoTracking()
            .Where(o => ids.Contains(o.Id) && o.Sample != null && o.Sample.ItemId != null)
            .Select(o => new
            {
                o.Id,
                ItemId = o.Sample == null ? null : o.Sample.ItemId,
                o.SopDocumentMasterId,
                o.SopCapturedAtUtc,
                CapturedSequence = db.DocumentRevisions
                    .Where(r => r.Id == o.SopRevisionId && r.DocumentMasterId == o.SopDocumentMasterId)
                    .Select(r => (int?)r.RevisionSequence).SingleOrDefault(),
                IsOpen = !o.IsSuperseded && (o.Status == ApprovalStatus.Pending || o.Status == ApprovalStatus.InProgress),
                CompletedAt = o.WorkflowHistory.Where(h => h.ToStep == WorkflowStep.Ready)
                    .Max(h => (DateTime?)h.Timestamp)
            }).ToListAsync(ct);
        var current = await new ItemSopResolver(db).ResolveManyAsync(
            candidates.Select(o => o.ItemId.GetValueOrDefault()).Distinct().ToArray(), ct);

        foreach (var order in candidates)
        {
            var revision = current[order.ItemId.GetValueOrDefault()];
            if (revision.Status == SopResolutionStatus.Effective &&
                revision.DocumentMasterId == order.SopDocumentMasterId &&
                revision.RevisionSequence > order.CapturedSequence &&
                revision.RevisionNumber is { } number && revision.EffectiveDate is { } date &&
                date <= nowUtc && date > order.SopCapturedAtUtc &&
                (order.IsOpen || date < order.CompletedAt))
            {
                notices.Add(order.Id, new(number, date));
            }
        }
        return notices;
    }
}
