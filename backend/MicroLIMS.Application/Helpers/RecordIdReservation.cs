using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;

namespace MicroLIMS.Application.Helpers;

// Takes the id a new row will be saved under before the row is saved.
//
// A record that is signed when it is created holds a required foreign key
// to its signature, and that signature must be written already carrying
// the record's id: ElectronicSignatures is append-only (PostgreSQL rejects
// any UPDATE of it), so it cannot be pointed at the record afterwards. The
// record cannot be saved first either - it needs the signature's id. So
// the record's id is drawn from its identity sequence up front, and the
// signature and the record are inserted together in one save.
//
// - A rolled-back command leaves a gap in the sequence, as any identity
//   column does.
// - On a non-relational provider (the unit tests run on EF Core's InMemory
//   provider, which has no sequences) the next free id is used instead.
public static class RecordIdReservation
{
    // table is the table's name, which is also its DbSet's name (pass it
    // with nameof); existingIds is that table's ids, for the InMemory case.
    public static async Task<int> ReserveAsync(IMicroLimsDbContext db, string table, IQueryable<int> existingIds)
    {
        if (!db.Database.IsRelational())
            return (await existingIds.MaxAsync(id => (int?)id) ?? 0) + 1;

        var quotedTable = $"\"{table}\"";
        return await db.Database
            .SqlQuery<int>($"SELECT nextval(pg_get_serial_sequence({quotedTable}, 'Id'))::int AS \"Value\"")
            .SingleAsync();
    }
}
