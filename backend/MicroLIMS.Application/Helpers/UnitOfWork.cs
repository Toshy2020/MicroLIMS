using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Helpers;

// Runs a command that saves more than once - a result, its projection,
// the workflow transition, the auto-submit for review - as one database
// transaction, so a failure part-way leaves none of it behind instead of
// a committed result with no ResultRecord or no transition.
//
// - Joins the caller's transaction when there is one, so commands that
//   call other commands commit or roll back together.
// - Does nothing on a non-relational provider: the unit tests run on EF
//   Core's InMemory provider, which throws on BeginTransactionAsync.
// - A refused electronic signature is recorded and then thrown. Rolling
//   the command back would erase that record and let the attempt escape
//   the signature throttle, so it is written again after the rollback.
// - After a rollback the change tracker is put back to match the
//   database for everything the command touched - its new rows are
//   detached, rows it changed are reloaded - and left alone otherwise, so
//   a caller that handles the failure can keep using the same context.
public static class UnitOfWork
{
    public static async Task RunAsync(MicroLimsDbContext db, Func<Task> command)
    {
        await RunAsync(db, async () =>
        {
            await command();
            return true;
        });
    }

    public static async Task<T> RunAsync<T>(MicroLimsDbContext db, Func<Task<T>> command)
    {
        if (!db.Database.IsRelational() || db.Database.CurrentTransaction is not null)
            return await command();

        var before = db.ChangeTracker.Entries()
            .ToDictionary(e => e.Entity, e => (e.State, Values: e.CurrentValues.Clone()), ReferenceEqualityComparer.Instance);

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var result = await command();
            await tx.CommitAsync();
            return result;
        }
        catch
        {
            var refusedAttempts = db.ChangeTracker.Entries<AuditLog>()
                .Where(e => e.State == EntityState.Unchanged && ElectronicSignatureService.IsRefusedAttemptRecord(e.Entity))
                .Select(e => e.Entity)
                .ToList();

            await tx.RollbackAsync();
            await RestoreTrackerAsync(db, before);

            if (refusedAttempts.Count > 0)
            {
                foreach (var entry in refusedAttempts)
                    entry.Id = 0;
                db.AuditLogs.AddRange(refusedAttempts);
                await db.SaveChangesAsync();
            }

            throw;
        }
    }

    private static async Task RestoreTrackerAsync(
        MicroLimsDbContext db, Dictionary<object, (EntityState State, Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues Values)> before)
    {
        foreach (var entry in db.ChangeTracker.Entries().ToList())
        {
            if (!before.TryGetValue(entry.Entity, out var was) || was.State != EntityState.Unchanged)
            {
                // Created during the command (its row was rolled back), or a
                // pending change the command's saves carried with them.
                entry.State = EntityState.Detached;
            }
            else if (entry.State != EntityState.Unchanged || !SameValues(entry.CurrentValues, was.Values))
            {
                // Saved by the command, then rolled back: the database has
                // the values from before it again.
                await entry.ReloadAsync();
            }
        }
    }

    private static bool SameValues(
        Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues current,
        Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues before) =>
        current.Properties.All(p => Equals(current[p], before[p]));
}
