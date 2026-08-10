namespace MicroLIMS.Infrastructure.Storage;

// Placeholder - swap for Azure Blob / S3 in production deployments.
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
        await File.WriteAllBytesAsync(path, content);
        return path;
    }

    public Task<byte[]> ReadAsync(string path) => File.ReadAllBytesAsync(path);
}
