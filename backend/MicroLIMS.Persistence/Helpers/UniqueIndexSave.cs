using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;
using Npgsql;

namespace MicroLIMS.Persistence.Helpers;

// Saves a record whose generated identifier (e.g. a prepared media lot
// number) is guarded by a unique index. Two users generating the next
// identifier at the same moment both pick the same value; the database
// rejects the second save, and this reports that instead of throwing so
// the caller can pick a fresh identifier and save again.
public static class UniqueIndexSave
{
    public static async Task<bool> TrySaveChangesAsync(MicroLimsDbContext db, string uniqueIndexName)
    {
        var auditAlreadyPending = db.ChangeTracker.Entries<AuditLog>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .ToHashSet();

        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
                                           && postgres.ConstraintName == uniqueIndexName)
        {
            // SaveChangesAsync captured audit rows for the rejected values
            // before the insert failed. Drop them, or the retry would log
            // the change twice - once with the identifier that was never
            // saved.
            foreach (var entry in db.ChangeTracker.Entries<AuditLog>()
                         .Where(e => e.State == EntityState.Added && !auditAlreadyPending.Contains(e.Entity))
                         .ToList())
            {
                entry.State = EntityState.Detached;
            }

            return false;
        }
    }
}
