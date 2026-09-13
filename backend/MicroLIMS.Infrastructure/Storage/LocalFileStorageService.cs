namespace MicroLIMS.Infrastructure.Storage;

// Filesystem-backed implementation for local development. Durable only if
// the directory behind Storage:BasePath is itself durable - inside a
// container it is not, which is why production uses S3FileStorageService.
//
// SaveAsync returns the relative key rather than the combined path, so what
// callers persist does not depend on where this directory lives. ReadAsync
// still accepts the combined paths older rows hold.
public class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;

    public LocalFileStorageService(string basePath)
    {
        _basePath = basePath;
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> SaveAsync(string fileName, byte[] content)
    {
        var key = StorageKey.Normalize(fileName, legacyBasePath: null);
        var path = ResolvePath(key);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        await File.WriteAllBytesAsync(path, content);
        return key;
    }

    public async Task<byte[]> ReadAsync(string path)
    {
        var key = StorageKey.Normalize(path, _basePath);

        try
        {
            return await File.ReadAllBytesAsync(ResolvePath(key));
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new StoredFileNotFoundException(key, ex);
        }
    }

    private string ResolvePath(string key) =>
        Path.Combine(_basePath, key.Replace('/', Path.DirectorySeparatorChar));
}
