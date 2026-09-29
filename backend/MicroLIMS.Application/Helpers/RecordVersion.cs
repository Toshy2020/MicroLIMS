using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MicroLIMS.Application.Helpers;

// Refuses an edit made from a form that is out of date: the client sends
// the Version it loaded (If-Match), and if the record has been saved since,
// nothing is written. The xmin concurrency token already stops two saves
// racing each other; this catches a form left open while a colleague saved.
public static class RecordVersion
{
    public static void EnsureCurrent(IMicroLimsDbContext db, IVersionedEntity record)
    {
        if (db.ExpectedVersion is uint expected && expected != record.Version)
            throw new DbUpdateConcurrencyException(
                $"{record.GetType().Name} changed after the form was loaded (version {expected}, now {record.Version}).");
    }
}
