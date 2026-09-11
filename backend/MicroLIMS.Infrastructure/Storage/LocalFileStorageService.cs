namespace MicroLIMS.Infrastructure.Storage;

// Filesystem-backed implementation. Durable only if the directory behind
// Storage:BasePath is itself durable - inside a container it is not, and
// the files here include GxP evidence (see RecordArchiveService).
//
// Note SaveAsync returns the combined path rather than the key it was
// given; callers persist that, which is what couples the database to this
// location. See docs/Storage_Migration_Implications.md.
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
        var path = Path.Combine(_basePath, fileName);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        await File.WriteAllBytesAsync(path, content);
        return path;
    }

    public Task<byte[]> ReadAsync(string path) => File.ReadAllBytesAsync(path);
}
