using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Tests.WorkflowTests;

public static class IncubationTestClock
{
    public static async Task ElapseOpenIncubationsAsync(MicroLimsDbContext db, int testOrderId, int hours = 200)
    {
        var incubations = await db.Incubations
            .Where(i => i.TestOrderId == testOrderId && i.CompletedAt == null)
            .ToListAsync();

        foreach (var inc in incubations)
        {
            inc.StartedAt = inc.StartedAt.AddHours(-hours);
            if (inc.IncubationStartUtc.HasValue)
            {
                inc.IncubationStartUtc = inc.IncubationStartUtc.Value.AddHours(-hours);
            }
            if (inc.IncubationEndUtc.HasValue)
            {
                inc.IncubationEndUtc = inc.IncubationEndUtc.Value.AddHours(-hours);
            }
            if (inc.ExpectedReadingAt.HasValue)
            {
                inc.ExpectedReadingAt = inc.ExpectedReadingAt.Value.AddHours(-hours);
            }
            if (inc.WindowReceivedAtUtc.HasValue)
            {
                inc.WindowReceivedAtUtc = inc.WindowReceivedAtUtc.Value.AddHours(-hours);
            }
        }

        await db.SaveChangesAsync();
    }
}
