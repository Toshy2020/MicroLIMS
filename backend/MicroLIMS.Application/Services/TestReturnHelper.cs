using Microsoft.EntityFrameworkCore;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record TestReturnInfo(string? Reason, DateTime ReturnedAt);

// Derived state helper for count-test orders returned to the analyst for revision.
// A test order is pending return if:
// 1. At least one TestReturnEvent exists for the test order, AND
// 2. Zero active CountTestReading rows exist for the test order (IsActive == true).
// When both hold, the reason and returned-at timestamp are taken from the latest
// TestReturnEvent (OrderByDescending(ReturnedAt)).
public static class TestReturnHelper
{
    public static async Task<TestReturnInfo?> GetPendingReturnAsync(MicroLimsDbContext db, int testOrderId)
    {
        var hasReturnEvents = await db.TestReturnEvents.AnyAsync(e => e.TestOrderId == testOrderId);
        if (!hasReturnEvents)
            return null;

        var hasActiveReadings = await db.CountTestReadings.AnyAsync(r => r.TestOrderId == testOrderId && r.IsActive);
        if (hasActiveReadings)
            return null;

        var latestEvent = await db.TestReturnEvents
            .Where(e => e.TestOrderId == testOrderId)
            .OrderByDescending(e => e.ReturnedAt)
            .FirstOrDefaultAsync();

        if (latestEvent == null)
            return null;

        return new TestReturnInfo(latestEvent.Reason, latestEvent.ReturnedAt);
    }

    public static async Task<Dictionary<int, TestReturnInfo>> GetPendingReturnsForOrdersAsync(MicroLimsDbContext db, IEnumerable<int> testOrderIds)
    {
        var idList = testOrderIds.Distinct().ToList();
        if (idList.Count == 0)
            return new Dictionary<int, TestReturnInfo>();

        var returnEvents = await db.TestReturnEvents
            .Where(e => idList.Contains(e.TestOrderId))
            .OrderByDescending(e => e.ReturnedAt)
            .ToListAsync();

        if (returnEvents.Count == 0)
            return new Dictionary<int, TestReturnInfo>();

        var returnedOrderIds = returnEvents.Select(e => e.TestOrderId).Distinct().ToList();

        var activeReadingOrderIds = await db.CountTestReadings
            .Where(r => returnedOrderIds.Contains(r.TestOrderId) && r.IsActive)
            .Select(r => r.TestOrderId)
            .Distinct()
            .ToListAsync();

        var activeReadingSet = new HashSet<int>(activeReadingOrderIds);

        var result = new Dictionary<int, TestReturnInfo>();
        foreach (var evt in returnEvents)
        {
            if (!activeReadingSet.Contains(evt.TestOrderId) && !result.ContainsKey(evt.TestOrderId))
            {
                result[evt.TestOrderId] = new TestReturnInfo(evt.Reason, evt.ReturnedAt);
            }
        }

        return result;
    }
}
