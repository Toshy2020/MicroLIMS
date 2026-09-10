namespace MicroLIMS.Infrastructure.Storage;

// The seam between the application and wherever files physically live.
// Verified to be the only route to file I/O in the codebase: nothing
// outside LocalFileStorageService touches File, FileStream or Directory,
// no consumer parses what SaveAsync returns, and only the DI registration
// names a concrete implementation. Replacing local disk with durable
// object storage is therefore one new class and one registration change.
//
// One caveat that is not visible from this interface: SaveAsync returns
// the base-path-combined location, and callers persist that value, so the
// database is coupled to where files currently live. Moving them - to a
// mounted disk or to an object store - invalidates seven columns of
// stored references unless they are migrated with it.
//
// Read docs/Storage_Migration_Implications.md before implementing a
// replacement. The migration is deferred, not designed away.
public interface IFileStorageService
{
    Task<string> SaveAsync(string fileName, byte[] content);
    Task<byte[]> ReadAsync(string path);
}
