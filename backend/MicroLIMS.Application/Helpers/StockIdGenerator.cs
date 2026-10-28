using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;

namespace MicroLIMS.Application.Helpers;

// Stock lot IDs (Master Testing Materials Q8): STK-{yyyy}-{seq:D5}, e.g. STK-2026-00123.
//
// There is ONE running number for the whole app: the sequence does not restart each year, so the number
// alone identifies a lot and the year is only the lab-local year of receipt. Past 99999 the number simply
// grows to six digits. Taken from the PostgreSQL sequence
// stock_id_seq so concurrent receipts can't collide; a rolled-back receipt leaves a gap. The unique index
// IX_Materials_StockId is the backstop. Existing in-stock lots were numbered by the migration from the same sequence.
public static class StockIdGenerator
{
    public const string SequenceName = "stock_id_seq";

    public static async Task<string> NextAsync(IDatabaseSequenceHelper sequences, ILabClock clock, DateTime receivedAtUtc, CancellationToken ct = default)
    {
        var number = await sequences.GetNextSequenceValueAsync(SequenceName, ct);
        return Format(clock.ToLabLocal(receivedAtUtc).Year, number);
    }

    public static string Format(int year, long number) => $"STK-{year:D4}-{number:D5}";
}
