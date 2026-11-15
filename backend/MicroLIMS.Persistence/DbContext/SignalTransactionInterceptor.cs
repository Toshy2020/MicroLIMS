using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MicroLIMS.Persistence.DbContext;

/// <summary>
/// Flushes queued live signals on explicit transaction commit and discards them on rollback/failure,
/// covering services that manage <c>BeginTransactionAsync</c> directly as well as <c>UnitOfWork</c>.
/// </summary>
internal sealed class SignalTransactionInterceptor : DbTransactionInterceptor
{
    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
    {
        if (eventData.Context is MicroLimsDbContext db)
            db.FlushSignalsSync();
    }

    public override Task TransactionCommittedAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        return eventData.Context is MicroLimsDbContext db
            ? db.FlushSignalsAsync(cancellationToken)
            : Task.CompletedTask;
    }

    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
    {
        if (eventData.Context is MicroLimsDbContext db)
            db.DiscardSignals();
    }

    public override Task TransactionRolledBackAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is MicroLimsDbContext db)
            db.DiscardSignals();
        return Task.CompletedTask;
    }

    public override void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData)
    {
        if (eventData.Context is MicroLimsDbContext db)
            db.DiscardSignals();
    }

    public override Task TransactionFailedAsync(
        DbTransaction transaction,
        TransactionErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is MicroLimsDbContext db)
            db.DiscardSignals();
        return Task.CompletedTask;
    }
}
