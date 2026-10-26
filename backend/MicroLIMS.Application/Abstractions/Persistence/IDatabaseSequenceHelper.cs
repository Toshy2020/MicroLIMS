namespace MicroLIMS.Application.Abstractions.Persistence;

public interface IDatabaseSequenceHelper
{
    Task<long> GetNextSequenceValueAsync(string sequenceName, CancellationToken cancellationToken = default);

    // Takes the id a new TEntity row will be saved under before the row is
    // saved, from the table's identity sequence.
    //
    // A record that is signed when it is created holds a required foreign
    // key to its signature, and that signature must be written already
    // carrying the record's id: ElectronicSignatures is append-only
    // (PostgreSQL rejects any UPDATE of it), so it cannot be pointed at the
    // record afterwards. The record cannot be saved first either - it needs
    // the signature's id. So the record's id is reserved up front, and the
    // signature and the record are inserted together in one save.
    //
    // A rolled-back command leaves a gap in the sequence, as any identity
    // column does. On a non-relational provider (the unit tests' InMemory
    // provider has no sequences) the next free id is used instead.
    Task<int> ReserveIdAsync<TEntity>(CancellationToken cancellationToken = default) where TEntity : class;
}
