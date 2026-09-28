namespace MicroLIMS.Domain.Entities;

// A record people edit through a form. Version is PostgreSQL's row version
// (xmin), which changes on every write. It goes out with the record and
// comes back with the edit (If-Match), so a save from a form loaded before
// someone else's change is refused instead of overwriting that change.
public interface IVersionedEntity
{
    uint Version { get; set; }
}
