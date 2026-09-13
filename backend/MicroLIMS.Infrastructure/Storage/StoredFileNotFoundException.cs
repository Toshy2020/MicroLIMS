namespace MicroLIMS.Infrastructure.Storage;

// Raised by every IFileStorageService implementation when no bytes exist
// for a key, so callers can tell "the file is gone" apart from a transient
// storage fault without knowing which provider is in use. Derives from
// FileNotFoundException so existing catch blocks keep working.
public class StoredFileNotFoundException : FileNotFoundException
{
    public StoredFileNotFoundException(string key, Exception? innerException = null)
        : base($"No stored file exists for key '{key}'.", key, innerException)
    {
    }

    public string Key => FileName!;
}
